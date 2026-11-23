using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Dashboard;
using Odontia.Application.Features.Documents;
using Odontia.Application.Features.Finance;
using Odontia.Application.Features.Lab;
using Odontia.Application.Features.Notifications;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Documentos privados del paciente (nunca públicos) y consentimientos informados.</summary>
[Route("api/v{version:apiVersion}")]
public sealed class DocumentsController(IDocumentService service) : ApiControllerBase
{
    /// <summary>Documentos del paciente. Los clínicos sólo se listan con documents.clinical.read.</summary>
    [HttpGet("patients/{patientId:guid}/documents"), HasPermission(P.DocumentsRead)]
    public Task<IReadOnlyList<DocumentDto>> List(Guid patientId, [FromQuery] DocumentCategory? category, CancellationToken ct) =>
        service.GetByPatientAsync(patientId, category, ct);

    /// <summary>Carga un archivo (PDF, JPG, PNG, DICOM). Se valida extensión, firma binaria real y tamaño máximo.</summary>
    [HttpPost("patients/{patientId:guid}/documents"), HasPermission(P.DocumentsWrite)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload(Guid patientId, [FromForm] UploadDocumentForm form, CancellationToken ct)
    {
        if (form.File is null) throw new RequestValidationException("file", "Debe adjuntar un archivo.");
        await using var stream = form.File.OpenReadStream();
        var doc = await service.UploadAsync(new UploadDocumentCommand(patientId, form.Category, form.Description, form.ClinicalEntryId,
            form.TreatmentPlanId, form.File.FileName, stream, form.File.Length), ct);
        return StatusCode(StatusCodes.Status201Created, doc);
    }

    /// <summary>Descarga autorizada (se audita). Nunca se expone una URL pública.</summary>
    [HttpGet("documents/{id:guid}/download"), HasPermission(P.DocumentsRead)]
    [Produces("application/octet-stream", "application/pdf", "image/jpeg", "image/png", "application/dicom")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/octet-stream", "application/pdf", "image/jpeg", "image/png", "application/dicom")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var d = await service.DownloadAsync(id, ct);
        return File(d.Content, d.ContentType, d.FileName);
    }

    /// <summary>Baja lógica con motivo (el archivo se conserva según la política de retención).</summary>
    [HttpDelete("documents/{id:guid}"), HasPermission(P.DocumentsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string reason, CancellationToken ct)
    {
        await service.DeleteAsync(id, new DeleteDocumentRequest(reason), ct);
        return NoContent();
    }

    [HttpGet("patients/{patientId:guid}/consents"), HasPermission(P.ConsentsRead)]
    public Task<IReadOnlyList<ConsentDto>> Consents(Guid patientId, CancellationToken ct) => service.GetConsentsAsync(patientId, ct);

    [HttpPost("patients/{patientId:guid}/consents"), HasPermission(P.ConsentsWrite)]
    [ProducesResponseType<ConsentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateConsent(Guid patientId, CreateConsentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateConsentAsync(patientId, request, ct));

    /// <summary>Registra la aceptación en el sistema (constancia interna; NO es una firma digital certificada).</summary>
    [HttpPost("patients/{patientId:guid}/consents/{consentId:guid}/accept"), HasPermission(P.ConsentsWrite)]
    public Task<ConsentDto> Accept(Guid patientId, Guid consentId, RecordConsentAcceptanceRequest request, CancellationToken ct) =>
        service.RecordAcceptanceAsync(patientId, consentId, request, ct);

    /// <summary>Vincula el consentimiento firmado en papel (documento previamente cargado con categoría ConsentForm).</summary>
    [HttpPost("patients/{patientId:guid}/consents/{consentId:guid}/attach-signed"), HasPermission(P.ConsentsWrite)]
    public Task<ConsentDto> AttachSigned(Guid patientId, Guid consentId, AttachSignedConsentRequest request, CancellationToken ct) =>
        service.AttachSignedAsync(patientId, consentId, request, ct);

    [HttpPost("patients/{patientId:guid}/consents/{consentId:guid}/reject"), HasPermission(P.ConsentsWrite)]
    public Task<ConsentDto> Reject(Guid patientId, Guid consentId, ConsentDecisionRequest request, CancellationToken ct) =>
        service.RejectAsync(patientId, consentId, request, ct);

    [HttpPost("patients/{patientId:guid}/consents/{consentId:guid}/revoke"), HasPermission(P.ConsentsWrite)]
    public Task<ConsentDto> Revoke(Guid patientId, Guid consentId, ConsentDecisionRequest request, CancellationToken ct) =>
        service.RevokeAsync(patientId, consentId, request, ct);
}
