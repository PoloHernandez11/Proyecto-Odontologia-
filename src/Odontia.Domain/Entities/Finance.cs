using Odontia.Domain.Common;
using Odontia.Domain.Dental;
using Odontia.Domain.Enums;

namespace Odontia.Domain.Entities;

/// <summary>
/// Presupuesto. Las líneas guardan descripción y valor unitario al momento de emisión,
/// por lo que los valores históricos no cambian aunque cambie el catálogo.
/// </summary>
public class Budget : TenantEntity
{
    private readonly List<BudgetItem> _items = [];
    private readonly List<BudgetStatusChange> _statusChanges = [];

    private Budget() { }

    public Budget(Guid consultorioId, Guid patientId, int number, Guid? treatmentPlanId, DateOnly issueDate, DateOnly expirationDate,
        decimal discountPercent, string? notes)
    {
        Status = BudgetStatus.Draft;
        ConsultorioId = consultorioId;
        PatientId = patientId;
        Number = number;
        TreatmentPlanId = treatmentPlanId;
        SetDates(issueDate, expirationDate);
        SetDiscount(discountPercent);
        Notes = Guard.Optional(notes, "Observaciones", 2000);
    }

    public Guid PatientId { get; private set; }
    /// <summary>Número correlativo por consultorio.</summary>
    public int Number { get; private set; }
    public Guid? TreatmentPlanId { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly ExpirationDate { get; private set; }
    /// <summary>Descuento global (%) aplicado sobre el subtotal de líneas.</summary>
    public decimal DiscountPercent { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal Total { get; private set; }
    public BudgetStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public string? StatusReason { get; private set; }

    public IReadOnlyCollection<BudgetItem> Items => _items;
    public IReadOnlyCollection<BudgetStatusChange> StatusChanges => _statusChanges;

    public bool IsEditable => Status == BudgetStatus.Draft;

    public void SetDates(DateOnly issueDate, DateOnly expirationDate)
    {
        EnsureEditable();
        if (expirationDate < issueDate) throw new DomainException("La fecha de vencimiento no puede ser anterior a la de emisión.", "invalid_range");
        IssueDate = issueDate;
        ExpirationDate = expirationDate;
    }

    public void SetDiscount(decimal percent)
    {
        EnsureEditable();
        if (percent is < 0 or > 100 || decimal.Round(percent, 2) != percent)
            throw new DomainException("El descuento debe estar entre 0 y 100 con hasta dos decimales.", "invalid_discount");
        DiscountPercent = percent;
        Recalculate();
    }

    public void SetNotes(string? notes) { EnsureEditable(); Notes = Guard.Optional(notes, "Observaciones", 2000); }

    public BudgetItem AddItem(Guid? procedureId, string description, int? toothCode, int quantity, decimal unitPrice, decimal discountPercent,
        Guid? treatmentPlanItemId)
    {
        EnsureEditable();
        if (toothCode is not null) Fdi.Get(toothCode.Value);
        var item = new BudgetItem(ConsultorioId, Id, procedureId, description, toothCode, quantity, unitPrice, discountPercent, treatmentPlanItemId, _items.Count + 1);
        _items.Add(item);
        Recalculate();
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureEditable();
        var item = _items.FirstOrDefault(i => i.Id == itemId) ?? throw new DomainException("La línea no pertenece al presupuesto.", "item_not_found");
        _items.Remove(item);
        Recalculate();
    }

    public void Issue(Guid userId, DateTime nowUtc)
    {
        EnsureEditable();
        if (_items.Count == 0) throw new DomainException("No puede emitirse un presupuesto sin prestaciones.", "budget_empty");
        ChangeStatus(BudgetStatus.Issued, null, userId, nowUtc);
    }

    public void Approve(Guid userId, DateTime nowUtc, DateOnly today)
    {
        if (Status != BudgetStatus.Issued) throw new DomainException("Sólo pueden aprobarse presupuestos emitidos.", "invalid_status_transition");
        if (ExpirationDate < today) throw new DomainException("El presupuesto está vencido.", "budget_expired");
        ApprovedAtUtc = nowUtc;
        ApprovedByUserId = userId;
        ChangeStatus(BudgetStatus.Approved, null, userId, nowUtc);
    }

    public void Reject(string? reason, Guid userId, DateTime nowUtc)
    {
        if (Status != BudgetStatus.Issued) throw new DomainException("Sólo pueden rechazarse presupuestos emitidos.", "invalid_status_transition");
        RejectedAtUtc = nowUtc;
        ChangeStatus(BudgetStatus.Rejected, reason, userId, nowUtc);
    }

    public void Cancel(string reason, Guid userId, DateTime nowUtc)
    {
        if (Status is BudgetStatus.Cancelled or BudgetStatus.Rejected or BudgetStatus.Expired)
            throw new DomainException("El presupuesto ya está cerrado.", "invalid_status_transition");
        ChangeStatus(BudgetStatus.Cancelled, Guard.Required(reason, "Motivo", 500), userId, nowUtc);
    }

    public bool MarkExpiredIfDue(DateOnly today, DateTime nowUtc)
    {
        if (Status != BudgetStatus.Issued || ExpirationDate >= today) return false;
        ChangeStatus(BudgetStatus.Expired, "Vencimiento automático", null, nowUtc);
        return true;
    }

    private void ChangeStatus(BudgetStatus to, string? reason, Guid? userId, DateTime nowUtc)
    {
        var from = Status;
        Status = to;
        StatusReason = Guard.Optional(reason, "Motivo", 500);
        _statusChanges.Add(new BudgetStatusChange(ConsultorioId, Id, from, to, StatusReason, userId, nowUtc));
    }

    private void Recalculate()
    {
        Subtotal = _items.Sum(i => i.LineTotal);
        DiscountAmount = decimal.Round(Subtotal * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        Total = Subtotal - DiscountAmount;
    }

    private void EnsureEditable()
    {
        if (Status != BudgetStatus.Draft)
            throw new DomainException("Sólo pueden modificarse presupuestos en borrador.", "budget_not_editable");
    }
}

public class BudgetItem : TenantEntity
{
    private BudgetItem() { }

    internal BudgetItem(Guid consultorioId, Guid budgetId, Guid? procedureId, string description, int? toothCode, int quantity,
        decimal unitPrice, decimal discountPercent, Guid? treatmentPlanItemId, int order)
    {
        if (quantity is < 1 or > 1000) throw new DomainException("La cantidad debe estar entre 1 y 1000.", "invalid_quantity");
        if (discountPercent is < 0 or > 100 || decimal.Round(discountPercent, 2) != discountPercent)
            throw new DomainException("El descuento de línea debe estar entre 0 y 100.", "invalid_discount");
        ConsultorioId = consultorioId;
        BudgetId = budgetId;
        ProcedureId = procedureId;
        Description = Guard.Required(description, "Descripción", 200);
        ToothCode = toothCode;
        Quantity = quantity;
        UnitPrice = Guard.Money(unitPrice, "Valor unitario");
        DiscountPercent = discountPercent;
        TreatmentPlanItemId = treatmentPlanItemId;
        SortOrder = order;
        var gross = UnitPrice * Quantity;
        LineTotal = gross - decimal.Round(gross * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    public Guid BudgetId { get; private set; }
    public Guid? ProcedureId { get; private set; }
    public string Description { get; private set; } = default!;
    public int? ToothCode { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal LineTotal { get; private set; }
    public Guid? TreatmentPlanItemId { get; private set; }
    public int SortOrder { get; private set; }
}

public class BudgetStatusChange : TenantEntity
{
    private BudgetStatusChange() { }

    internal BudgetStatusChange(Guid consultorioId, Guid budgetId, BudgetStatus from, BudgetStatus to, string? reason, Guid? userId, DateTime atUtc)
    {
        ConsultorioId = consultorioId;
        BudgetId = budgetId;
        FromStatus = from;
        ToStatus = to;
        Reason = reason;
        ChangedByUserId = userId;
        ChangedAtUtc = atUtc;
    }

    public Guid BudgetId { get; private set; }
    public BudgetStatus FromStatus { get; private set; }
    public BudgetStatus ToStatus { get; private set; }
    public string? Reason { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
}

/// <summary>Pago recibido. Nunca se elimina: se anula con motivo y contraasiento.</summary>
public class Payment : TenantEntity
{
    private Payment() { }

    public Payment(Guid consultorioId, Guid patientId, Guid? budgetId, decimal amount, PaymentMethod method, DateTime paidAtUtc,
        string? reference, string? notes, Guid receivedByUserId, int receiptNumber)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        BudgetId = budgetId;
        Amount = Guard.Money(amount, "Importe", allowZero: false);
        Method = method;
        PaidAtUtc = DateTime.SpecifyKind(paidAtUtc, DateTimeKind.Utc);
        Reference = Guard.Optional(reference, "Referencia", 100);
        Notes = Guard.Optional(notes, "Observaciones", 500);
        ReceivedByUserId = receivedByUserId;
        ReceiptNumber = receiptNumber;
        Status = PaymentStatus.Registered;
    }

    public Guid PatientId { get; private set; }
    public Guid? BudgetId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public DateTime PaidAtUtc { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public Guid ReceivedByUserId { get; private set; }
    public int ReceiptNumber { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public string? VoidReason { get; private set; }

    public void Void(string reason, Guid userId, DateTime nowUtc)
    {
        if (Status == PaymentStatus.Voided) throw new DomainException("El pago ya fue anulado.", "payment_already_voided");
        VoidReason = Guard.Required(reason, "Motivo de anulación", 500);
        VoidedAtUtc = nowUtc;
        VoidedByUserId = userId;
        Status = PaymentStatus.Voided;
    }
}

/// <summary>Movimiento de cuenta corriente (libro inmutable). Saldo = Σ débitos − Σ créditos.</summary>
public class AccountMovement : TenantEntity
{
    private AccountMovement() { }

    public AccountMovement(Guid consultorioId, Guid patientId, MovementType type, MovementDirection direction, decimal amount,
        DateTime occurredAtUtc, string description, Guid? budgetId, Guid? paymentId, Guid? reversesMovementId)
    {
        ConsultorioId = consultorioId;
        PatientId = patientId;
        Type = type;
        Direction = direction;
        Amount = Guard.Money(amount, "Importe", allowZero: false);
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        Description = Guard.Required(description, "Descripción", 250);
        BudgetId = budgetId;
        PaymentId = paymentId;
        ReversesMovementId = reversesMovementId;
    }

    public Guid PatientId { get; private set; }
    public MovementType Type { get; private set; }
    public MovementDirection Direction { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string Description { get; private set; } = default!;
    public Guid? BudgetId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Guid? ReversesMovementId { get; private set; }

    public decimal SignedAmount => Direction == MovementDirection.Debit ? Amount : -Amount;
}

/// <summary>Cálculos de cuenta corriente (puros, testeables).</summary>
public static class AccountCalculator
{
    public static decimal Balance(IEnumerable<AccountMovement> movements) => movements.Sum(m => m.SignedAmount);

    /// <summary>Saldo pendiente de un presupuesto aprobado: total − pagos vigentes imputados.</summary>
    public static decimal BudgetOutstanding(Budget budget, IEnumerable<Payment> payments) =>
        budget.Status == BudgetStatus.Approved
            ? budget.Total - payments.Where(p => p.BudgetId == budget.Id && p.Status == PaymentStatus.Registered).Sum(p => p.Amount)
            : 0m;
}
