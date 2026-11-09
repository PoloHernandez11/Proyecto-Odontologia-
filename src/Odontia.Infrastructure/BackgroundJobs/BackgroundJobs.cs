using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Application.Features.Finance;
using Odontia.Application.Features.Lab;
using Odontia.Application.Features.Notifications;
using Odontia.Application.Features.Tasks;
using Odontia.Infrastructure.Persistence;

namespace Odontia.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobOptions
{
    public const string Section = "BackgroundJobs";
    public bool Enabled { get; set; } = true;
    public int ReminderIntervalMinutes { get; set; } = 15;
    public int MaintenanceIntervalMinutes { get; set; } = 60;
}

/// <summary>
/// Ejecuta un trabajo por cada consultorio activo, cada uno en su propio scope con el contexto de
/// consultorio inicializado: el filtro global y las validaciones de tenant se aplican igual que en HTTP.
/// </summary>
public abstract class PerConsultorioJob(IServiceScopeFactory scopes, ILogger logger, TimeSpan interval, string name) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        using var timer = new PeriodicTimer(interval);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error en el trabajo {Job}", name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        List<Guid> ids;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OdontiaDbContext>();
            ids = await db.Consultorios.AsNoTracking().Where(c => c.IsActive).Select(c => c.Id).ToListAsync(ct);
        }
        foreach (var id in ids)
        {
            using var scope = scopes.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>().InitializeBackground(id);
            try { await ExecuteForConsultorioAsync(scope.ServiceProvider, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error en el trabajo {Job} para el consultorio {ConsultorioId}", name, id);
            }
        }
    }

    protected abstract Task ExecuteForConsultorioAsync(IServiceProvider services, CancellationToken ct);
}

public sealed class AppointmentReminderJob(IServiceScopeFactory scopes, ILogger<AppointmentReminderJob> logger, IOptions<BackgroundJobOptions> options)
    : PerConsultorioJob(scopes, logger, TimeSpan.FromMinutes(Math.Max(1, options.Value.ReminderIntervalMinutes)), "recordatorios")
{
    protected override async Task ExecuteForConsultorioAsync(IServiceProvider services, CancellationToken ct) =>
        await services.GetRequiredService<INotificationService>().ProcessDueRemindersAsync(ct);
}

public sealed class MaintenanceJob(IServiceScopeFactory scopes, ILogger<MaintenanceJob> logger, IOptions<BackgroundJobOptions> options)
    : PerConsultorioJob(scopes, logger, TimeSpan.FromMinutes(Math.Max(1, options.Value.MaintenanceIntervalMinutes)), "mantenimiento")
{
    protected override async Task ExecuteForConsultorioAsync(IServiceProvider services, CancellationToken ct)
    {
        await services.GetRequiredService<IBudgetService>().ExpireDueBudgetsAsync(ct);
        await services.GetRequiredService<ITaskService>().NotifyDueTasksAsync(ct);
        await services.GetRequiredService<ILabService>().NotifyOverdueAsync(ct);
    }
}
