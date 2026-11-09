using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odontia.Application.Abstractions;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;
using Odontia.Application.Features.Clinical;
using Odontia.Application.Features.Dashboard;
using Odontia.Application.Features.Documents;
using Odontia.Application.Features.Finance;
using Odontia.Application.Features.Lab;
using Odontia.Application.Features.Notifications;
using Odontia.Application.Features.Odontograms;
using Odontia.Application.Features.Organization;
using Odontia.Application.Features.Patients;
using Odontia.Application.Features.Scheduling;
using Odontia.Application.Features.Tasks;
using Odontia.Application.Features.Treatments;

namespace Odontia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.Section));
        services.Configure<DocumentOptions>(configuration.GetSection(DocumentOptions.Section));

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<IConsultorioClock, ConsultorioClock>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IConsultorioService, ConsultorioService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAvailabilityReader, AvailabilityReader>();
        services.AddScoped<IAgendaService, AgendaService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IClinicalService, ClinicalService>();
        services.AddScoped<IOdontogramService, OdontogramService>();
        services.AddScoped<ITreatmentService, TreatmentService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ILabService, LabService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        return services;
    }
}
