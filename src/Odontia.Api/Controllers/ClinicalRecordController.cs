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
