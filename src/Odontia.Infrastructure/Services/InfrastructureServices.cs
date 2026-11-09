using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;
using Odontia.Infrastructure.Persistence;

namespace Odontia.Infrastructure.Services;

/// <summary>
/// Escribe auditoría en una unidad de trabajo independiente: el registro persiste aunque la operación
/// principal falle (p. ej. intentos de login fallidos). Nunca recibe contraseñas, tokens ni contenido clínico.
/// </summary>
internal sealed class AuditWriter(IServiceScopeFactory scopes, ITenantContext tenant, TimeProvider clock, ILogger<AuditWriter> logger) : IAuditWriter
{
    public void Write(AuditAction action, string entityType, string? entityId, string? details = null, Guid? consultorioId = null, Guid? userId = null)
    {
        var cid = consultorioId ?? tenant.ConsultorioIdOrNull;
        var uid = userId ?? (tenant.IsAuthenticated ? tenant.UserId : null);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OdontiaDbContext>();
            db.AuditLogs.Add(new AuditLog(cid, uid, action, entityType, entityId, details, tenant.IpAddress, clock.GetUtcNow().UtcDateTime));
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo registrar el evento de auditoría {Action} sobre {EntityType}", action, entityType);
        }
        logger.LogInformation("Audit {Action} {EntityType} {EntityId} user={UserId} consultorio={ConsultorioId}", action, entityType, entityId, uid, cid);
    }
}

/// <summary>Registra cada mensaje saliente y su resultado real informado por el proveedor.</summary>
internal sealed class MessageDispatcher(IServiceScopeFactory scopes, IEmailSender email, IWhatsAppSender whatsApp, TimeProvider clock,
    ILogger<MessageDispatcher> logger) : IMessageDispatcher
{
    public async Task<OutboundMessage> SendEmailAsync(Guid? consultorioId, string to, string subject, string html, string text, string purpose,
        bool containsSecret, string? relatedType, Guid? relatedId, CancellationToken ct)
    {
        var msg = new OutboundMessage(consultorioId, MessageChannel.Email, to, subject, text, purpose, relatedType, relatedId, clock.GetUtcNow().UtcDateTime, containsSecret);
        SendResult result;
        try { result = await email.SendAsync(new OutgoingEmail(to, subject, html, text), ct); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo al enviar correo {Purpose} a {Recipient}", purpose, Mask.Email(to));
            result = new SendResult(true, false, "email", null, ex.Message);
        }
        return await PersistAsync(msg, result, ct);
    }

    public async Task<OutboundMessage> SendWhatsAppAsync(Guid? consultorioId, string toPhone, string text, string purpose, string? relatedType,
        Guid? relatedId, CancellationToken ct)
    {
        var msg = new OutboundMessage(consultorioId, MessageChannel.WhatsApp, toPhone, null, text, purpose, relatedType, relatedId, clock.GetUtcNow().UtcDateTime, false);
        SendResult result;
        try { result = await whatsApp.SendAsync(new OutgoingWhatsApp(toPhone, text), ct); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo al enviar WhatsApp {Purpose} a {Recipient}", purpose, Mask.Phone(toPhone));
            result = new SendResult(true, false, "whatsapp", null, ex.Message);
        }
        return await PersistAsync(msg, result, ct);
    }

    private async Task<OutboundMessage> PersistAsync(OutboundMessage msg, SendResult result, CancellationToken ct)
    {
        msg.RecordResult(result.Provider, result.Accepted, result.ProviderConfigured, result.ProviderMessageId, result.Error, clock.GetUtcNow().UtcDateTime);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OdontiaDbContext>();
        db.OutboundMessages.Add(msg);
        await db.SaveChangesAsync(ct);
        return msg;
    }
}

/// <summary>Bloqueo de fila (InnoDB: UPDATE toma un lock exclusivo hasta el commit) para serializar reservas.</summary>
internal sealed class AgendaLock(OdontiaDbContext db) : IAgendaLock
{
    public async Task<IDbContextTransaction> AcquireAsync(Guid professionalUserId, Guid? gabineteId, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Members.Where(m => m.UserId == professionalUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockVersion, m => m.LockVersion + 1), ct);
        if (gabineteId is { } g)
            await db.Gabinetes.Where(x => x.Id == g)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LockVersion, x => x.LockVersion + 1), ct);
        return tx;
    }
}

