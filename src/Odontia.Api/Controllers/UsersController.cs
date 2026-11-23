using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Application.Features.Organization;
using P = Odontia.Domain.Authorization.Permissions;

namespace Odontia.Api.Controllers;

/// <summary>Usuarios del consultorio, roles, permisos e invitaciones.</summary>
[Route("api/v{version:apiVersion}")]
public sealed class UsersController(IMemberService service) : ApiControllerBase
{
    /// <summary>Usuarios (miembros) del consultorio.</summary>
    [HttpGet("users"), HasPermission(P.UsersRead)]
    public Task<IReadOnlyList<MemberDto>> List([FromQuery] bool includeInactive, CancellationToken ct) => service.GetMembersAsync(includeInactive, ct);

    [HttpGet("users/{memberId:guid}"), HasPermission(P.UsersRead)]
    public Task<MemberDto> Get(Guid memberId, CancellationToken ct) => service.GetMemberAsync(memberId, ct);

    /// <summary>Marca como profesional y preferencias de agenda.</summary>
    [HttpPut("users/{memberId:guid}"), HasPermission(P.UsersManage)]
    public Task<MemberDto> Update(Guid memberId, UpdateMemberRequest request, CancellationToken ct) => service.UpdateMemberAsync(memberId, request, ct);

    /// <summary>Asigna roles. No se pueden otorgar permisos que el actor no posee ni modificar los propios.</summary>
    [HttpPut("users/{memberId:guid}/roles"), HasPermission(P.UsersManage)]
    public Task<MemberDto> UpdateRoles(Guid memberId, UpdateMemberRolesRequest request, CancellationToken ct) => service.UpdateRolesAsync(memberId, request, ct);

    /// <summary>Activa o desactiva un usuario en el consultorio (desactivar revoca sus sesiones).</summary>
    [HttpPut("users/{memberId:guid}/active"), HasPermission(P.UsersManage)]
    public Task<MemberDto> SetActive(Guid memberId, SetActiveRequest request, CancellationToken ct) => service.SetActiveAsync(memberId, request.IsActive, ct);

    /// <summary>Roles disponibles (de sistema y personalizados del consultorio).</summary>
    [HttpGet("roles"), HasPermission(P.UsersRead)]
    public Task<IReadOnlyList<RoleDto>> Roles(CancellationToken ct) => service.GetRolesAsync(ct);

    [HttpPost("roles"), HasPermission(P.RolesManage)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateRole(SaveRoleRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateRoleAsync(request, ct));

    [HttpPut("roles/{roleId:guid}"), HasPermission(P.RolesManage)]
    public Task<RoleDto> UpdateRole(Guid roleId, SaveRoleRequest request, CancellationToken ct) => service.UpdateRoleAsync(roleId, request, ct);

    [HttpDelete("roles/{roleId:guid}"), HasPermission(P.RolesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRole(Guid roleId, CancellationToken ct)
    {
        await service.DeleteRoleAsync(roleId, ct);
        return NoContent();
    }

    /// <summary>Catálogo de permisos granulares.</summary>
    [HttpGet("permissions"), HasPermission(P.UsersRead)]
    public IReadOnlyList<PermissionInfoDto> Permissions() => service.GetPermissionCatalog();

    [HttpGet("invitations"), HasPermission(P.UsersManage)]
    public Task<IReadOnlyList<InvitationDto>> Invitations(CancellationToken ct) => service.GetInvitationsAsync(ct);

    /// <summary>Invita por correo a un usuario con los roles indicados.</summary>
    [HttpPost("invitations"), HasPermission(P.UsersManage)]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Invite(CreateInvitationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.InviteAsync(request, ct));

    [HttpDelete("invitations/{id:guid}"), HasPermission(P.UsersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken ct)
    {
        await service.RevokeInvitationAsync(id, ct);
        return NoContent();
    }
}
