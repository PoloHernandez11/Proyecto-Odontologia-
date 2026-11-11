using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Finance;

public sealed record RegisterExpenseRequest(string Description, ExpenseCategory Category, decimal Amount, PaymentMethod Method,
    DateTimeOffset? PaidAt, string? Supplier, string? Reference, string? Notes, Guid? LaboratoryId);

public sealed record VoidExpenseRequest(string Reason);

public sealed record ExpenseDto(Guid Id, string Description, ExpenseCategory Category, decimal Amount, PaymentMethod Method, DateTime PaidAtUtc,
    string? Supplier, string? Reference, string? Notes, Guid? LaboratoryId, string? LaboratoryName, ExpenseStatus Status, string RegisteredBy,
    DateTime? VoidedAtUtc, string? VoidReason);

public sealed class ExpenseQuery : PageQuery
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public ExpenseCategory? Category { get; set; }
    public bool IncludeVoided { get; set; } = true;
}

public sealed class FinanceSummaryQuery
{
    /// <summary>Inicio del período (incluido).</summary>
    public DateTimeOffset? From { get; set; }
    /// <summary>Fin del período (excluido).</summary>
    public DateTimeOffset? To { get; set; }
}

public sealed record ExpenseCategoryTotalDto(ExpenseCategory Category, decimal Total);

/// <summary>Ingresos (pagos de pacientes) y egresos registrados en un período; los anulados no se cuentan.</summary>
public sealed record FinanceSummaryDto(DateTime? FromUtc, DateTime? ToUtc, decimal Income, int PaymentCount, decimal Expenses, int ExpenseCount,
    decimal Net, IReadOnlyList<ExpenseCategoryTotalDto> ExpensesByCategory);

public interface IExpenseService
{
    Task<PagedResult<ExpenseDto>> SearchAsync(ExpenseQuery query, CancellationToken ct);
    Task<ExpenseDto> GetAsync(Guid id, CancellationToken ct);
    Task<ExpenseDto> RegisterAsync(RegisterExpenseRequest request, CancellationToken ct);
    Task<ExpenseDto> VoidAsync(Guid id, VoidExpenseRequest request, CancellationToken ct);
    Task<FinanceSummaryDto> GetSummaryAsync(FinanceSummaryQuery query, CancellationToken ct);
}

/// <summary>
/// Egresos del consultorio (insumos, laboratorio, alquiler, etc.). Usan los mismos permisos que los pagos:
/// finance.read para consultar, payments.write para registrar y payments.void para anular.
/// </summary>
internal sealed class ExpenseService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, IAuditWriter audit) : IExpenseService
{
    public async Task<PagedResult<ExpenseDto>> SearchAsync(ExpenseQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.FinanceRead);
        var query = db.Expenses.AsNoTracking().AsQueryable();
        if (q.From is not null) query = query.Where(e => e.PaidAtUtc >= q.From.Value.UtcDateTime);
        if (q.To is not null) query = query.Where(e => e.PaidAtUtc < q.To.Value.UtcDateTime);
        if (q.Category is not null) query = query.Where(e => e.Category == q.Category);
        if (!q.IncludeVoided) query = query.Where(e => e.Status == ExpenseStatus.Registered);
        var page = await query.OrderByDescending(e => e.PaidAtUtc).ToPagedAsync(q, ct);
        return new PagedResult<ExpenseDto>(await ToDtosAsync(page.Items, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<ExpenseDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.FinanceRead);
        var e = await db.Expenses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("El egreso no existe.");
        return (await ToDtosAsync([e], ct))[0];
    }

    public async Task<ExpenseDto> RegisterAsync(RegisterExpenseRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PaymentsWrite);
        var now = clock.UtcNow;
        var paidAt = r.PaidAt?.UtcDateTime ?? now;
        if (paidAt > now.AddMinutes(5)) throw new RequestValidationException("paidAt", "La fecha del egreso no puede ser futura.");
        if (r.LaboratoryId is { } labId && !await db.Laboratories.AnyAsync(l => l.Id == labId, ct))
            throw new RequestValidationException("laboratoryId", "El laboratorio no existe.");

        var expense = new Expense(tenant.ConsultorioId, r.Description, r.Category, r.Amount, r.Method, paidAt, r.Supplier, r.Reference, r.Notes,
            r.LaboratoryId, tenant.UserId);
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return await GetAsync(expense.Id, ct);
    }

    /// <summary>Anulación controlada: el egreso no se elimina; queda marcado como anulado con su motivo.</summary>
    public async Task<ExpenseDto> VoidAsync(Guid id, VoidExpenseRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PaymentsVoid);
        var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("El egreso no existe.");
        e.Void(r.Reason, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.ExpenseVoided, nameof(Expense), e.Id.ToString(), e.Description);
        return await GetAsync(id, ct);
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync(FinanceSummaryQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.FinanceRead);
        var from = q.From?.UtcDateTime;
        var to = q.To?.UtcDateTime;
        if (from is not null && to is not null && to <= from) throw new RequestValidationException("to", "El fin del período debe ser posterior al inicio.");

        var payments = db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Registered);
        var expenses = db.Expenses.AsNoTracking().Where(e => e.Status == ExpenseStatus.Registered);
        if (from is not null) { payments = payments.Where(p => p.PaidAtUtc >= from); expenses = expenses.Where(e => e.PaidAtUtc >= from); }
        if (to is not null) { payments = payments.Where(p => p.PaidAtUtc < to); expenses = expenses.Where(e => e.PaidAtUtc < to); }

        // Se suma en memoria: no todos los proveedores (SQLite en las pruebas) agregan decimales en el servidor.
        var income = await payments.Select(p => p.Amount).ToListAsync(ct);
        var spent = await expenses.Select(e => new { e.Category, e.Amount }).ToListAsync(ct);
        var byCategory = spent.GroupBy(e => e.Category).Select(g => new ExpenseCategoryTotalDto(g.Key, g.Sum(e => e.Amount)))
            .OrderByDescending(c => c.Total).ToList();
        var totalIncome = income.Sum();
        var totalExpenses = spent.Sum(e => e.Amount);
        return new FinanceSummaryDto(from, to, totalIncome, income.Count, totalExpenses, spent.Count, totalIncome - totalExpenses, byCategory);
    }

    private async Task<List<ExpenseDto>> ToDtosAsync(IReadOnlyList<Expense> items, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, items.Select(e => e.RegisteredByUserId), ct);
        var labIds = items.Where(e => e.LaboratoryId != null).Select(e => e.LaboratoryId!.Value).Distinct().ToList();
        var labs = await db.Laboratories.AsNoTracking().Where(l => labIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        return items.Select(e => new ExpenseDto(e.Id, e.Description, e.Category, e.Amount, e.Method, e.PaidAtUtc, e.Supplier, e.Reference, e.Notes,
            e.LaboratoryId, e.LaboratoryId is null ? null : labs.GetValueOrDefault(e.LaboratoryId.Value), e.Status,
            names.GetValueOrDefault(e.RegisteredByUserId, "?"), e.VoidedAtUtc, e.VoidReason)).ToList();
    }
}