internal sealed class SequenceGenerator(OdontiaDbContext db, ITenantContext tenant) : ISequenceGenerator
{
    public async Task<int> NextAsync(string name, CancellationToken ct)
    {
        var cid = tenant.ConsultorioId;
        var ownTx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var updated = await db.Sequences.Where(s => s.ConsultorioId == cid && s.Name == name)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastValue, x => x.LastValue + 1), ct);
            if (updated == 0)
            {
                db.Sequences.Add(new ConsultorioSequence(cid, name));
                await db.SaveChangesAsync(ct);
                await db.Sequences.Where(s => s.ConsultorioId == cid && s.Name == name)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastValue, x => x.LastValue + 1), ct);
            }
            var value = await db.Sequences.AsNoTracking().Where(s => s.ConsultorioId == cid && s.Name == name).Select(s => s.LastValue).FirstAsync(ct);
            if (ownTx is not null) await ownTx.CommitAsync(ct);
            return value;
        }
        finally
        {
            if (ownTx is not null) await ownTx.DisposeAsync();
        }
    }
}

public sealed record ResolvedMembership(Guid MemberId, bool IsProfessional, IReadOnlyList<string> Permissions);

public interface IMembershipResolver
{
    /// <summary>Membresía activa del usuario en el consultorio (usuario, consultorio y membresía activos), o null.</summary>
    Task<ResolvedMembership?> ResolveAsync(Guid userId, Guid consultorioId, CancellationToken ct);
    Task<string?> GetSecurityStampAsync(Guid userId, CancellationToken ct);
}

internal sealed class MembershipResolver(OdontiaDbContext db) : IMembershipResolver
{
    public async Task<ResolvedMembership?> ResolveAsync(Guid userId, Guid consultorioId, CancellationToken ct)
    {
        var row = await db.Members.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.UserId == userId && m.ConsultorioId == consultorioId && m.IsActive && m.User!.IsActive)
            .Where(m => db.Consultorios.Any(c => c.Id == consultorioId && c.IsActive))
            .Select(m => new
            {
                m.Id,
                m.IsProfessional,
                Permissions = m.Roles.SelectMany(r => r.Role!.Permissions.Select(p => p.Permission)).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new ResolvedMembership(row.Id, row.IsProfessional, row.Permissions.Distinct().ToList());
    }

    public async Task<string?> GetSecurityStampAsync(Guid userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => u.SecurityStamp).FirstOrDefaultAsync(ct);
}

/// <summary>Almacenamiento local privado (desarrollo / instalaciones on-premise). Fuera del directorio web.</summary>
public sealed class LocalStorageOptions
{
    public const string Section = "Storage:Local";
    public string RootPath { get; set; } = "";
}

internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(Microsoft.Extensions.Options.IOptions<LocalStorageOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        var configured = options.Value.RootPath;
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "..", "..", "storage", "documents")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Guid consultorioId, Stream content, string extension, CancellationToken ct)
    {
        if (extension.Any(c => !char.IsLetterOrDigit(c) && c != '.')) throw new ArgumentException("Extensión inválida.", nameof(extension));
        var now = DateTime.UtcNow;
        var key = $"{consultorioId:N}/{now:yyyy}/{now:MM}/{Guid.CreateVersion7():N}{extension}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                sha.AppendData(buffer, 0, read);
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                size += read;
            }
        }
        return new StoredFile(key, size, Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant());
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey);
        if (!File.Exists(path)) throw new NotFoundException("El archivo no se encuentra en el almacenamiento.");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Evita path traversal: la ruta final debe quedar dentro de la raíz.</summary>
    private string Resolve(string key)
    {
        if (key.Contains("..") || Path.IsPathRooted(key)) throw new UnauthorizedAccessException("Clave de almacenamiento inválida.");
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Clave de almacenamiento inválida.");
        return full;
    }
}
