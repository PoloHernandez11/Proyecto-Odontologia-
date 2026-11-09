using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Odontia.Application.Abstractions;
using Odontia.Infrastructure.Persistence;

namespace Odontia.IntegrationTests.Infrastructure;

/// <summary>
/// Host de pruebas. Por defecto usa SQLite en memoria (con FKs e índices únicos reales).
/// Si existe la variable de entorno ODONTIA_TEST_MYSQL (cadena de conexión a una base descartable),
/// usa MySQL real aplicando las migraciones de EF Core.
/// </summary>
public class OdontiaFactory : WebApplicationFactory<Program>
{
    private static readonly SemaphoreSlim HookLock = new(1, 1);
    private readonly SqliteConnection? _sqlite;
    private readonly string? _mysql;
    public CapturingEmailSender Emails { get; } = new();
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "odontia-tests", Guid.NewGuid().ToString("N"));
    protected virtual bool RequireConfirmedEmail => false;

    public OdontiaFactory()
    {
        var mysql = Environment.GetEnvironmentVariable("ODONTIA_TEST_MYSQL");
        if (mysql is null)
        {
            _sqlite = new SqliteConnection("DataSource=:memory:");
            _sqlite.Open();
        }
        else
        {
            // Una base descartable por fixture: las clases de prueba corren en paralelo sin interferirse.
            var db = $"odontia_test_{Guid.NewGuid():N}"[..28];
            _mysql = Regex.Replace(mysql, @"(?i)database=[^;]*", $"database={db}");
            if (!_mysql.Contains("database=", StringComparison.OrdinalIgnoreCase)) _mysql += $";database={db}";
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "integration-tests-only-signing-key-0123456789abcdef",
            ["ConnectionStrings:Default"] = _mysql ?? "unused",
            ["Auth:AllowPublicRegistration"] = "true",
            ["Auth:RequireConfirmedEmail"] = RequireConfirmedEmail ? "true" : "false",
            ["Auth:Cookies:Secure"] = "true",
            ["Auth:Cookies:SameSite"] = "Strict",
            ["BackgroundJobs:Enabled"] = "false",
            ["Storage:Local:RootPath"] = StorageRoot,
            ["Frontend:BaseUrl"] = "https://app.test",
            ["RateLimiting:AuthPermitPerMinute"] = "10000",
            ["RateLimiting:RefreshPermitPerMinute"] = "10000",
            ["Cors:AllowedOrigins:0"] = "https://app.test",
            ["Email:Provider"] = "None",
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        HookLock.Wait();
        try
        {
            OdontiaTestHooks.ConfigureDatabase = _sqlite is not null ? o => o.UseSqlite(_sqlite) : null;
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OdontiaDbContext>();
            if (_sqlite is not null) db.Database.EnsureCreated();
            else
            {
                db.Database.EnsureDeleted();
                db.Database.Migrate();
            }
            return host;
        }
        finally
        {
            OdontiaTestHooks.ConfigureDatabase = null;
            HookLock.Release();
        }
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    protected override void Dispose(bool disposing)
    {
        if (disposing && _mysql is not null)
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<OdontiaDbContext>().Database.EnsureDeleted();
            }
            catch (Exception) { /* limpieza de mejor esfuerzo */ }
        }
        base.Dispose(disposing);
        if (disposing)
        {
            _sqlite?.Dispose();
            try { if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, true); } catch (IOException) { }
        }
    }
}

public sealed class ConfirmedEmailFactory : OdontiaFactory
{
    protected override bool RequireConfirmedEmail => true;
}

/// <summary>Captura correos en memoria (simula un proveedor que NO confirma envío) para extraer enlaces en pruebas.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<OutgoingEmail> Sent { get; } = new();

    public Task<SendResult> SendAsync(OutgoingEmail email, CancellationToken ct)
    {
        Sent.Enqueue(email);
        return Task.FromResult(SendResult.NoProvider("test-capture"));
    }

    public string LastLinkFor(string to, string pathFragment)
    {
        var mail = Sent.Reverse().First(m => m.To.Equals(to, StringComparison.OrdinalIgnoreCase) && m.TextBody.Contains(pathFragment));
        return Regex.Match(mail.TextBody, @"https?://\S+").Value.TrimEnd('.', ')');
    }

    public static string Query(string url, string key)
    {
        var q = new Uri(url).Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        return q[key];
    }
}
