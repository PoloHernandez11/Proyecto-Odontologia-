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

/// <summary>Presupuestos: borrador, emisión, aprobación (genera cargo), rechazo, cancelación e historial.</summary>
[Route("api/v{version:apiVersion}")]
public sealed class BudgetsController(IBudgetService service) : ApiControllerBase
{
    [HttpGet("budgets"), HasPermission(P.BudgetsRead)]
    public Task<PagedResult<BudgetDto>> Search([FromQuery] BudgetQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Crea un presupuesto en borrador (líneas manuales o importadas del plan de tratamiento).</summary>
    [HttpPost("patients/{patientId:guid}/budgets"), HasPermission(P.BudgetsWrite)]
    [ProducesResponseType<BudgetDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid patientId, SaveBudgetRequest request, CancellationToken ct)
    {
        var b = await service.CreateAsync(patientId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = b.Id, version = "1" }, b);
    }

    [HttpGet("budgets/{id:guid}"), HasPermission(P.BudgetsRead)]
    public Task<BudgetDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Modifica un presupuesto en borrador (reemplaza las líneas).</summary>
    [HttpPut("budgets/{id:guid}"), HasPermission(P.BudgetsWrite)]
    public Task<BudgetDto> Update(Guid id, SaveBudgetRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPost("budgets/{id:guid}/issue"), HasPermission(P.BudgetsWrite)]
    public Task<BudgetDto> Issue(Guid id, CancellationToken ct) => service.IssueAsync(id, ct);

    [HttpPost("budgets/{id:guid}/approve"), HasPermission(P.BudgetsApprove)]
    public Task<BudgetDto> Approve(Guid id, CancellationToken ct) => service.ApproveAsync(id, ct);

    [HttpPost("budgets/{id:guid}/reject"), HasPermission(P.BudgetsApprove)]
    public Task<BudgetDto> Reject(Guid id, BudgetDecisionRequest request, CancellationToken ct) => service.RejectAsync(id, request, ct);

    [HttpPost("budgets/{id:guid}/cancel"), HasPermission(P.BudgetsApprove)]
    public Task<BudgetDto> Cancel(Guid id, BudgetDecisionRequest request, CancellationToken ct) => service.CancelAsync(id, request, ct);

    [HttpGet("budgets/{id:guid}/history"), HasPermission(P.BudgetsRead)]
    public Task<IReadOnlyList<BudgetStatusChangeDto>> History(Guid id, CancellationToken ct) => service.GetHistoryAsync(id, ct);
}

/// <summary>Pagos y cuenta corriente (saldos calculados en el servidor).</summary>
[Route("api/v{version:apiVersion}")]
public sealed class PaymentsController(IPaymentService service) : ApiControllerBase
{
    [HttpGet("payments"), HasPermission(P.FinanceRead)]
    public Task<PagedResult<PaymentDto>> Search([FromQuery] PaymentQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Registra un pago (opcionalmente imputado a un presupuesto aprobado, sin exceder su saldo).</summary>
    [HttpPost("payments"), HasPermission(P.PaymentsWrite)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterPaymentRequest request, CancellationToken ct)
    {
        var p = await service.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id, version = "1" }, p);
    }

    [HttpGet("payments/{id:guid}"), HasPermission(P.FinanceRead)]
    public Task<PaymentDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    /// <summary>Anulación controlada con motivo: genera contraasiento; el pago no se elimina.</summary>
    [HttpPost("payments/{id:guid}/void"), HasPermission(P.PaymentsVoid)]
    public Task<PaymentDto> Void(Guid id, VoidPaymentRequest request, CancellationToken ct) => service.VoidAsync(id, request, ct);

    /// <summary>Cuenta corriente del paciente con movimientos, saldo acumulado y saldos por presupuesto.</summary>
    [HttpGet("patients/{patientId:guid}/account"), HasPermission(P.FinanceRead)]
    public Task<AccountStatementDto> Account(Guid patientId, CancellationToken ct) => service.GetStatementAsync(patientId, ct);
}

/// <summary>Laboratorios externos.</summary>
[Route("api/v{version:apiVersion}/laboratories")]
public sealed class LaboratoriesController(ILabService service) : ApiControllerBase
{
    /// <summary>Requiere lab.read o laboratories.manage.</summary>
    [HttpGet]
    public Task<IReadOnlyList<LaboratoryDto>> List([FromQuery] bool includeInactive, CancellationToken ct) => service.GetLaboratoriesAsync(includeInactive, ct);

    [HttpPost, HasPermission(P.LaboratoriesManage)]
    [ProducesResponseType<LaboratoryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveLaboratoryRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateLaboratoryAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(P.LaboratoriesManage)]
    public Task<LaboratoryDto> Update(Guid id, SaveLaboratoryRequest request, CancellationToken ct) => service.UpdateLaboratoryAsync(id, request, ct);
}

