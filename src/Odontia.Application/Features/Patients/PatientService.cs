using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Patients;

public interface IPatientService
{
    Task<PagedResult<PatientListItemDto>> SearchAsync(PatientQuery query, CancellationToken ct);
    Task<PatientDto> GetAsync(Guid id, CancellationToken ct);
    Task<PatientSummaryDto> GetSummaryAsync(Guid id, CancellationToken ct);
    Task<PatientDto> CreateAsync(CreatePatientRequest request, CancellationToken ct);
    Task<PatientDto> UpdateAsync(Guid id, SavePatientRequest request, CancellationToken ct);
    Task<PatientDto> ChangeStatusAsync(Guid id, PatientStatus status, CancellationToken ct);
    Task<MedicalHistoryDto> GetMedicalHistoryAsync(Guid patientId, CancellationToken ct);
    Task<MedicalHistoryDto> UpdateMedicalHistoryAsync(Guid patientId, MedicalHistoryRequest request, CancellationToken ct);
    Task<IReadOnlyList<AlertDto>> GetAlertsAsync(Guid patientId, CancellationToken ct);
    Task<AlertDto> AddAlertAsync(Guid patientId, SaveAlertRequest request, CancellationToken ct);
    Task<AlertDto> UpdateAlertAsync(Guid patientId, Guid alertId, SaveAlertRequest request, CancellationToken ct);
    Task DeactivateAlertAsync(Guid patientId, Guid alertId, CancellationToken ct);
    Task<IReadOnlyList<PatientHistoryItemDto>> GetAdministrativeHistoryAsync(Guid patientId, CancellationToken ct);
    Task<IReadOnlyList<HealthInsuranceDto>> GetInsurancesAsync(bool includeInactive, CancellationToken ct);
    Task<HealthInsuranceDto> CreateInsuranceAsync(SaveHealthInsuranceRequest request, CancellationToken ct);
    Task<HealthInsuranceDto> UpdateInsuranceAsync(Guid id, SaveHealthInsuranceRequest request, CancellationToken ct);
}

