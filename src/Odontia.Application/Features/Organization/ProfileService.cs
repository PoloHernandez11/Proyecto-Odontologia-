using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Domain.Entities;

namespace Odontia.Application.Features.Organization;

public interface IProfileService
{
    Task<ProfileDto> GetAsync(CancellationToken ct);
    Task<ProfileDto> UpdateAsync(UpdateProfileRequest request, CancellationToken ct);
    Task<ProfileDto> UpdateProfessionalAsync(UpdateProfessionalProfileRequest request, CancellationToken ct);
    Task<MessageResponse> ChangeEmailAsync(ChangeEmailRequest request, CancellationToken ct);
    Task<NotificationSettingsDto> UpdateNotificationSettingsAsync(NotificationSettingsDto request, CancellationToken ct);
}

internal sealed class ProfileService(UserManager<ApplicationUser> users, IApplicationDbContext db, ITenantContext tenant,
    IMessageDispatcher messages, IOptions<FrontendOptions> front) : IProfileService
{
    public async Task<ProfileDto> GetAsync(CancellationToken ct)
    {
        var user = await LoadUserAsync();
        return await ToDtoAsync(user, ct);
    }

    public async Task<ProfileDto> UpdateAsync(UpdateProfileRequest r, CancellationToken ct)
    {
        var user = await LoadUserAsync();
        user.FirstName = r.FirstName.Trim();
        user.LastName = r.LastName.Trim();
        user.Title = string.IsNullOrWhiteSpace(r.Title) ? null : r.Title.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim();
        await users.UpdateAsync(user);
        return await ToDtoAsync(user, ct);
    }

    public async Task<ProfileDto> UpdateProfessionalAsync(UpdateProfessionalProfileRequest r, CancellationToken ct)
    {
        var user = await LoadUserAsync();
        user.Specialty = string.IsNullOrWhiteSpace(r.Specialty) ? null : r.Specialty.Trim();
        user.LicenseNumber = string.IsNullOrWhiteSpace(r.LicenseNumber) ? null : r.LicenseNumber.Trim();
        await users.UpdateAsync(user);
        if (tenant.HasTenant)
        {
            var member = await db.Members.FirstAsync(m => m.Id == tenant.MemberId, ct);
            member.SetAgendaPreferences(r.DefaultAppointmentMinutes, member.AgendaColor);
            await db.SaveChangesAsync(ct);
        }
        return await ToDtoAsync(user, ct);
    }

    public async Task<MessageResponse> ChangeEmailAsync(ChangeEmailRequest r, CancellationToken ct)
    {
        var user = await LoadUserAsync();
        if (!await users.CheckPasswordAsync(user, r.Password))
            throw new RequestValidationException("password", "La contraseña es incorrecta.");
        var newEmail = r.NewEmail.Trim().ToLowerInvariant();
        const string msg = "Si el correo está disponible, se enviará un enlace de confirmación a la nueva dirección.";
        if (await users.FindByEmailAsync(newEmail) is not null) return new MessageResponse(msg);
        var token = await users.GenerateChangeEmailTokenAsync(user, newEmail);
        // Se aplica de inmediato con el token (el usuario ya se autenticó con contraseña); se notifica a ambas direcciones.
        var result = await users.ChangeEmailAsync(user, newEmail, token);
        if (!result.Succeeded) throw new BusinessRuleException("No fue posible cambiar el correo.");
        await users.SetUserNameAsync(user, newEmail);
        user.EmailConfirmed = false;
        await users.UpdateAsync(user);
        var confirm = await users.GenerateEmailConfirmationTokenAsync(user);
        var enc = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(confirm)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var link = $"{front.Value.BaseUrl.TrimEnd('/')}{front.Value.ConfirmEmailPath}?userId={user.Id}&token={Uri.EscapeDataString(enc)}";
        await messages.SendEmailAsync(null, newEmail, "Confirme su nuevo correo - Odentix", $"<p>Confirme su nuevo correo: <a href=\"{HtmlEncoder.Default.Encode(link)}\">confirmar</a></p>",
            $"Confirme su nuevo correo: {link}", "email_change", true, "User", user.Id, ct);
        return new MessageResponse(msg);
    }

    public async Task<NotificationSettingsDto> UpdateNotificationSettingsAsync(NotificationSettingsDto r, CancellationToken ct)
    {
        var uid = tenant.UserId;
        var s = await db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (s is null) { s = new UserNotificationSettings(uid); db.NotificationSettings.Add(s); }
        s.EmailEnabled = r.EmailEnabled;
        s.InAppEnabled = r.InAppEnabled;
        s.NotifyTaskAssigned = r.NotifyTaskAssigned;
        s.NotifyTaskDue = r.NotifyTaskDue;
        s.NotifyLabUpdates = r.NotifyLabUpdates;
        s.NotifyAppointmentChanges = r.NotifyAppointmentChanges;
        s.DailyAgendaSummary = r.DailyAgendaSummary;
        await db.SaveChangesAsync(ct);
        return ToSettings(s);
    }

    private async Task<ApplicationUser> LoadUserAsync() =>
        await users.FindByIdAsync(tenant.UserId.ToString()) ?? throw new UnauthorizedException();

    private async Task<ProfileDto> ToDtoAsync(ApplicationUser u, CancellationToken ct)
    {
        var settings = await db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == u.Id, ct) ?? new UserNotificationSettings(u.Id);
        int? minutes = tenant.HasTenant
            ? await db.Members.Where(m => m.Id == tenant.MemberId).Select(m => m.DefaultAppointmentMinutes).FirstOrDefaultAsync(ct)
            : null;
        return new ProfileDto(u.Id, u.Email!, u.EmailConfirmed, u.FirstName, u.LastName, u.DisplayName, u.Title, u.Specialty, u.LicenseNumber,
            u.PhoneNumber, u.TwoFactorEnabled, minutes, ToSettings(settings));
    }

    private static NotificationSettingsDto ToSettings(UserNotificationSettings s) => new(s.EmailEnabled, s.InAppEnabled, s.NotifyTaskAssigned,
        s.NotifyTaskDue, s.NotifyLabUpdates, s.NotifyAppointmentChanges, s.DailyAgendaSummary);
}
