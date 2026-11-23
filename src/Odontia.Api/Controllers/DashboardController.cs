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