internal sealed class PatientService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock) : IPatientService
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Patient, object?>>> Sorts = new Dictionary<string, Expression<Func<Patient, object?>>>
    {
        ["name"] = p => p.SearchName,
        ["document"] = p => p.DocumentNumber,
        ["created"] = p => p.CreatedAtUtc,
        ["status"] = p => p.Status,
        ["birthdate"] = p => p.BirthDate,
    };

    public async Task<PagedResult<PatientListItemDto>> SearchAsync(PatientQuery q, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsRead);
        var query = db.Patients.AsNoTracking().AsQueryable();
        query = ApplySearch(query, q.Search);
        if (q.Status is not null) query = query.Where(p => p.Status == q.Status);
        if (q.HealthInsuranceId is not null) query = query.Where(p => p.HealthInsuranceId == q.HealthInsuranceId);
        if (q.Private == true) query = query.Where(p => p.HealthInsuranceId == null);

        var now = clock.UtcNow;
        var today = await clock.TodayAsync(ct);
        var projected = query.ApplySort(q.Sort, Sorts, "name").Select(p => new
        {
            p.Id, p.FirstName, p.LastName, p.DocumentNumber, p.BirthDate, p.Phone,
            Insurance = p.HealthInsurance != null ? p.HealthInsurance.Name : null, p.InsurancePlan, p.Status,
            LastVisit = db.Appointments.Where(a => a.PatientId == p.Id && a.Status == AppointmentStatus.Attended).Max(a => (DateTime?)a.StartUtc),
            Next = db.Appointments.Where(a => a.PatientId == p.Id && a.StartUtc >= now
                                              && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
                .Min(a => (DateTime?)a.StartUtc),
        });
        var page = await projected.ToPagedAsync(q, ct);
        return new PagedResult<PatientListItemDto>(page.Items.Select(p => new PatientListItemDto(p.Id, p.FirstName, p.LastName, p.DocumentNumber,
            Age(p.BirthDate, today), p.Phone, p.Insurance, p.InsurancePlan, p.Status, p.LastVisit, p.Next)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    internal static IQueryable<Patient> ApplySearch(IQueryable<Patient> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        var digits = new string(term.Where(char.IsDigit).ToArray());
        if (digits.Length >= 3 && digits.Length >= term.Count(char.IsLetterOrDigit) - 1)
        {
            // Búsqueda numérica: DNI o teléfono.
            var doc = Patient.NormalizeDocument(term);
            return query.Where(p => p.DocumentNumber.StartsWith(doc) || (p.PhoneNormalized != null && p.PhoneNormalized.Contains(digits)));
        }
        foreach (var token in Patient.NormalizeText(term).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5))
        {
            var t = token;
            query = query.Where(p => p.SearchName.Contains(t));
        }
        return query;
    }

    public async Task<PatientDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsRead);
        var p = await LoadAsync(id, ct);
        return await ToDtoAsync(p, ct);
    }

    public async Task<PatientSummaryDto> GetSummaryAsync(Guid id, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsRead);
        var p = await LoadAsync(id, ct);
        var dto = await ToDtoAsync(p, ct);
        var now = clock.UtcNow;

        var upcoming = await db.Appointments.AsNoTracking()
            .Where(a => a.PatientId == id && a.StartUtc >= now && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.StartUtc).Take(5)
            .Select(a => new { a.Id, a.StartUtc, a.EndUtc, a.ProfessionalUserId, a.Reason, a.Status }).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, upcoming.Select(a => a.ProfessionalUserId), ct);
        var stats = await db.Appointments.Where(a => a.PatientId == id)
            .GroupBy(_ => 1).Select(g => new
            {
                Total = g.Count(),
                NoShows = g.Count(a => a.Status == AppointmentStatus.NoShow),
                Last = g.Where(a => a.Status == AppointmentStatus.Attended).Max(a => (DateTime?)a.StartUtc),
            }).FirstOrDefaultAsync(ct);

        AccountSummaryDto? account = null;
        if (tenant.Has(Permissions.FinanceRead))
        {
            var movements = await db.AccountMovements.Where(m => m.PatientId == id).ToListAsync(ct);
            var approved = movements.Where(m => m.Type == MovementType.BudgetCharge).Sum(m => m.Amount)
                           - movements.Where(m => m.Type == MovementType.BudgetChargeReversal).Sum(m => m.Amount);
            var paid = movements.Where(m => m.Type == MovementType.Payment).Sum(m => m.Amount)
                       - movements.Where(m => m.Type == MovementType.PaymentReversal).Sum(m => m.Amount);
            account = new AccountSummaryDto(approved, paid, AccountCalculator.Balance(movements));
        }

        TreatmentSummaryDto? treatment = null;
        if (tenant.Has(Permissions.TreatmentsRead))
        {
            var plan = await db.TreatmentPlans.AsNoTracking().Include(t => t.Items)
                .Where(t => t.PatientId == id && (t.Status == TreatmentStatus.InProgress || t.Status == TreatmentStatus.Pending))
                .OrderByDescending(t => t.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (plan is not null)
                treatment = new TreatmentSummaryDto(plan.Id, plan.Title, plan.Status, plan.ProgressPercent,
                    plan.Items.Count(i => i.Status == TreatmentStatus.Completed), plan.Items.Count(i => i.Status != TreatmentStatus.Cancelled), plan.StartDate);
        }

        LastEvolutionDto? evolution = null;
        if (tenant.Has(Permissions.ClinicalRead))
        {
            var e = await db.ClinicalEntries.AsNoTracking().Where(x => x.PatientId == id && !x.IsVoided)
                .OrderByDescending(x => x.OccurredAtUtc).FirstOrDefaultAsync(ct);
            if (e is not null)
            {
                var n = await UserNames.LoadAsync(db, [e.ProfessionalUserId], ct);
                evolution = new LastEvolutionDto(e.Id, e.OccurredAtUtc, n.GetValueOrDefault(e.ProfessionalUserId, "?"), e.Title, e.Description);
            }
        }

        var openLab = tenant.Has(Permissions.LabRead)
            ? await db.LabWorks.CountAsync(l => l.PatientId == id && l.Status != LabWorkStatus.Placed && l.Status != LabWorkStatus.Cancelled, ct)
            : 0;
        var docs = tenant.Has(Permissions.DocumentsRead)
            ? await db.Documents.CountAsync(d => d.PatientId == id && !d.IsDeleted, ct)
            : 0;

        return new PatientSummaryDto(dto,
            upcoming.Select(a => new UpcomingAppointmentDto(a.Id, a.StartUtc, a.EndUtc, names.GetValueOrDefault(a.ProfessionalUserId, "?"), a.Reason, a.Status)).ToList(),
            stats?.Last, stats?.Total ?? 0, stats?.NoShows ?? 0, account, treatment, evolution, openLab, docs);
    }

    public async Task<PatientDto> CreateAsync(CreatePatientRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsWrite);
        var doc = Patient.NormalizeDocument(r.Patient.DocumentNumber);
        if (await db.Patients.AnyAsync(p => p.DocumentNumber == doc, ct))
            throw new ConflictException("Ya existe un paciente con ese DNI en el consultorio.", "duplicate_patient");

        var p = new Patient(tenant.ConsultorioId, r.Patient.FirstName, r.Patient.LastName, r.Patient.DocumentNumber);
        await ApplyAsync(p, r.Patient, ct);
        db.Patients.Add(p);

        var history = new MedicalHistory(tenant.ConsultorioId, p.Id);
        if (r.MedicalHistory is not null)
        {
            // Los antecedentes sólo se cargan con permiso explícito; no se ignoran silenciosamente.
            if (!tenant.Has(Permissions.PatientsMedicalWrite))
                throw new ForbiddenAccessException("No tiene permiso para registrar antecedentes médicos.");
            ApplyHistory(history, r.MedicalHistory);
        }
        db.MedicalHistories.Add(history);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(p, ct);
    }

    public async Task<PatientDto> UpdateAsync(Guid id, SavePatientRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsWrite);
        var p = await LoadAsync(id, ct);
        var doc = Patient.NormalizeDocument(r.DocumentNumber);
        if (doc != p.DocumentNumber && await db.Patients.AnyAsync(x => x.Id != id && x.DocumentNumber == doc, ct))
            throw new ConflictException("Ya existe un paciente con ese DNI en el consultorio.", "duplicate_patient");
        p.SetIdentity(r.FirstName, r.LastName, r.DocumentNumber);
        await ApplyAsync(p, r, ct);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(p, ct);
    }

    public async Task<PatientDto> ChangeStatusAsync(Guid id, PatientStatus status, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsWrite);
        var p = await LoadAsync(id, ct);
        p.SetStatus(status);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(p, ct);
    }

    private async Task ApplyAsync(Patient p, SavePatientRequest r, CancellationToken ct)
    {
        if (r.HealthInsuranceId is { } hid && !await db.HealthInsurances.AnyAsync(h => h.Id == hid, ct))
            throw new RequestValidationException("healthInsuranceId", "La obra social no existe en el consultorio.");
        p.SetPersonalData(r.BirthDate, r.Sex, r.Occupation, await clock.TodayAsync(ct));
        p.SetContact(r.Phone, r.Email, r.AddressLine, r.City, r.Province, r.PostalCode);
        p.SetEmergencyContact(r.EmergencyContactName, r.EmergencyContactPhone, r.EmergencyContactRelationship);
        p.SetCoverage(r.HealthInsuranceId, r.InsurancePlan, r.AffiliateNumber);
        p.SetAdministrativeNotes(r.AdministrativeNotes);
    }

    // ---------------- Antecedentes médicos (acceso restringido) ----------------

    public async Task<MedicalHistoryDto> GetMedicalHistoryAsync(Guid patientId, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsMedicalRead);
        await EnsurePatientAsync(patientId, ct);
        var h = await db.MedicalHistories.AsNoTracking().FirstOrDefaultAsync(x => x.PatientId == patientId, ct)
                ?? new MedicalHistory(tenant.ConsultorioId, patientId);
        return await ToHistoryDtoAsync(h, ct);
    }

    public async Task<MedicalHistoryDto> UpdateMedicalHistoryAsync(Guid patientId, MedicalHistoryRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsMedicalWrite);
        await EnsurePatientAsync(patientId, ct);
        var h = await db.MedicalHistories.FirstOrDefaultAsync(x => x.PatientId == patientId, ct);
        if (h is null) { h = new MedicalHistory(tenant.ConsultorioId, patientId); db.MedicalHistories.Add(h); }
        ApplyHistory(h, r);
        await db.SaveChangesAsync(ct);
        return await ToHistoryDtoAsync(h, ct);
    }

    private static void ApplyHistory(MedicalHistory h, MedicalHistoryRequest r)
    {
        h.Diabetes = r.Diabetes; h.Hypertension = r.Hypertension; h.HeartDisease = r.HeartDisease; h.Anticoagulated = r.Anticoagulated;
        h.Pregnant = r.Pregnant; h.Asthma = r.Asthma; h.Hepatitis = r.Hepatitis; h.Smoker = r.Smoker;
        h.Allergies = r.Allergies; h.CurrentMedication = r.CurrentMedication; h.OtherConditions = r.OtherConditions;
        h.Validate();
    }

    private async Task<MedicalHistoryDto> ToHistoryDtoAsync(MedicalHistory h, CancellationToken ct)
    {
        var by = h.UpdatedByUserId ?? h.CreatedByUserId;
        var names = by is null ? new Dictionary<Guid, string>() : await UserNames.LoadAsync(db, [by.Value], ct);
        return new MedicalHistoryDto(h.Diabetes, h.Hypertension, h.HeartDisease, h.Anticoagulated, h.Pregnant, h.Asthma, h.Hepatitis, h.Smoker,
            h.Allergies, h.CurrentMedication, h.OtherConditions, h.UpdatedAtUtc ?? (h.CreatedAtUtc == default ? null : h.CreatedAtUtc),
            by is null ? null : names.GetValueOrDefault(by.Value));
    }

    // ---------------- Alertas ----------------

    public async Task<IReadOnlyList<AlertDto>> GetAlertsAsync(Guid patientId, CancellationToken ct)
    {
        await EnsurePatientAsync(patientId, ct);
        return await BuildAlertsAsync(patientId, ct);
    }

    /// <summary>
    /// Alertas visibles según permisos: con antecedentes médicos se ven todas y su detalle; con sólo
    /// <c>patients.alerts.read</c> (recepción) se ven únicamente títulos de alertas relevantes para la atención.
    /// </summary>
    private async Task<IReadOnlyList<AlertDto>> BuildAlertsAsync(Guid patientId, CancellationToken ct)
    {
        var full = tenant.Has(Permissions.PatientsMedicalRead);
        if (!full && !tenant.Has(Permissions.PatientsAlertsRead)) return [];

        var manual = await db.PatientAlerts.AsNoTracking().Where(a => a.PatientId == patientId && a.IsActive && (full || a.VisibleToReception))
            .OrderByDescending(a => a.Severity).ToListAsync(ct);
        var history = await db.MedicalHistories.AsNoTracking().FirstOrDefaultAsync(h => h.PatientId == patientId, ct);
        var list = manual.Select(a => new AlertDto(a.Id, a.Title, full ? a.Details : null, a.Severity, false, a.VisibleToReception)).ToList();
        if (history is not null)
            list.AddRange(history.DerivedAlerts().Select(d => new AlertDto(null, d.Title, null, d.Severity, true, true)));
        return list.OrderByDescending(a => a.Severity).ToList();
    }

    public async Task<AlertDto> AddAlertAsync(Guid patientId, SaveAlertRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsMedicalWrite);
        await EnsurePatientAsync(patientId, ct);
        var a = new PatientAlert(tenant.ConsultorioId, patientId, r.Title, r.Details, r.Severity, r.VisibleToReception);
        db.PatientAlerts.Add(a);
        await db.SaveChangesAsync(ct);
        return new AlertDto(a.Id, a.Title, a.Details, a.Severity, false, a.VisibleToReception);
    }

    public async Task<AlertDto> UpdateAlertAsync(Guid patientId, Guid alertId, SaveAlertRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsMedicalWrite);
        var a = await db.PatientAlerts.FirstOrDefaultAsync(x => x.Id == alertId && x.PatientId == patientId, ct) ?? throw new NotFoundException();
        a.Update(r.Title, r.Details, r.Severity, r.VisibleToReception);
        await db.SaveChangesAsync(ct);
        return new AlertDto(a.Id, a.Title, a.Details, a.Severity, false, a.VisibleToReception);
    }

    public async Task DeactivateAlertAsync(Guid patientId, Guid alertId, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsMedicalWrite);
        var a = await db.PatientAlerts.FirstOrDefaultAsync(x => x.Id == alertId && x.PatientId == patientId, ct) ?? throw new NotFoundException();
        a.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Historial administrativo ----------------

    public async Task<IReadOnlyList<PatientHistoryItemDto>> GetAdministrativeHistoryAsync(Guid patientId, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsRead);
        await EnsurePatientAsync(patientId, ct);
        var items = new List<(DateTime At, string Kind, string Desc, Guid? User)>();

        if (tenant.Has(Permissions.AppointmentsRead))
        {
            var all = tenant.Has(Permissions.AgendaAllProfessionals);
            var uid = tenant.UserId;
            var appts = await db.AppointmentHistories.AsNoTracking()
                .Where(h => db.Appointments.Any(a => a.Id == h.AppointmentId && a.PatientId == patientId && (all || a.ProfessionalUserId == uid)))
                .OrderByDescending(h => h.ChangedAtUtc).Take(200).ToListAsync(ct);
            items.AddRange(appts.Select(h => (h.ChangedAtUtc, "appointment", $"{h.Action}{(h.ToStatus is null ? "" : $" → {h.ToStatus}")}{(h.Details is null ? "" : $": {h.Details}")}", h.ChangedByUserId)));
        }
        if (tenant.Has(Permissions.FinanceRead))
        {
            var pays = await db.Payments.AsNoTracking().Where(p => p.PatientId == patientId).ToListAsync(ct);
            items.AddRange(pays.Select(p => (p.PaidAtUtc, "payment", $"Pago #{p.ReceiptNumber} {p.Amount:N2} ({p.Method})", (Guid?)p.ReceivedByUserId)));
            items.AddRange(pays.Where(p => p.VoidedAtUtc != null).Select(p => (p.VoidedAtUtc!.Value, "payment_void", $"Anulación pago #{p.ReceiptNumber}: {p.VoidReason}", p.VoidedByUserId)));
        }
        if (tenant.Has(Permissions.BudgetsRead))
        {
            var changes = await db.BudgetStatusChanges.AsNoTracking().Where(c => db.Budgets.Any(b => b.Id == c.BudgetId && b.PatientId == patientId)).ToListAsync(ct);
            var numbers = await db.Budgets.Where(b => b.PatientId == patientId).ToDictionaryAsync(b => b.Id, b => b.Number, ct);
            items.AddRange(changes.Select(c => (c.ChangedAtUtc, "budget", $"Presupuesto #{numbers.GetValueOrDefault(c.BudgetId)}: {c.FromStatus} → {c.ToStatus}", c.ChangedByUserId)));
        }
        if (tenant.Has(Permissions.DocumentsRead))
        {
            var clinical = tenant.Has(Permissions.DocumentsClinicalRead);
            var docs = await db.Documents.AsNoTracking().Where(d => d.PatientId == patientId).ToListAsync(ct);
            items.AddRange(docs.Where(d => clinical || !d.IsClinical).Select(d => (d.UploadedAtUtc, "document", $"Documento cargado: {d.FileName}", (Guid?)d.UploadedByUserId)));
        }

        var names = await UserNames.LoadAsync(db, items.Where(i => i.User != null).Select(i => i.User!.Value), ct);
        return items.OrderByDescending(i => i.At)
            .Select(i => new PatientHistoryItemDto(i.At, i.Kind, i.Desc, i.User is null ? null : names.GetValueOrDefault(i.User.Value))).ToList();
    }

    // ---------------- Obras sociales ----------------

    public async Task<IReadOnlyList<HealthInsuranceDto>> GetInsurancesAsync(bool includeInactive, CancellationToken ct)
    {
        tenant.Require(Permissions.PatientsRead);
        return await db.HealthInsurances.AsNoTracking().Where(h => includeInactive || h.IsActive).OrderBy(h => h.Name)
            .Select(h => new HealthInsuranceDto(h.Id, h.Name, h.Code, h.Phone, h.Notes, h.IsActive)).ToListAsync(ct);
    }

    public async Task<HealthInsuranceDto> CreateInsuranceAsync(SaveHealthInsuranceRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.InsurancesManage);
        if (await db.HealthInsurances.AnyAsync(h => h.Name == r.Name.Trim(), ct)) throw new ConflictException("Ya existe una obra social con ese nombre.");
        var h = new HealthInsurance(tenant.ConsultorioId, r.Name, r.Code);
        h.Update(r.Name, r.Code, r.Phone, r.Notes);
        h.SetActive(r.IsActive);
        db.HealthInsurances.Add(h);
        await db.SaveChangesAsync(ct);
        return new HealthInsuranceDto(h.Id, h.Name, h.Code, h.Phone, h.Notes, h.IsActive);
    }

    public async Task<HealthInsuranceDto> UpdateInsuranceAsync(Guid id, SaveHealthInsuranceRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.InsurancesManage);
        var h = await db.HealthInsurances.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
        if (await db.HealthInsurances.AnyAsync(x => x.Id != id && x.Name == r.Name.Trim(), ct)) throw new ConflictException("Ya existe una obra social con ese nombre.");
        h.Update(r.Name, r.Code, r.Phone, r.Notes);
        h.SetActive(r.IsActive);
        await db.SaveChangesAsync(ct);
        return new HealthInsuranceDto(h.Id, h.Name, h.Code, h.Phone, h.Notes, h.IsActive);
    }

    // ---------------- Utilidades ----------------

    private async Task<Patient> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Patients.Include(p => p.HealthInsurance).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("El paciente no existe.");

    private async Task EnsurePatientAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("El paciente no existe.");
    }

    private async Task<PatientDto> ToDtoAsync(Patient p, CancellationToken ct)
    {
        var today = await clock.TodayAsync(ct);
        var insurance = p.HealthInsurance?.Name ?? (p.HealthInsuranceId is null ? null
            : await db.HealthInsurances.Where(h => h.Id == p.HealthInsuranceId).Select(h => h.Name).FirstOrDefaultAsync(ct));
        return new PatientDto(p.Id, p.FirstName, p.LastName, p.DocumentNumber, p.BirthDate, p.AgeAt(today), p.Sex, p.Occupation, p.Phone, p.Email,
            p.AddressLine, p.City, p.Province, p.PostalCode, p.EmergencyContactName, p.EmergencyContactPhone, p.EmergencyContactRelationship,
            p.HealthInsuranceId, insurance, p.InsurancePlan, p.AffiliateNumber, p.AdministrativeNotes, p.Status, p.CreatedAtUtc,
            await BuildAlertsAsync(p.Id, ct));
    }

    private static int? Age(DateOnly? birth, DateOnly today)
    {
        if (birth is not { } b) return null;
        var age = today.Year - b.Year;
        if (today < b.AddYears(age)) age--;
        return age;
    }
}

internal static class UserNames
{
    public static async Task<Dictionary<Guid, string>> LoadAsync(IApplicationDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return [];
        var users = await db.Users.AsNoTracking().Where(u => list.Contains(u.Id))
            .Select(u => new { u.Id, u.Title, u.FirstName, u.LastName }).ToListAsync(ct);
        return users.ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.Title) ? $"{u.FirstName} {u.LastName}" : $"{u.Title} {u.FirstName} {u.LastName}");
    }
}
