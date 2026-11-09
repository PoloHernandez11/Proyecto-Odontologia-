using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Clinical;
using Odontia.Application.Features.Odontograms;
using Odontia.Application.Features.Treatments;
using Odontia.Domain.Enums;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Historia clínica: evoluciones cronológicas, correcciones trazables y diagnósticos. No accesible para recepción.</summary>
[Route("api/v{version:apiVersion}/patients/{patientId:guid}/clinical-record")]
public sealed class ClinicalRecordController(IClinicalService service) : ApiControllerBase
{
    /// <summary>Encabezado de la historia clínica con antecedentes y alertas.</summary>
    [HttpGet, HasPermission(P.ClinicalRead)]
    public Task<ClinicalRecordDto> Get(Guid patientId, CancellationToken ct) => service.GetRecordAsync(patientId, ct);

    /// <summary>Evolución cronológica (más recientes primero). Filtros: type, toothCode, includeVoided.</summary>
    [HttpGet("entries"), HasPermission(P.ClinicalRead)]
    public Task<PagedResult<ClinicalEntryDto>> Entries(Guid patientId, [FromQuery] ClinicalTimelineQuery query, CancellationToken ct) =>
        service.GetTimelineAsync(patientId, query, ct);

    /// <summary>Registra una evolución, consulta, observación, procedimiento, receta o estudio. El responsable es el profesional autenticado.</summary>
    [HttpPost("entries"), HasPermission(P.ClinicalWrite)]
    [ProducesResponseType<ClinicalEntryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEntry(Guid patientId, CreateClinicalEntryRequest request, CancellationToken ct)
    {
        var e = await service.CreateEntryAsync(patientId, request, ct);
        return CreatedAtAction(nameof(GetEntry), new { patientId, entryId = e.Id, version = "1" }, e);
    }

    [HttpGet("entries/{entryId:guid}"), HasPermission(P.ClinicalRead)]
    public Task<ClinicalEntryDto> GetEntry(Guid patientId, Guid entryId, CancellationToken ct) => service.GetEntryAsync(patientId, entryId, ct);

    /// <summary>Corrige un registro (sólo el autor, con motivo). La versión previa se conserva y se audita.</summary>
    [HttpPost("entries/{entryId:guid}/amendments"), HasPermission(P.ClinicalWrite)]
    public Task<ClinicalEntryDto> Amend(Guid patientId, Guid entryId, AmendClinicalEntryRequest request, CancellationToken ct) =>
        service.AmendEntryAsync(patientId, entryId, request, ct);

    /// <summary>Anula un registro con motivo (no se elimina).</summary>
    [HttpPost("entries/{entryId:guid}/void"), HasPermission(P.ClinicalWrite)]
    public Task<ClinicalEntryDto> Void(Guid patientId, Guid entryId, VoidClinicalEntryRequest request, CancellationToken ct) =>
        service.VoidEntryAsync(patientId, entryId, request, ct);

    /// <summary>Versiones anteriores del registro.</summary>
    [HttpGet("entries/{entryId:guid}/revisions"), HasPermission(P.ClinicalRead)]
    public Task<IReadOnlyList<ClinicalEntryRevisionDto>> Revisions(Guid patientId, Guid entryId, CancellationToken ct) =>
        service.GetRevisionsAsync(patientId, entryId, ct);

    [HttpGet("diagnoses"), HasPermission(P.ClinicalRead)]
    public Task<IReadOnlyList<DiagnosisDto>> Diagnoses(Guid patientId, [FromQuery] DiagnosisStatus? status, CancellationToken ct) =>
        service.GetDiagnosesAsync(patientId, status, ct);

    [HttpPost("diagnoses"), HasPermission(P.ClinicalWrite)]
    [ProducesResponseType<DiagnosisDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDiagnosis(Guid patientId, CreateDiagnosisRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateDiagnosisAsync(patientId, request, ct));

    [HttpPut("diagnoses/{diagnosisId:guid}/status"), HasPermission(P.ClinicalWrite)]
    public Task<DiagnosisDto> DiagnosisStatus(Guid patientId, Guid diagnosisId, ChangeDiagnosisStatusRequest request, CancellationToken ct) =>
        service.ChangeDiagnosisStatusAsync(patientId, diagnosisId, request.Status, ct);
}

/// <summary>Catálogo FDI y condiciones configurables del odontograma.</summary>
[Route("api/v{version:apiVersion}/odontogram")]
public sealed class OdontogramCatalogController(IOdontogramService service) : ApiControllerBase
{
    /// <summary>Piezas FDI (permanentes y temporales) con superficies válidas, superficies y condiciones.</summary>
    [HttpGet("catalog"), HasPermission(P.OdontogramRead)]
    public Task<OdontogramCatalogDto> Catalog(CancellationToken ct) => service.GetCatalogAsync(ct);

    [HttpPost("conditions"), HasPermission(P.CatalogManage)]
    [ProducesResponseType<ConditionDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCondition(SaveConditionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateConditionAsync(request, ct));

    [HttpPut("conditions/{id:guid}"), HasPermission(P.CatalogManage)]
    public Task<ConditionDto> UpdateCondition(Guid id, SaveConditionRequest request, CancellationToken ct) => service.UpdateConditionAsync(id, request, ct);
}

/// <summary>Odontograma del paciente: estado actual, registro de condiciones, notas e historial.</summary>
[Route("api/v{version:apiVersion}/patients/{patientId:guid}/odontogram")]
public sealed class OdontogramController(IOdontogramService service) : ApiControllerBase
{
    [HttpGet, HasPermission(P.OdontogramRead)]
    public Task<OdontogramDto> Get(Guid patientId, CancellationToken ct) => service.GetAsync(patientId, ct);

    /// <summary>Aplica una o varias condiciones (pieza o superficie; existente o a realizar) en una operación atómica.</summary>
    [HttpPost("findings"), HasPermission(P.OdontogramWrite)]
    public Task<OdontogramDto> Apply(Guid patientId, ApplyConditionsRequest request, CancellationToken ct) => service.ApplyAsync(patientId, request, ct);

    /// <summary>Marca un hallazgo como resuelto.</summary>
    [HttpPost("findings/{findingId:guid}/resolve"), HasPermission(P.OdontogramWrite)]
    public Task<OdontogramDto> Resolve(Guid patientId, Guid findingId, ResolveFindingRequest request, CancellationToken ct) =>
        service.ResolveAsync(patientId, findingId, request, ct);

    /// <summary>Retira un hallazgo cargado por error (queda en el historial con motivo).</summary>
    [HttpPost("findings/{findingId:guid}/remove"), HasPermission(P.OdontogramWrite)]
    public Task<OdontogramDto> Remove(Guid patientId, Guid findingId, RemoveFindingRequest request, CancellationToken ct) =>
        service.RemoveAsync(patientId, findingId, request, ct);

    [HttpPost("teeth/{toothCode:int}/notes"), HasPermission(P.OdontogramWrite)]
    [ProducesResponseType<ToothNoteDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddNote(Guid patientId, int toothCode, AddToothNoteRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.AddNoteAsync(patientId, toothCode, request, ct));

    /// <summary>Historial de modificaciones (filtrable por pieza) con profesional y fecha.</summary>
    [HttpGet("history"), HasPermission(P.OdontogramRead)]
    public Task<PagedResult<OdontogramHistoryDto>> History(Guid patientId, [FromQuery] OdontogramHistoryQuery query, CancellationToken ct) =>
        service.GetHistoryAsync(patientId, query, ct);
}

/// <summary>Catálogo de prestaciones y valores.</summary>
[Route("api/v{version:apiVersion}/procedures")]
public sealed class ProceduresController(ITreatmentService service) : ApiControllerBase
{
    /// <summary>Requiere treatments.read, budgets.read o catalog.manage.</summary>
    [HttpGet]
    public Task<PagedResult<ProcedureDto>> List([FromQuery] ProcedureQuery query, CancellationToken ct) => service.GetProceduresAsync(query, ct);

    [HttpPost, HasPermission(P.CatalogManage)]
    [ProducesResponseType<ProcedureDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveProcedureRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateProcedureAsync(request, ct));

    /// <summary>Actualiza la prestación. Los planes/presupuestos existentes conservan su valor histórico.</summary>
    [HttpPut("{id:guid}"), HasPermission(P.CatalogManage)]
    public Task<ProcedureDto> Update(Guid id, SaveProcedureRequest request, CancellationToken ct) => service.UpdateProcedureAsync(id, request, ct);
}

/// <summary>Planes de tratamiento y prestaciones con estados y progreso.</summary>
[Route("api/v{version:apiVersion}/patients/{patientId:guid}/treatment-plans")]
public sealed class TreatmentPlansController(ITreatmentService service) : ApiControllerBase
{
    [HttpGet, HasPermission(P.TreatmentsRead)]
    public Task<IReadOnlyList<TreatmentPlanDto>> List(Guid patientId, CancellationToken ct) => service.GetPlansAsync(patientId, ct);

    [HttpPost, HasPermission(P.TreatmentsWrite)]
    [ProducesResponseType<TreatmentPlanDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid patientId, CreateTreatmentPlanRequest request, CancellationToken ct)
    {
        var plan = await service.CreatePlanAsync(patientId, request, ct);
        return CreatedAtAction(nameof(Get), new { patientId, planId = plan.Id, version = "1" }, plan);
    }

    [HttpGet("{planId:guid}"), HasPermission(P.TreatmentsRead)]
    public Task<TreatmentPlanDto> Get(Guid patientId, Guid planId, CancellationToken ct) => service.GetPlanAsync(patientId, planId, ct);

    [HttpPut("{planId:guid}"), HasPermission(P.TreatmentsWrite)]
    public Task<TreatmentPlanDto> Update(Guid patientId, Guid planId, UpdateTreatmentPlanRequest request, CancellationToken ct) =>
        service.UpdatePlanAsync(patientId, planId, request, ct);

    /// <summary>Agrega una prestación (valor tomado del catálogo vigente).</summary>
    [HttpPost("{planId:guid}/items"), HasPermission(P.TreatmentsWrite)]
    public Task<TreatmentPlanDto> AddItem(Guid patientId, Guid planId, AddPlanItemRequest request, CancellationToken ct) =>
        service.AddItemAsync(patientId, planId, request, ct);

    /// <summary>Cambia el estado de una prestación (Pending, InProgress, Completed, Cancelled); el plan se recalcula.</summary>
    [HttpPut("{planId:guid}/items/{itemId:guid}/status"), HasPermission(P.TreatmentsWrite)]
    public Task<TreatmentPlanDto> ItemStatus(Guid patientId, Guid planId, Guid itemId, ChangeItemStatusRequest request, CancellationToken ct) =>
        service.ChangeItemStatusAsync(patientId, planId, itemId, request, ct);

    [HttpPost("{planId:guid}/cancel"), HasPermission(P.TreatmentsWrite)]
    public Task<TreatmentPlanDto> Cancel(Guid patientId, Guid planId, CancelPlanRequest request, CancellationToken ct) =>
        service.CancelPlanAsync(patientId, planId, request, ct);

    [HttpGet("{planId:guid}/history"), HasPermission(P.TreatmentsRead)]
    public Task<IReadOnlyList<TreatmentChangeDto>> History(Guid patientId, Guid planId, CancellationToken ct) =>
        service.GetPlanHistoryAsync(patientId, planId, ct);
}
