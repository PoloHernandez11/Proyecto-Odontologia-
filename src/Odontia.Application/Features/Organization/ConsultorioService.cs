using Microsoft.EntityFrameworkCore;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Domain.Authorization;
using Odontia.Domain.Entities;

namespace Odontia.Application.Features.Organization;

public interface IConsultorioService
{
    Task<ConsultorioDto> GetCurrentAsync(CancellationToken ct);
    Task<ConsultorioDto> UpdateAsync(UpdateConsultorioRequest request, CancellationToken ct);
    Task<ConsultorioDto> UpdateSettingsAsync(UpdateConsultorioSettingsRequest request, CancellationToken ct);
    Task<IReadOnlyList<GabineteDto>> GetGabinetesAsync(bool includeInactive, CancellationToken ct);
    Task<GabineteDto> CreateGabineteAsync(SaveGabineteRequest request, CancellationToken ct);
    Task<GabineteDto> UpdateGabineteAsync(Guid id, SaveGabineteRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProfessionalDto>> GetProfessionalsAsync(CancellationToken ct);
}

internal sealed class ConsultorioService(IApplicationDbContext db, ITenantContext tenant) : IConsultorioService
{
    public async Task<ConsultorioDto> GetCurrentAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioRead);
        return ToDto(await LoadAsync(ct));
    }

    public async Task<ConsultorioDto> UpdateAsync(UpdateConsultorioRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioManage);
        var c = await LoadAsync(ct);
        c.UpdateDetails(r.Name, r.LegalName, r.TaxId, r.Phone, r.Email, r.Website, r.AddressLine, r.City, r.Province, r.PostalCode, r.Country);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<ConsultorioDto> UpdateSettingsAsync(UpdateConsultorioSettingsRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioManage);
        AuthService.EnsureTimeZone(r.TimeZoneId);
        var c = await LoadAsync(ct);
        c.UpdateSettings(r.TimeZoneId, r.DefaultAppointmentMinutes, r.SlotIntervalMinutes, r.ReminderHoursBefore);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<IReadOnlyList<GabineteDto>> GetGabinetesAsync(bool includeInactive, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioRead);
        return await db.Gabinetes.Where(g => includeInactive || g.IsActive).OrderBy(g => g.Name)
            .Select(g => new GabineteDto(g.Id, g.Name, g.Description, g.IsActive)).ToListAsync(ct);
    }

    public async Task<GabineteDto> CreateGabineteAsync(SaveGabineteRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioManage);
        if (await db.Gabinetes.AnyAsync(g => g.Name == r.Name.Trim(), ct)) throw new ConflictException("Ya existe un consultorio físico con ese nombre.");
        var g = new Gabinete(tenant.ConsultorioId, r.Name, r.Description);
        g.SetActive(r.IsActive);
        db.Gabinetes.Add(g);
        await db.SaveChangesAsync(ct);
        return new GabineteDto(g.Id, g.Name, g.Description, g.IsActive);
    }

    public async Task<GabineteDto> UpdateGabineteAsync(Guid id, SaveGabineteRequest r, CancellationToken ct)
    {
        tenant.Require(Permissions.ConsultorioManage);
        var g = await db.Gabinetes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException();
        if (await db.Gabinetes.AnyAsync(x => x.Id != id && x.Name == r.Name.Trim(), ct)) throw new ConflictException("Ya existe un consultorio físico con ese nombre.");
        g.Rename(r.Name, r.Description);
        g.SetActive(r.IsActive);
        await db.SaveChangesAsync(ct);
        return new GabineteDto(g.Id, g.Name, g.Description, g.IsActive);
    }

    public async Task<IReadOnlyList<ProfessionalDto>> GetProfessionalsAsync(CancellationToken ct)
    {
        tenant.Require(Permissions.AgendaRead);
        var defaultMinutes = await db.Consultorios.Where(c => c.Id == tenant.ConsultorioId).Select(c => c.DefaultAppointmentMinutes).FirstAsync(ct);
        var list = await db.Members.Where(m => m.IsActive && m.IsProfessional && m.User!.IsActive)
            .Select(m => new { m.UserId, m.User!.FirstName, m.User.LastName, m.User.Title, m.User.Specialty, m.User.LicenseNumber, m.DefaultAppointmentMinutes, m.AgendaColor })
            .ToListAsync(ct);
        return list
            .Where(m => tenant.CanActOnProfessional(m.UserId))
            .Select(m => new ProfessionalDto(m.UserId, Display(m.Title, m.FirstName, m.LastName), m.Title, m.Specialty, m.LicenseNumber,
                m.DefaultAppointmentMinutes ?? defaultMinutes, m.AgendaColor))
            .OrderBy(p => p.DisplayName).ToList();
    }

    internal static string Display(string? title, string first, string last) =>
        string.IsNullOrWhiteSpace(title) ? $"{first} {last}" : $"{title} {first} {last}";

    private async Task<Consultorio> LoadAsync(CancellationToken ct) =>
        await db.Consultorios.FirstOrDefaultAsync(c => c.Id == tenant.ConsultorioId, ct) ?? throw new NotFoundException();

    private static ConsultorioDto ToDto(Consultorio c) => new(c.Id, c.Name, c.LegalName, c.TaxId, c.Phone, c.Email, c.Website, c.AddressLine,
        c.City, c.Province, c.PostalCode, c.Country, c.TimeZoneId, c.DefaultAppointmentMinutes, c.SlotIntervalMinutes, c.ReminderHoursBefore, c.IsActive);
}
