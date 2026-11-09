using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Finance;

public sealed record BudgetItemRequest(Guid? ProcedureId, string? Description, int? ToothCode, int Quantity, decimal? UnitPrice,
    decimal DiscountPercent, Guid? TreatmentPlanItemId);

public sealed record SaveBudgetRequest(Guid? TreatmentPlanId, DateOnly? IssueDate, DateOnly? ExpirationDate, decimal DiscountPercent,
    string? Notes, IReadOnlyList<BudgetItemRequest>? Items, bool ImportPlanItems = false);

public sealed record BudgetDecisionRequest(string? Reason);

public sealed record BudgetItemDto(Guid Id, Guid? ProcedureId, string Description, int? ToothCode, int Quantity, decimal UnitPrice,
    decimal DiscountPercent, decimal LineTotal, Guid? TreatmentPlanItemId);

public sealed record BudgetDto(Guid Id, int Number, Guid PatientId, string PatientName, Guid? TreatmentPlanId, DateOnly IssueDate,
    DateOnly ExpirationDate, BudgetStatus Status, decimal Subtotal, decimal DiscountPercent, decimal DiscountAmount, decimal Total,
    decimal Paid, decimal Outstanding, string? Notes, string? StatusReason, DateTime? ApprovedAtUtc, IReadOnlyList<BudgetItemDto> Items,
    DateTime CreatedAtUtc);

public sealed record BudgetStatusChangeDto(DateTime ChangedAtUtc, BudgetStatus FromStatus, BudgetStatus ToStatus, string? Reason, string? ChangedBy);

public sealed class BudgetQuery : PageQuery
{
    public BudgetStatus? Status { get; set; }
    public Guid? PatientId { get; set; }
}

public sealed record RegisterPaymentRequest(Guid PatientId, Guid? BudgetId, decimal Amount, PaymentMethod Method, DateTimeOffset? PaidAt,
    string? Reference, string? Notes);

public sealed record VoidPaymentRequest(string Reason);

public sealed record PaymentDto(Guid Id, int ReceiptNumber, Guid PatientId, string PatientName, Guid? BudgetId, int? BudgetNumber, decimal Amount,
    PaymentMethod Method, DateTime PaidAtUtc, string? Reference, string? Notes, PaymentStatus Status, string ReceivedBy,
    DateTime? VoidedAtUtc, string? VoidReason);

public sealed class PaymentQuery : PageQuery
{
    public Guid? PatientId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public PaymentMethod? Method { get; set; }
    public bool IncludeVoided { get; set; } = true;
}

public sealed record AccountMovementDto(Guid Id, DateTime OccurredAtUtc, MovementType Type, MovementDirection Direction, decimal Amount,
    decimal RunningBalance, string Description, Guid? BudgetId, Guid? PaymentId);

/// <summary>Cuenta corriente calculada en el servidor. Balance &gt; 0 = saldo deudor; &lt; 0 = saldo a favor.</summary>
public sealed record AccountStatementDto(Guid PatientId, decimal TotalCharges, decimal TotalCredits, decimal Balance,
    IReadOnlyList<AccountMovementDto> Movements, IReadOnlyList<BudgetBalanceDto> Budgets);

public sealed record BudgetBalanceDto(Guid BudgetId, int Number, decimal Total, decimal Paid, decimal Outstanding);

public interface IBudgetService
{
    Task<PagedResult<BudgetDto>> SearchAsync(BudgetQuery query, CancellationToken ct);
    Task<BudgetDto> GetAsync(Guid id, CancellationToken ct);
    Task<BudgetDto> CreateAsync(Guid patientId, SaveBudgetRequest request, CancellationToken ct);
    Task<BudgetDto> UpdateAsync(Guid id, SaveBudgetRequest request, CancellationToken ct);
    Task<BudgetDto> IssueAsync(Guid id, CancellationToken ct);
    Task<BudgetDto> ApproveAsync(Guid id, CancellationToken ct);
    Task<BudgetDto> RejectAsync(Guid id, BudgetDecisionRequest request, CancellationToken ct);
    Task<BudgetDto> CancelAsync(Guid id, BudgetDecisionRequest request, CancellationToken ct);
    Task<IReadOnlyList<BudgetStatusChangeDto>> GetHistoryAsync(Guid id, CancellationToken ct);
    Task<int> ExpireDueBudgetsAsync(CancellationToken ct);
}

