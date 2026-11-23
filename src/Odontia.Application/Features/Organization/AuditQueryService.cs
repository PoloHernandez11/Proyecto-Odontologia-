using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Domain.Authorization;

namespace Odontia.Application.Features.Organization;

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditQuery query, CancellationToken ct);
}

internal sealed class AuditQueryService(IApplicationDbContext db, ITenantContext tenant) : IAuditQueryService
{
    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.AuditRead);
        var cid = tenant.ConsultorioId;
        var query = db.AuditLogs.Where(a => a.ConsultorioId == cid);
        if (!string.IsNullOrWhiteSpace(q.EntityType)) query = query.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) query = query.Where(a => a.EntityId == q.EntityId);
        if (q.UserId is not null) query = query.Where(a => a.UserId == q.UserId);
        if (q.FromUtc is not null) query = query.Where(a => a.OccurredAtUtc >= q.FromUtc);
        if (q.ToUtc is not null) query = query.Where(a => a.OccurredAtUtc < q.ToUtc);
        var page = await query.OrderByDescending(a => a.OccurredAtUtc).ToPagedAsync(q, ct);
        var ids = page.Items.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        return new PagedResult<AuditLogDto>(page.Items.Select(a => new AuditLogDto(a.Id, a.OccurredAtUtc, a.Action.ToString(), a.EntityType,
            a.EntityId, a.UserId, a.UserId is null ? null : names.GetValueOrDefault(a.UserId.Value), a.Details, a.IpAddress)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }
}
