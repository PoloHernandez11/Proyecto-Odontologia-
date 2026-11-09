using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Patients;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.Application.Features.Documents;

public sealed class DocumentOptions
{
    public const string Section = "Documents";
    public long MaxSizeBytes { get; set; } = 15 * 1024 * 1024;
}

public sealed record UploadDocumentCommand(Guid PatientId, DocumentCategory Category, string? Description, Guid? ClinicalEntryId,
    Guid? TreatmentPlanId, string FileName, Stream Content, long Length);

public sealed record DocumentDto(Guid Id, Guid PatientId, DocumentCategory Category, string FileName, string ContentType, long SizeBytes,
    string? Description, bool IsClinical, Guid? ClinicalEntryId, Guid? TreatmentPlanId, DateTime UploadedAtUtc, string UploadedBy);

public sealed record DocumentDownload(Stream Content, string ContentType, string FileName);

public sealed record DeleteDocumentRequest(string Reason);

public sealed record CreateConsentRequest(string Title, string Content, Guid? TreatmentPlanId);

public sealed record RecordConsentAcceptanceRequest(string AcceptedByName, string? AcceptedByDocument, string? Relationship);

public sealed record ConsentDecisionRequest(string? Notes);

public sealed record AttachSignedConsentRequest(Guid DocumentId, string SignedByName);

/// <summary>
/// <see cref="SignatureType"/> describe cómo se registró la conformidad. Odontia no emite firmas digitales
/// certificadas: "SystemAcceptance" es una constancia interna, no una firma con validez de firma digital.
/// </summary>
public sealed record ConsentDto(Guid Id, Guid PatientId, string Title, string Content, Guid? TreatmentPlanId, ConsentStatus Status,
    string SignatureType, string? AcceptedByName, string? AcceptedByDocument, string? AcceptedByRelationship, DateTime? DecisionAtUtc,
    string? DecisionRecordedBy, Guid? SignedDocumentId, string? Notes, string Responsible, DateTime CreatedAtUtc);

/// <summary>Validación de archivos por firma binaria ("magic numbers"), no por la extensión declarada.</summary>
public static class FileSignatureValidator
{
    private static readonly (string Ext, string Mime, Func<byte[], bool> Match)[] Allowed =
    [
        (".pdf", "application/pdf", b => b.Length >= 5 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46 && b[4] == 0x2D),
        (".jpg", "image/jpeg", b => b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF),
        (".jpeg", "image/jpeg", b => b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF),
        (".png", "image/png", b => b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A),
        (".dcm", "application/dicom", b => b.Length >= 132 && b[128] == 0x44 && b[129] == 0x49 && b[130] == 0x43 && b[131] == 0x4D),
    ];

    public static IReadOnlyList<string> AllowedExtensions => Allowed.Select(a => a.Ext).Distinct().ToList();

    /// <summary>Devuelve (extensión normalizada, MIME real) o lanza si el contenido no coincide con un tipo permitido.</summary>
    public static (string Extension, string ContentType) Validate(string fileName, byte[] header)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var candidates = Allowed.Where(a => a.Ext == ext).ToList();
        if (candidates.Count == 0)
            throw new RequestValidationException("file", $"Extensión no permitida. Permitidas: {string.Join(", ", AllowedExtensions)}.");
        var match = candidates.FirstOrDefault(c => c.Match(header));
        if (match.Ext is null)
            throw new RequestValidationException("file", "El contenido del archivo no corresponde a su extensión.");
        return (ext == ".jpeg" ? ".jpg" : ext, match.Mime);
    }

    /// <summary>Nombre seguro: sin rutas, sin caracteres de control/reservados, longitud acotada.</summary>
    public static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/').Split('/').Last());
        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '|', '?', '*', ';']).ToHashSet();
        var clean = new string(name.Where(c => !char.IsControl(c) && !invalid.Contains(c)).ToArray()).Trim().TrimStart('.');
        if (clean.Length == 0) clean = "documento";
        if (clean.Length > 150)
        {
            var ext = Path.GetExtension(clean);
            clean = clean[..(150 - ext.Length)] + ext;
        }
        return clean;
    }
}

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> GetByPatientAsync(Guid patientId, DocumentCategory? category, CancellationToken ct);
    Task<DocumentDto> UploadAsync(UploadDocumentCommand command, CancellationToken ct);
    Task<DocumentDownload> DownloadAsync(Guid documentId, CancellationToken ct);
    Task DeleteAsync(Guid documentId, DeleteDocumentRequest request, CancellationToken ct);
    Task<IReadOnlyList<ConsentDto>> GetConsentsAsync(Guid patientId, CancellationToken ct);
    Task<ConsentDto> CreateConsentAsync(Guid patientId, CreateConsentRequest request, CancellationToken ct);
    Task<ConsentDto> RecordAcceptanceAsync(Guid patientId, Guid consentId, RecordConsentAcceptanceRequest request, CancellationToken ct);
    Task<ConsentDto> AttachSignedAsync(Guid patientId, Guid consentId, AttachSignedConsentRequest request, CancellationToken ct);
    Task<ConsentDto> RejectAsync(Guid patientId, Guid consentId, ConsentDecisionRequest request, CancellationToken ct);
    Task<ConsentDto> RevokeAsync(Guid patientId, Guid consentId, ConsentDecisionRequest request, CancellationToken ct);
}

