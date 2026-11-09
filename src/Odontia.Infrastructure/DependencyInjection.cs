using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Domain.Entities;
using Odontia.Infrastructure.BackgroundJobs;
using Odontia.Infrastructure.Messaging;
using Odontia.Infrastructure.Persistence;
using Odontia.Infrastructure.Security;
using Odontia.Infrastructure.Services;

namespace Odontia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env,
        Action<DbContextOptionsBuilder>? configureDb = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Section));
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.Section));
        services.Configure<LocalStorageOptions>(configuration.GetSection(LocalStorageOptions.Section));
        services.Configure<BackgroundJobOptions>(configuration.GetSection(BackgroundJobOptions.Section));

        services.AddDbContext<OdontiaDbContext>(options =>
        {
            if (configureDb is not null)
            {
                configureDb(options);
                return;
            }
            var cs = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("Falta ConnectionStrings:Default. Configúrela con User Secrets o la variable de entorno ConnectionStrings__Default.");
            // Sin EnableRetryOnFailure: las operaciones críticas (reservas, numeración, pagos) usan transacciones
            // explícitas con bloqueo de fila, incompatibles con la estrategia de reintento del proveedor.
            // Ante errores transitorios la API responde 500/409 y el cliente reintenta la operación completa.
            options.UseMySQL(cs, mysql => mysql.MigrationsAssembly(typeof(OdontiaDbContext).Assembly.FullName));
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OdontiaDbContext>());

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 4;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.SignIn.RequireConfirmedEmail = false; // controlado por Auth:RequireConfirmedEmail
            })
            .AddEntityFrameworkStores<OdontiaDbContext>()
            .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

        var keysPath = configuration["DataProtection:KeysPath"];
        var dp = services.AddDataProtection().SetApplicationName("Odontia");
        if (!string.IsNullOrWhiteSpace(keysPath)) dp.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        services.AddScoped<IAgendaLock, AgendaLock>();
        services.AddScoped<ISequenceGenerator, SequenceGenerator>();
        services.AddScoped<IMembershipResolver, MembershipResolver>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddScoped<IEmailSender>(sp =>
        {
            var provider = sp.GetRequiredService<IOptions<EmailOptions>>().Value.Provider;
            return provider.ToLowerInvariant() switch
            {
                "smtp" => ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp),
                "developmentfile" when env.IsDevelopment() => ActivatorUtilities.CreateInstance<DevelopmentFileEmailSender>(sp),
                _ => ActivatorUtilities.CreateInstance<NoEmailSender>(sp),
            };
        });
        services.AddScoped<IWhatsAppSender, NoWhatsAppSender>();

        if (configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            services.AddHostedService<AppointmentReminderJob>();
            services.AddHostedService<MaintenanceJob>();
        }

        services.AddHealthChecks().AddDbContextCheck<OdontiaDbContext>("database", tags: ["ready"]);
        return services;
    }
}
