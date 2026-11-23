using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Odontia.Application.Abstractions;
using Odontia.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>ARQ-06: el envío SMTP se corta al vencer Email:Smtp:TimeoutSeconds en lugar de esperar 100 s.</summary>
public sealed class SmtpTimeoutTests
{
    [Fact]
    public async Task Smtp_send_times_out_against_a_server_that_never_answers()
    {
        // Servidor TCP que acepta la conexión y nunca envía el saludo SMTP.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = new List<TcpClient>();
        _ = Task.Run(async () => { try { while (true) accepted.Add(await listener.AcceptTcpClientAsync()); } catch { /* fin */ } });

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Smtp",
            ["Email:Smtp:Host"] = "127.0.0.1",
            ["Email:Smtp:Port"] = port.ToString(),
            ["Email:Smtp:EnableSsl"] = "false",
            ["Email:Smtp:TimeoutSeconds"] = "1",
            ["BackgroundJobs:Enabled"] = "false",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config, new Env(), o => o.UseSqlite("DataSource=:memory:"));
        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var sw = Stopwatch.StartNew();
        var result = await sender.SendAsync(new OutgoingEmail("dest@odontia.test", "Asunto", "<p>x</p>", "x"), CancellationToken.None);
        sw.Stop();

        Assert.False(result.Accepted);
        Assert.Equal("timeout", result.Error);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"El envío tardó {sw.Elapsed}.");
        listener.Stop();
        foreach (var c in accepted) c.Dispose();
    }

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Odontia.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