public interface IPaymentService
{
    Task<PagedResult<PaymentDto>> SearchAsync(PaymentQuery query, CancellationToken ct);
    Task<PaymentDto> GetAsync(Guid id, CancellationToken ct);
    Task<PaymentDto> RegisterAsync(RegisterPaymentRequest request, CancellationToken ct);
    Task<PaymentDto> VoidAsync(Guid id, VoidPaymentRequest request, CancellationToken ct);
    Task<AccountStatementDto> GetStatementAsync(Guid patientId, CancellationToken ct);
}

internal sealed class BudgetService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, ISequenceGenerator sequences,
    IAuditWriter audit) : IBudgetService
{
    public async Task<PagedResult<BudgetDto>> SearchAsync(BudgetQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsRead);
        var query = db.Budgets.AsNoTracking().Include(b => b.Items).AsQueryable();
        if (q.Status is not null) query = query.Where(b => b.Status == q.Status);
        if (q.PatientId is not null) query = query.Where(b => b.PatientId == q.PatientId);
        var page = await query.OrderByDescending(b => b.Number).ToPagedAsync(q, ct);
        return new PagedResult<BudgetDto>(await ToDtosAsync(page.Items, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<BudgetDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsRead);
        var b = await db.Budgets.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("El presupuesto no existe.");
        return (await ToDtosAsync([b], ct))[0];
    }

    public async Task<BudgetDto> CreateAsync(Guid patientId, SaveBudgetRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsWrite);
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct)) throw new NotFoundException("El paciente no existe.");
        await EnsurePlanAsync(patientId, r.TreatmentPlanId, ct);
        var today = await clock.TodayAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var number = await sequences.NextAsync(ConsultorioSequence.Budget, ct);
        var budget = new Budget(tenant.ConsultorioId, patientId, number, r.TreatmentPlanId, r.IssueDate ?? today, r.ExpirationDate ?? today.AddDays(30),
            r.DiscountPercent, r.Notes);
        await AddItemsAsync(budget, patientId, r, ct);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(budget.Id, ct);
    }

    public async Task<BudgetDto> UpdateAsync(Guid id, SaveBudgetRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsWrite);
        var b = await LoadAsync(id, ct);
        if (!b.IsEditable) throw new BusinessRuleException("Sólo pueden modificarse presupuestos en borrador.", "budget_not_editable");
        await EnsurePlanAsync(b.PatientId, r.TreatmentPlanId, ct);
        b.SetDates(r.IssueDate ?? b.IssueDate, r.ExpirationDate ?? b.ExpirationDate);
        b.SetDiscount(r.DiscountPercent);
        b.SetNotes(r.Notes);
        foreach (var item in b.Items.ToList()) b.RemoveItem(item.Id);
        await AddItemsAsync(b, b.PatientId, r, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<BudgetDto> IssueAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsWrite);
        var b = await LoadAsync(id, ct);
        b.Issue(tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Aprobación: genera el cargo en la cuenta corriente del paciente.</summary>
    public async Task<BudgetDto> ApproveAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsApprove);
        var b = await LoadAsync(id, ct);
        b.Approve(tenant.UserId, clock.UtcNow, await clock.TodayAsync(ct));
        if (b.Total > 0)
            db.AccountMovements.Add(new AccountMovement(tenant.ConsultorioId, b.PatientId, MovementType.BudgetCharge, MovementDirection.Debit, b.Total,
                clock.UtcNow, $"Presupuesto #{b.Number} aprobado", b.Id, null, null));
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.BudgetStatusChanged, nameof(Budget), b.Id.ToString(), "Aprobado");
        return await GetAsync(id, ct);
    }

    public async Task<BudgetDto> RejectAsync(Guid id, BudgetDecisionRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsApprove);
        var b = await LoadAsync(id, ct);
        b.Reject(r.Reason, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.BudgetStatusChanged, nameof(Budget), b.Id.ToString(), "Rechazado");
        return await GetAsync(id, ct);
    }

    /// <summary>Cancelación: si estaba aprobado, se registra un contraasiento (los pagos quedan como saldo a favor).</summary>
    public async Task<BudgetDto> CancelAsync(Guid id, BudgetDecisionRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsApprove);
        var b = await LoadAsync(id, ct);
        var wasApproved = b.Status == BudgetStatus.Approved;
        b.Cancel(r.Reason ?? "", tenant.UserId, clock.UtcNow);
        if (wasApproved && b.Total > 0)
        {
            var charge = await db.AccountMovements.FirstOrDefaultAsync(m => m.BudgetId == b.Id && m.Type == MovementType.BudgetCharge, ct);
            db.AccountMovements.Add(new AccountMovement(tenant.ConsultorioId, b.PatientId, MovementType.BudgetChargeReversal, MovementDirection.Credit,
                b.Total, clock.UtcNow, $"Cancelación presupuesto #{b.Number}", b.Id, null, charge?.Id));
        }
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.BudgetStatusChanged, nameof(Budget), b.Id.ToString(), "Cancelado");
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<BudgetStatusChangeDto>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.BudgetsRead);
        if (!await db.Budgets.AnyAsync(b => b.Id == id, ct)) throw new NotFoundException();
        var rows = await db.BudgetStatusChanges.AsNoTracking().Where(c => c.BudgetId == id).OrderBy(c => c.ChangedAtUtc).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, rows.Where(r => r.ChangedByUserId != null).Select(r => r.ChangedByUserId!.Value), ct);
        return rows.Select(c => new BudgetStatusChangeDto(c.ChangedAtUtc, c.FromStatus, c.ToStatus, c.Reason,
            c.ChangedByUserId is null ? "Sistema" : names.GetValueOrDefault(c.ChangedByUserId.Value))).ToList();
    }

    public async Task<int> ExpireDueBudgetsAsync(CancellationToken ct)
    {
        var today = await clock.TodayAsync(ct);
        var due = await db.Budgets.Where(b => b.Status == BudgetStatus.Issued && b.ExpirationDate < today).ToListAsync(ct);
        foreach (var b in due) b.MarkExpiredIfDue(today, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return due.Count;
    }

    private async Task AddItemsAsync(Budget budget, Guid patientId, SaveBudgetRequest r, CancellationToken ct)
    {
        if (r.ImportPlanItems)
        {
            if (r.TreatmentPlanId is null) throw new RequestValidationException("treatmentPlanId", "Indique el plan de tratamiento a importar.");
            var items = await db.TreatmentPlanItems.AsNoTracking().Where(i => i.TreatmentPlanId == r.TreatmentPlanId && i.Status != TreatmentStatus.Cancelled)
                .OrderBy(i => i.SortOrder).ToListAsync(ct);
            foreach (var i in items)
                budget.AddItem(i.ProcedureId, DescribeItem(i.ProcedureName, i.ToothCode, i.Surfaces), i.ToothCode, 1, i.UnitPrice, 0, i.Id);
        }

        var procIds = (r.Items ?? []).Where(i => i.ProcedureId != null).Select(i => i.ProcedureId!.Value).Distinct().ToList();
        var procedures = await db.Procedures.AsNoTracking().Where(p => procIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var planItemIds = (r.Items ?? []).Where(i => i.TreatmentPlanItemId != null).Select(i => i.TreatmentPlanItemId!.Value).Distinct().ToList();
        if (planItemIds.Count > 0)
        {
            var valid = await db.TreatmentPlanItems.CountAsync(i => planItemIds.Contains(i.Id) && db.TreatmentPlans.Any(p => p.Id == i.TreatmentPlanId && p.PatientId == patientId), ct);
            if (valid != planItemIds.Count) throw new RequestValidationException("treatmentPlanItemId", "Una prestación del plan no corresponde al paciente.");
        }

        foreach (var i in r.Items ?? [])
        {
            ProcedureCatalogItem? proc = null;
            if (i.ProcedureId is { } pid && !procedures.TryGetValue(pid, out proc))
                throw new RequestValidationException("procedureId", "La prestación no existe en el catálogo.");
            var description = i.Description ?? proc?.Name ?? throw new RequestValidationException("description", "Indique la descripción de la línea.");
            // Se toma el valor vigente del catálogo como valor histórico de la línea.
            var price = i.UnitPrice ?? proc?.Price ?? throw new RequestValidationException("unitPrice", "Indique el valor unitario.");
            budget.AddItem(i.ProcedureId, description, i.ToothCode, i.Quantity, price, i.DiscountPercent, i.TreatmentPlanItemId);
        }
    }

    private static string DescribeItem(string name, int? tooth, string? surfaces) =>
        tooth is null ? name : $"{name} - pieza {tooth}{(surfaces is null ? "" : $" ({surfaces})")}";

    private async Task EnsurePlanAsync(Guid patientId, Guid? planId, CancellationToken ct)
    {
        if (planId is { } id && !await db.TreatmentPlans.AnyAsync(p => p.Id == id && p.PatientId == patientId, ct))
            throw new RequestValidationException("treatmentPlanId", "El plan no corresponde al paciente.");
    }

    private async Task<Budget> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Budgets.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException("El presupuesto no existe.");

    private async Task<List<BudgetDto>> ToDtosAsync(IReadOnlyList<Budget> budgets, CancellationToken ct)
    {
        var ids = budgets.Select(b => b.Id).ToList();
        var payments = await db.Payments.AsNoTracking().Where(p => p.BudgetId != null && ids.Contains(p.BudgetId.Value)).ToListAsync(ct);
        var patientIds = budgets.Select(b => b.PatientId).Distinct().ToList();
        var patients = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);
        return budgets.Select(b =>
        {
            var paid = payments.Where(p => p.BudgetId == b.Id && p.Status == PaymentStatus.Registered).Sum(p => p.Amount);
            return new BudgetDto(b.Id, b.Number, b.PatientId, patients.GetValueOrDefault(b.PatientId, "?"), b.TreatmentPlanId, b.IssueDate, b.ExpirationDate,
                b.Status, b.Subtotal, b.DiscountPercent, b.DiscountAmount, b.Total, paid, AccountCalculator.BudgetOutstanding(b, payments), b.Notes,
                b.StatusReason, b.ApprovedAtUtc,
                b.Items.OrderBy(i => i.SortOrder).Select(i => new BudgetItemDto(i.Id, i.ProcedureId, i.Description, i.ToothCode, i.Quantity, i.UnitPrice,
                    i.DiscountPercent, i.LineTotal, i.TreatmentPlanItemId)).ToList(), b.CreatedAtUtc);
        }).ToList();
    }
}

