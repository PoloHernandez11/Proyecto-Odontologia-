using Microsoft.EntityFrameworkCore.Storage;
using Odontia.Application.Abstractions;

namespace Odontia.Infrastructure.Persistence;

/// <summary>Adaptador de <see cref="IDbContextTransaction"/> (EF Core) al puerto <see cref="IApplicationTransaction"/>.</summary>
internal sealed class EfApplicationTransaction(IDbContextTransaction inner) : IApplicationTransaction
{
    public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
