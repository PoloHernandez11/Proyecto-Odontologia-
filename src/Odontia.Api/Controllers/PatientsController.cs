using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Notifications;
using Odontia.Application.Features.Patients;
using Odontia.Application.Features.Scheduling;
using Odontia.Application.Features.Tasks;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Pacientes: datos personales/administrativos, antecedentes médicos (restringidos) y alertas.</summary>
[Route("api/v{version:apiVersion}/patients")]
public sealed class PatientsController(IPatientService service) : ApiControllerBase
{
    /// <summary>Búsqueda paginada por nombre, apellido, DNI o teléfono, con filtros. Orden: name, document, created, status, birthdate.</summary>
    [HttpGet, HasPermission(P.PatientsRead)]
    public Task<PagedResult<PatientListItemDto>> Search([FromQuery] PatientQuery query, CancellationToken ct) => service.SearchAsync(query, ct);

    /// <summary>Alta de paciente (DNI único por consultorio). Antecedentes opcionales: requieren patients.medical.write.</summary>
    [HttpPost, HasPermission(P.PatientsWrite)]
    [ProducesResponseType<PatientDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreatePatientRequest request, CancellationToken ct)
    {
        var p = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id, version = "1" }, p);
    }

    [HttpGet("{id:guid}"), HasPermission(P.PatientsRead)]
    public Task<PatientDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPut("{id:guid}"), HasPermission(P.PatientsWrite)]
    public Task<PatientDto> Update(Guid id, SavePatientRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPut("{id:guid}/status"), HasPermission(P.PatientsWrite)]
    public Task<PatientDto> ChangeStatus(Guid id, ChangePatientStatusRequest request, CancellationToken ct) => service.ChangeStatusAsync(id, request.Status, ct);

    /// <summary>Resumen de la ficha (secciones según permisos: cuenta, tratamiento, última evolución, laboratorio).</summary>
    [HttpGet("{id:guid}/summary"), HasPermission(P.PatientsRead)]
    public Task<PatientSummaryDto> Summary(Guid id, CancellationToken ct) => service.GetSummaryAsync(id, ct);

    /// <summary>Historial administrativo (turnos, pagos, presupuestos, documentos no clínicos).</summary>
    [HttpGet("{id:guid}/history"), HasPermission(P.PatientsRead)]
    public Task<IReadOnlyList<PatientHistoryItemDto>> History(Guid id, CancellationToken ct) => service.GetAdministrativeHistoryAsync(id, ct);

    /// <summary>Antecedentes médicos completos (información clínica restringida).</summary>
    [HttpGet("{id:guid}/medical-history"), HasPermission(P.PatientsMedicalRead)]
    public Task<MedicalHistoryDto> MedicalHistory(Guid id, CancellationToken ct) => service.GetMedicalHistoryAsync(id, ct);

    [HttpPut("{id:guid}/medical-history"), HasPermission(P.PatientsMedicalWrite)]
    public Task<MedicalHistoryDto> UpdateMedicalHistory(Guid id, MedicalHistoryRequest request, CancellationToken ct) => service.UpdateMedicalHistoryAsync(id, request, ct);

    /// <summary>Alertas médicas relevantes. Recepción ve sólo títulos de alertas necesarias para la atención.</summary>
    [HttpGet("{id:guid}/alerts"), HasPermission(P.PatientsAlertsRead)]
    public Task<IReadOnlyList<AlertDto>> Alerts(Guid id, CancellationToken ct) => service.GetAlertsAsync(id, ct);

    [HttpPost("{id:guid}/alerts"), HasPermission(P.PatientsMedicalWrite)]
    [ProducesResponseType<AlertDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAlert(Guid id, SaveAlertRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.AddAlertAsync(id, request, ct));

    [HttpPut("{id:guid}/alerts/{alertId:guid}"), HasPermission(P.PatientsMedicalWrite)]
    public Task<AlertDto> UpdateAlert(Guid id, Guid alertId, SaveAlertRequest request, CancellationToken ct) => service.UpdateAlertAsync(id, alertId, request, ct);

    [HttpDelete("{id:guid}/alerts/{alertId:guid}"), HasPermission(P.PatientsMedicalWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeactivateAlert(Guid id, Guid alertId, CancellationToken ct)
    {
        await service.DeactivateAlertAsync(id, alertId, ct);
        return NoContent();
    }
}