/// <summary>Trabajos de laboratorio y seguimiento de entregas.</summary>
[Route("api/v{version:apiVersion}/lab-works")]
public sealed class LabWorksController(ILabService service) : ApiControllerBase
{
    /// <summary>Listado. view: open | overdue | this_week | closed.</summary>
    [HttpGet, HasPermission(P.LabRead)]
    public Task<PagedResult<LabWorkDto>> Search([FromQuery] LabWorkQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Tablero: conteos por estado, atrasados y entregas de la semana.</summary>
    [HttpGet("board"), HasPermission(P.LabRead)]
    public Task<LabBoardDto> Board(CancellationToken ct) => service.GetBoardAsync(ct);

    [HttpPost, HasPermission(P.LabWrite)]
    [ProducesResponseType<LabWorkDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveLabWorkRequest request, CancellationToken ct)
    {
        var w = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = w.Id, version = "1" }, w);
    }

    [HttpGet("{id:guid}"), HasPermission(P.LabRead)]
    public Task<LabWorkDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPut("{id:guid}"), HasPermission(P.LabWrite)]
    public Task<LabWorkDto> Update(Guid id, SaveLabWorkRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    /// <summary>Avance logístico (lab.track o lab.write). Colocado/Cancelado requieren lab.write.</summary>
    [HttpPost("{id:guid}/status")]
    public Task<LabWorkDto> ChangeStatus(Guid id, ChangeLabWorkStatusRequest request, CancellationToken ct) => service.ChangeStatusAsync(id, request, ct);

    [HttpGet("{id:guid}/history"), HasPermission(P.LabRead)]
    public Task<IReadOnlyList<LabWorkChangeDto>> History(Guid id, CancellationToken ct) => service.GetHistoryAsync(id, ct);
}

/// <summary>Formulario de carga de documento (multipart/form-data).</summary>
public sealed class UploadDocumentForm
{
    public IFormFile File { get; set; } = default!;
    public DocumentCategory Category { get; set; }
    public string? Description { get; set; }
    public Guid? ClinicalEntryId { get; set; }
    public Guid? TreatmentPlanId { get; set; }
}

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
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
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

/// <summary>Notificaciones internas del usuario y mensajes salientes (recordatorios).</summary>
[Route("api/v{version:apiVersion}")]
public sealed class NotificationsController(INotificationService service) : ApiControllerBase
{
    [HttpGet("notifications"), RequireConsultorio]
    public Task<PagedResult<NotificationDto>> Mine([FromQuery] NotificationQuery query, CancellationToken ct) => service.GetMineAsync(query, ct);

    [HttpGet("notifications/unread-count"), RequireConsultorio]
    public Task<UnreadCountDto> Unread(CancellationToken ct) => service.GetUnreadCountAsync(ct);

    [HttpPost("notifications/{id:guid}/read"), RequireConsultorio]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("notifications/read-all"), RequireConsultorio]
    public async Task<object> MarkAllRead(CancellationToken ct) => new { updated = await service.MarkAllReadAsync(ct) };

    /// <summary>Registro de mensajes salientes y su estado real (Sent sólo con confirmación del proveedor).</summary>
    [HttpGet("messages"), HasPermission(P.MessagingSend)]
    public Task<PagedResult<OutboundMessageDto>> Outbound([FromQuery] OutboundMessageQuery query, CancellationToken ct) => service.GetOutboundAsync(query, ct);

    /// <summary>Envía recordatorios para los turnos pendientes/confirmados de una fecha.</summary>
    [HttpPost("messages/reminders"), HasPermission(P.MessagingSend)]
    public Task<SendRemindersResultDto> SendReminders(SendRemindersRequest request, CancellationToken ct) => service.SendDayRemindersAsync(request, ct);
}

/// <summary>Dashboards por perfil (respetan permisos y consultorio activo).</summary>
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController(IDashboardService service) : ApiControllerBase
{
    /// <summary>Recepción: turnos del día, confirmaciones, sala de espera, tareas y entregas de laboratorio. Sin información clínica.</summary>
    [HttpGet("reception"), HasPermission(P.DashboardReception)]
    public Task<ReceptionDashboardDto> Reception(CancellationToken ct) => service.GetReceptionAsync(ct);

    /// <summary>Odontólogo: pacientes del día, próximo turno, tratamientos, laboratorio, alertas y actividad.</summary>
    [HttpGet("clinical"), HasPermission(P.DashboardClinical)]
    public Task<ClinicalDashboardDto> Clinical(CancellationToken ct) => service.GetClinicalAsync(ct);
}