internal sealed class DocumentService(IApplicationDbContext db, ITenantContext tenant, IConsultorioClock clock, IFileStorage storage,
    IAuditWriter audit, IOptions<DocumentOptions> options) : IDocumentService
{
    public async Task<IReadOnlyList<DocumentDto>> GetByPatientAsync(Guid patientId, DocumentCategory? category, CancellationToken ct)
    {
        tenant.Require(Permissions.DocumentsRead);
        await EnsurePatientAsync(patientId, ct);
        var docs = await db.Documents.AsNoTracking().Where(d => d.PatientId == patientId && !d.IsDeleted && (category == null || d.Category == category))
            .OrderByDescending(d => d.UploadedAtUtc).ToListAsync(ct);
        var clinical = tenant.Has(Permissions.DocumentsClinicalRead);
        docs = docs.Where(d => clinical || !d.IsClinical).ToList();
        var names = await UserNames.LoadAsync(db, docs.Select(d => d.UploadedByUserId), ct);
        return docs.Select(d => ToDto(d, names)).ToList();
    }

    public async Task<DocumentDto> UploadAsync(UploadDocumentCommand c, CancellationToken ct)
    {
        tenant.Require(Permissions.DocumentsWrite);
        await EnsurePatientAsync(c.PatientId, ct);
        var isClinical = StoredDocument.IsClinicalCategory(c.Category) || c.ClinicalEntryId is not null || c.TreatmentPlanId is not null;
        if (isClinical) tenant.Require(Permissions.DocumentsClinicalRead);
        if (c.ClinicalEntryId is { } eid)
        {
            tenant.Require(Permissions.ClinicalWrite);
            if (!await db.ClinicalEntries.AnyAsync(e => e.Id == eid && e.PatientId == c.PatientId, ct))
                throw new RequestValidationException("clinicalEntryId", "El registro clínico no corresponde al paciente.");
        }
        if (c.TreatmentPlanId is { } tid && !await db.TreatmentPlans.AnyAsync(t => t.Id == tid && t.PatientId == c.PatientId, ct))
            throw new RequestValidationException("treatmentPlanId", "El plan no corresponde al paciente.");

        var max = options.Value.MaxSizeBytes;
        if (c.Length <= 0) throw new RequestValidationException("file", "El archivo está vacío.");
        if (c.Length > max) throw new RequestValidationException("file", $"El archivo supera el tamaño máximo de {max / (1024 * 1024)} MB.");

        // Se lee con límite estricto (no se confía en el Content-Length declarado).
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await c.Content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > max) throw new RequestValidationException("file", "El archivo supera el tamaño máximo permitido.");
            buffer.Write(chunk, 0, read);
        }
        var header = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 256)).ToArray();
        var safeName = FileSignatureValidator.SafeFileName(c.FileName);
        var (ext, mime) = FileSignatureValidator.Validate(safeName, header);
        buffer.Position = 0;

        var stored = await storage.SaveAsync(tenant.ConsultorioId, buffer, ext, ct);
        var doc = new StoredDocument(tenant.ConsultorioId, c.PatientId, c.Category, safeName, stored.StorageKey, mime, stored.SizeBytes, stored.Sha256,
            c.Description, c.ClinicalEntryId, c.TreatmentPlanId, tenant.UserId, clock.UtcNow);
        db.Documents.Add(doc);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
        return ToDto(doc, await UserNames.LoadAsync(db, [tenant.UserId], ct));
    }

    public async Task<DocumentDownload> DownloadAsync(Guid documentId, CancellationToken ct)
    {
        tenant.Require(Permissions.DocumentsRead);
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId && !x.IsDeleted, ct) ?? throw new NotFoundException("El documento no existe.");
        if (d.IsClinical && !tenant.Has(Permissions.DocumentsClinicalRead))
        {
            audit.Write(AuditAction.AccessDenied, nameof(StoredDocument), d.Id.ToString(), "Descarga de documento clínico sin permiso");
            throw new ForbiddenAccessException("No tiene permiso para acceder a documentos clínicos.");
        }
        var stream = await storage.OpenReadAsync(d.StorageKey, ct);
        audit.Write(AuditAction.DocumentDownloaded, nameof(StoredDocument), d.Id.ToString());
        return new DocumentDownload(stream, d.ContentType, d.FileName);
    }

    public async Task DeleteAsync(Guid documentId, DeleteDocumentRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.DocumentsDelete);
        var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == documentId && !x.IsDeleted, ct) ?? throw new NotFoundException("El documento no existe.");
        if (d.IsClinical) tenant.Require(Permissions.DocumentsClinicalRead);
        if (await db.Consents.AnyAsync(c => c.SignedDocumentId == d.Id && c.Status == ConsentStatus.SignedOnPaperAttached, ct))
            throw new BusinessRuleException("El documento respalda un consentimiento firmado y no puede darse de baja.", "document_in_use");
        // Baja lógica: el archivo se conserva según la política de retención documentada.
        d.SoftDelete(r.Reason, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Consentimientos ----------------

    public async Task<IReadOnlyList<ConsentDto>> GetConsentsAsync(Guid patientId, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsentsRead);
        await EnsurePatientAsync(patientId, ct);
        var list = await db.Consents.AsNoTracking().Where(c => c.PatientId == patientId).OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);
        return await ToDtosAsync(list, ct);
    }

    public async Task<ConsentDto> CreateConsentAsync(Guid patientId, CreateConsentRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsentsWrite);
        await EnsurePatientAsync(patientId, ct);
        if (r.TreatmentPlanId is { } tid && !await db.TreatmentPlans.AnyAsync(t => t.Id == tid && t.PatientId == patientId, ct))
            throw new RequestValidationException("treatmentPlanId", "El plan no corresponde al paciente.");
        var c = new Consent(tenant.ConsultorioId, patientId, r.Title, r.Content, r.TreatmentPlanId, tenant.UserId);
        db.Consents.Add(c);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([c], ct))[0];
    }

    public async Task<ConsentDto> RecordAcceptanceAsync(Guid patientId, Guid consentId, RecordConsentAcceptanceRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsentsWrite);
        var c = await LoadConsentAsync(patientId, consentId, ct);
        c.RecordSystemAcceptance(r.AcceptedByName, r.AcceptedByDocument, r.Relationship, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([c], ct))[0];
    }

    public async Task<ConsentDto> AttachSignedAsync(Guid patientId, Guid consentId, AttachSignedConsentRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsentsWrite);
        var c = await LoadConsentAsync(patientId, consentId, ct);
        if (!await db.Documents.AnyAsync(d => d.Id == r.DocumentId && d.PatientId == patientId && !d.IsDeleted && d.Category == DocumentCategory.ConsentForm, ct))
            throw new RequestValidationException("documentId", "El documento no existe, no es del paciente o no es un consentimiento.");
        c.AttachSignedDocument(r.DocumentId, r.SignedByName, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([c], ct))[0];
    }

    public async Task<ConsentDto> RejectAsync(Guid patientId, Guid consentId, ConsentDecisionRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsentsWrite);
        var c = await LoadConsentAsync(patientId, consentId, ct);
        c.Reject(r.Notes, tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([c], ct))[0];
    }

    public async Task<ConsentDto> RevokeAsync(Guid patientId, Guid consentId, ConsentDecisionRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsentsWrite);
        var c = await LoadConsentAsync(patientId, consentId, ct);
        c.Revoke(r.Notes ?? "", tenant.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([c], ct))[0];
    }

    private async Task<Consent> LoadConsentAsync(Guid patientId, Guid consentId, CancellationToken ct) =>
        await db.Consents.FirstOrDefaultAsync(c => c.Id == consentId && c.PatientId == patientId, ct) ?? throw new NotFoundException("El consentimiento no existe.");

    private async Task EnsurePatientAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("El paciente no existe.");
    }

    private async Task<List<ConsentDto>> ToDtosAsync(IReadOnlyList<Consent> list, CancellationToken ct)
    {
        var ids = list.SelectMany(c => new[] { c.ResponsibleUserId, c.DecisionRecordedByUserId ?? Guid.Empty }).Where(i => i != Guid.Empty);
        var names = await UserNames.LoadAsync(db, ids, ct);
        return list.Select(c => new ConsentDto(c.Id, c.PatientId, c.Title, c.Content, c.TreatmentPlanId, c.Status,
            c.Status switch
            {
                ConsentStatus.AcceptedInSystem => "SystemAcceptance",
                ConsentStatus.SignedOnPaperAttached => "HandwrittenScanned",
                _ => "None",
            },
            c.AcceptedByName, c.AcceptedByDocument, c.AcceptedByRelationship, c.DecisionAtUtc,
            c.DecisionRecordedByUserId is null ? null : names.GetValueOrDefault(c.DecisionRecordedByUserId.Value), c.SignedDocumentId, c.Notes,
            names.GetValueOrDefault(c.ResponsibleUserId, "?"), c.CreatedAtUtc)).ToList();
    }

    private static DocumentDto ToDto(StoredDocument d, Dictionary<Guid, string> names) => new(d.Id, d.PatientId, d.Category, d.FileName, d.ContentType,
        d.SizeBytes, d.Description, d.IsClinical, d.ClinicalEntryId, d.TreatmentPlanId, d.UploadedAtUtc, names.GetValueOrDefault(d.UploadedByUserId, "?"));
}
