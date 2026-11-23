namespace Odontia.Application.Abstractions;

/// <summary>
/// Transacción de base de datos abierta desde la capa de aplicación (ver <see cref="IApplicationDbContext.BeginTransactionAsync"/>
/// e <see cref="IAgendaLock"/>). Si se libera sin <see cref="CommitAsync"/>, se revierte. Evita que los puertos expongan
/// tipos de infraestructura de EF Core.
/// </summary>
public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}
