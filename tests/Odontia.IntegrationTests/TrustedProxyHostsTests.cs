using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Odontia.Api.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>Confianza en el proxy por nombre de host resuelto por DNS (QA ARQ-15).</summary>
public sealed class TrustedProxyHostsTests
{
    private static readonly IPAddress Caddy = IPAddress.Parse("172.18.0.4");
    private static readonly IPAddress Otro = IPAddress.Parse("172.18.0.9");

    private sealed class Reloj : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class DnsFalso
    {
        public Func<string, IPAddress[]> Respuesta { get; set; } = _ => [Caddy];
        public int Consultas { get; private set; }
        public Task<IPAddress[]> Resolver(string host, CancellationToken _)
        {
            Consultas++;
            return Task.FromResult(Respuesta(host));
        }
    }

    private static TrustedProxyHosts Crear(DnsFalso dns, Reloj reloj) => new(
        ["miaula-web-1"], TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(5), dns.Resolver, reloj,
        NullLogger<TrustedProxyHosts>.Instance);

    [Fact]
    public async Task Confia_solo_en_la_ip_resuelta_incluida_la_forma_ipv4_mapeada()
    {
        var dns = new DnsFalso();
        var trust = Crear(dns, new Reloj());

        Assert.True(await trust.IsTrustedAsync(Caddy));
        Assert.True(await trust.IsTrustedAsync(Caddy.MapToIPv6()));
        Assert.False(await trust.IsTrustedAsync(Otro));
        Assert.False(await trust.IsTrustedAsync(null));
    }

    [Fact]
    public async Task Si_la_resolucion_falla_no_confia_en_nadie()
    {
        var dns = new DnsFalso { Respuesta = _ => throw new SocketException((int)SocketError.HostNotFound) };
        var trust = Crear(dns, new Reloj());

        Assert.False(await trust.IsTrustedAsync(Caddy));
        Assert.False(await trust.IsTrustedAsync(Otro));
    }

    [Fact]
    public async Task Si_la_resolucion_falla_tras_un_exito_conserva_las_ultimas_ips_conocidas()
    {
        var dns = new DnsFalso();
        var reloj = new Reloj();
        var trust = Crear(dns, reloj);
        Assert.True(await trust.IsTrustedAsync(Caddy));

        // Caída temporal del DNS en el refresco periódico: se sigue confiando en la última IP buena.
        dns.Respuesta = _ => throw new SocketException((int)SocketError.TryAgain);
        reloj.Ahora += TimeSpan.FromSeconds(61);
        Assert.True(await trust.IsTrustedAsync(Caddy));
        Assert.Equal(2, dns.Consultas);
        Assert.False(await trust.IsTrustedAsync(Otro));

        // Cuando el DNS vuelve, manda la nueva resolución.
        dns.Respuesta = _ => [Otro];
        reloj.Ahora += TimeSpan.FromSeconds(61);
        Assert.True(await trust.IsTrustedAsync(Otro));
        Assert.False(await trust.IsTrustedAsync(Caddy));
    }

    [Fact]
    public async Task Re_resuelve_cuando_cambia_la_ip_del_proxy_con_limite_de_frecuencia()
    {
        var dns = new DnsFalso();
        var reloj = new Reloj();
        var trust = Crear(dns, reloj);
        Assert.True(await trust.IsTrustedAsync(Caddy));
        Assert.Equal(1, dns.Consultas);

        // Caddy se recrea con otra IP: la IP vieja deja de ser de confianza en la próxima resolución.
        dns.Respuesta = _ => [Otro];
        Assert.False(await trust.IsTrustedAsync(Otro));   // dentro de la ventana de reintento: no consulta DNS
        Assert.Equal(1, dns.Consultas);

        reloj.Ahora += TimeSpan.FromSeconds(6);
        Assert.True(await trust.IsTrustedAsync(Otro));    // IP desconocida tras la ventana: re-resuelve
        Assert.Equal(2, dns.Consultas);
        Assert.False(await trust.IsTrustedAsync(Caddy));
    }

    [Fact]
    public async Task Refresca_periodicamente_aunque_la_ip_siga_siendo_conocida()
    {
        var dns = new DnsFalso();
        var reloj = new Reloj();
        var trust = Crear(dns, reloj);
        Assert.True(await trust.IsTrustedAsync(Caddy));

        dns.Respuesta = _ => [];
        reloj.Ahora += TimeSpan.FromSeconds(61);
        Assert.False(await trust.IsTrustedAsync(Caddy));
    }

    [Theory]
    [InlineData("172.18.0.4", true)]
    [InlineData("172.18.0.9", false)]
    public async Task El_middleware_descarta_cabeceras_forwarded_de_ips_no_confiables(string remota, bool conserva)
    {
        var trust = Crear(new DnsFalso(), new Reloj());
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(remota);
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        ctx.Request.Headers["X-Forwarded-Proto"] = "https";
        var llamado = false;
        var mw = new TrustedProxyHostsMiddleware(_ => { llamado = true; return Task.CompletedTask; }, trust);

        await mw.InvokeAsync(ctx);

        Assert.True(llamado);
        Assert.Equal(conserva, ctx.Request.Headers.ContainsKey("X-Forwarded-For"));
        Assert.Equal(conserva, ctx.Request.Headers.ContainsKey("X-Forwarded-Proto"));
    }
}