internal sealed class PaymentService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, ISequenceGenerator sequences,
    IAuditWriter audit) : IPaymentService
{
    public async Task<PagedResult<PaymentDto>> SearchAsync(PaymentQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.FinanceRead);
        var query = db.Payments.AsNoTracking().AsQueryable();
        if (q.PatientId is not null) query = query.Where(p => p.PatientId == q.PatientId);
        if (q.From is not null) query = query.Where(p => p.PaidAtUtc >= q.From.Value.UtcDateTime);
        if (q.To is not null) query = query.Where(p => p.PaidAtUtc < q.To.Value.UtcDateTime);
        if (q.Method is not null) query = query.Where(p => p.Method == q.Method);
        if (!q.IncludeVoided) query = query.Where(p => p.Status == PaymentStatus.Registered);
        var page = await query.OrderByDescending(p => p.PaidAtUtc).ToPagedAsync(q, ct);
        return new PagedResult<PaymentDto>(await ToDtosAsync(page.Items, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PaymentDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.FinanceRead);
        var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("El pago no existe.");
        return (await ToDtosAsync([p], ct))[0];
    }

    public async Task<PaymentDto> RegisterAsync(RegisterPaymentRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PaymentsWrite);
        if (!await db.Patients.AnyAsync(p => p.Id == r.PatientId, ct)) throw new RequestValidationException("patientId", "El paciente no existe.");
        var now = clock.UtcNow;
        var paidAt = r.PaidAt?.UtcDateTime ?? now;
        if (paidAt > now.AddMinutes(5)) throw new RequestValidationException("paidAt", "La fecha de pago no puede ser futura.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (r.BudgetId is { } bid)
        {
            var budget = await db.Budgets.FirstOrDefaultAsync(b => b.Id == bid && b.PatientId == r.PatientId, ct)
                         ?? throw new RequestValidationException("budgetId", "El presupuesto no corresponde al paciente.");
            if (budget.Status != BudgetStatus.Approved)
                throw new BusinessRuleException("Sólo pueden imputarse pagos a presupuestos aprobados.", "budget_not_approved");
            var payments = await db.Payments.Where(p => p.BudgetId == bid).ToListAsync(ct);
            var outstanding = AccountCalculator.BudgetOutstanding(budget, payments);
            if (r.Amount > outstanding)
                throw new BusinessRuleException($"El importe supera el saldo pendiente del presupuesto ({outstanding:N2}).", "amount_exceeds_outstanding");
        }

        var number = await sequences.NextAsync(ConsultorioSequence.Receipt, ct);
        var payment = new Payment(tenant.ConsultorioId, r.PatientId, r.BudgetId, r.Amount, r.Method, paidAt, r.Reference, r.Notes, tenant.UserId, number);
        db.Payments.Add(payment);
        db.AccountMovements.Add(new AccountMovement(tenant.ConsultorioId, r.PatientId, MovementType.Payment, MovementDirection.Credit, payment.Amount,
            paidAt, $"Pago recibo #{number} ({r.Method})", r.BudgetId, payment.Id, null));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(payment.Id, ct);
    }

    /// <summary>Anulación controlada: el pago no se elimina; se marca anulado y se registra el contraasiento.</summary>
    public async Task<PaymentDto> VoidAsync(Guid id, VoidPaymentRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PaymentsVoid);
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("El pago no existe.");
        p.Void(r.Reason, tenant.UserId, clock.UtcNow);
        var original = await db.AccountMovements.FirstOrDefaultAsync(m => m.PaymentId == p.Id && m.Type == MovementType.Payment, ct);
        db.AccountMovements.Add(new AccountMovement(tenant.ConsultorioId, p.PatientId, MovementType.PaymentReversal, MovementDirection.Debit, p.Amount,
            clock.UtcNow, $"Anulación recibo #{p.ReceiptNumber}: {r.Reason}", p.BudgetId, p.Id, original?.Id));
        await db.SaveChangesAsync(ct);
        audit.Write(AuditAction.PaymentVoided, nameof(Payment), p.Id.ToString(), $"Recibo #{p.ReceiptNumber}");
        return await GetAsync(id, ct);
    }

    public async Task<AccountStatementDto> GetStatementAsync(Guid patientId, CancellationToken ct)
    {
        tenant.Require(Permissions.FinanceRead);
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct)) throw new NotFoundException("El paciente no existe.");
        var movements = await db.AccountMovements.AsNoTracking().Where(m => m.PatientId == patientId)
            .OrderBy(m => m.OccurredAtUtc).ThenBy(m => m.CreatedAtUtc).ToListAsync(ct);
        decimal running = 0;
        var rows = new List<AccountMovementDto>();
        foreach (var m in movements)
        {
            running += m.SignedAmount;
            rows.Add(new AccountMovementDto(m.Id, m.OccurredAtUtc, m.Type, m.Direction, m.Amount, running, m.Description, m.BudgetId, m.PaymentId));
        }
        var budgets = await db.Budgets.AsNoTracking().Where(b => b.PatientId == patientId && b.Status == BudgetStatus.Approved).ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking().Where(p => p.PatientId == patientId).ToListAsync(ct);
        return new AccountStatementDto(patientId,
            movements.Where(m => m.Direction == MovementDirection.Debit).Sum(m => m.Amount),
            movements.Where(m => m.Direction == MovementDirection.Credit).Sum(m => m.Amount),
            AccountCalculator.Balance(movements), rows,
            budgets.Select(b => new BudgetBalanceDto(b.Id, b.Number, b.Total,
                payments.Where(p => p.BudgetId == b.Id && p.Status == PaymentStatus.Registered).Sum(p => p.Amount),
                AccountCalculator.BudgetOutstanding(b, payments))).ToList());
    }

    private async Task<List<PaymentDto>> ToDtosAsync(IReadOnlyList<Payment> items, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, items.Select(p => p.ReceivedByUserId), ct);
        var patientIds = items.Select(p => p.PatientId).Distinct().ToList();
        var patients = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);
        var budgetIds = items.Where(p => p.BudgetId != null).Select(p => p.BudgetId!.Value).Distinct().ToList();
        var numbers = await db.Budgets.AsNoTracking().Where(b => budgetIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Number, ct);
        return items.Select(p => new PaymentDto(p.Id, p.ReceiptNumber, p.PatientId, patients.GetValueOrDefault(p.PatientId, "?"), p.BudgetId,
            p.BudgetId is null ? null : numbers.GetValueOrDefault(p.BudgetId.Value), p.Amount, p.Method, p.PaidAtUtc, p.Reference, p.Notes, p.Status,
            names.GetValueOrDefault(p.ReceivedByUserId, "?"), p.VoidedAtUtc, p.VoidReason)).ToList();
    }
}
