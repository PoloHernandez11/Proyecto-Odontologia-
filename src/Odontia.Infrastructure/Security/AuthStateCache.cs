using System.Collections.Concurrent;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Odontia.Domain.Entities;

namespace Odontia.Infrastructure.Security;

/// <summary>
/// Versionado del estado de autenticación cacheado por solicitud (security stamp, sesión revocada, membresía).
/// Las claves de caché incluyen la versión vigente del usuario y la global: invalidar = incrementar la versión,
/// de modo que una lectura concurrente que guarde datos viejos lo hace bajo una clave que ya nadie consulta.
/// </summary>
public sealed class AuthStateVersions
{
    private readonly ConcurrentDictionary<Guid, long> _users = new();
    private long _global;

    public long Global => Interlocked.Read(ref _global);
    public long For(Guid userId) => _users.TryGetValue(userId, out var v) ? v : 0;

    public void InvalidateUser(Guid userId) => _users.AddOrUpdate(userId, 1, (_, v) => v + 1);
    public void InvalidateAll() => Interlocked.Increment(ref _global);
}

/// <summary>
/// Detecta en SaveChanges cambios que afectan la autorización por solicitud y versiona la caché:
/// usuario (stamp, activo, bloqueo), refresh tokens (revocación de sesión) y membresías/roles/permisos/consultorios.
/// Invalida al guardar y, si había una transacción explícita, también al confirmarla.
/// </summary>
internal sealed class AuthStateInvalidationInterceptor(AuthStateVersions versions) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private sealed class Pending
    {
        public HashSet<Guid> Users { get; } = [];
        public bool All { get; set; }
    }

    private readonly ConditionalWeakTable<DbContext, Pending> _pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush(eventData.Context, keepUntilCommit: true);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context, keepUntilCommit: true);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Flush(eventData.Context, keepUntilCommit: false);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context, keepUntilCommit: false);
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        Pending? pending = null;
        foreach (var e in context.ChangeTracker.Entries())
        {
            if (e.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            switch (e.Entity)
            {
                case ApplicationUser u:
                    (pending ??= GetPending(context)).Users.Add(u.Id);
                    break;
                case RefreshToken t:
                    (pending ??= GetPending(context)).Users.Add(t.UserId);
                    break;
                case ConsultorioMember or MemberRole or Role or RolePermission or Consultorio:
                    (pending ??= GetPending(context)).All = true;
                    break;
            }
        }
    }

    private Pending GetPending(DbContext context) => _pending.GetValue(context, _ => new Pending());

    private void Flush(DbContext? context, bool keepUntilCommit)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending)) return;
        Apply(pending);
        if (!keepUntilCommit || context.Database.CurrentTransaction is null) _pending.Remove(context);
    }

    private void Apply(Pending pending)
    {
        foreach (var u in pending.Users) versions.InvalidateUser(u);
        if (pending.All) versions.InvalidateAll();
    }

    // ---- Transacciones explícitas: se vuelve a invalidar al confirmar (los lectores ya ven los datos nuevos). ----

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => OnTransactionEnded(eventData.Context);

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        OnTransactionEnded(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => OnTransactionEnded(eventData.Context);

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        OnTransactionEnded(eventData.Context);
        return Task.CompletedTask;
    }

    private void OnTransactionEnded(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending)) return;
        Apply(pending);
        _pending.Remove(context);
    }
}
