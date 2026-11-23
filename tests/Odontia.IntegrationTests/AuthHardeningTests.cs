using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Odontia.Application.Abstractions;
using Odontia.Domain.Enums;
using Odontia.Infrastructure.Persistence;
using Odontia.Infrastructure.Security;
using Odontia.Infrastructure.Services;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>Sesión, refresh y segundo factor (QA API-05..09).</summary>
public sealed class SessionHardeningTests(OdontiaFactory factory) : IClassFixture<OdontiaFactory>
{
    private static string Mail() => $"h{Guid.NewGuid():N}@odontia.test";

    private static async Task<string> CodeOf(HttpResponseMessage r) =>
        JsonNode.Parse(await r.Content.ReadAsStringAsync())?["code"]?.GetValue<string>() ?? "";

    /// <summary>Activa 2FA para el usuario y devuelve la clave del autenticador.</summary>
    private static async Task<string> EnableTwoFactorAsync(TestApi api)
    {
        var setup = await api.PostOk("/api/v1/auth/2fa/setup");
        var key = setup["sharedKey"]!.GetValue<string>();
        await api.PostOk("/api/v1/auth/2fa/enable", new { code = Totp.Code(key) });
        return key;
    }

    private static async Task<string> ChallengeAsync(OdontiaFactory f, string email)
    {
        var anon = new TestApi(f.NewClient(), f);
        var login = await TestApi.Ok(await anon.LoginRawAsync(email, TestApi.DefaultPassword));
        Assert.True(login["requiresTwoFactor"]!.GetValue<bool>());
        return login["twoFactorChallengeToken"]!.GetValue<string>();
    }

    private static Task<HttpResponseMessage> TwoFactorAsync(OdontiaFactory f, string challenge, string? code = null, string? recovery = null) =>
        f.NewClient().PostAsJsonAsync("/api/v1/auth/login/2fa", new { challengeToken = challenge, code, recoveryCode = recovery });

    // ---------- API-07 ----------

    [Fact]
    public async Task Two_factor_setup_is_post_and_does_not_invalidate_the_current_session()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "2FA Setup");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await api.Get("/api/v1/auth/2fa/setup")).StatusCode);

        var setup = await api.PostOk("/api/v1/auth/2fa/setup");
        await api.GetOk("/api/v1/auth/me"); // antes: 401 session_invalidated (rotación del security stamp)
        var key = setup["sharedKey"]!.GetValue<string>();
        Assert.Equal(key, (await api.PostOk("/api/v1/auth/2fa/setup"))["sharedKey"]!.GetValue<string>()); // idempotente

        var other = new TestApi(factory.NewClient(), factory);
        await other.LoginAsync(api.Email, TestApi.DefaultPassword);

        var codes = await api.PostOk("/api/v1/auth/2fa/enable", new { code = Totp.Code(key) });
        Assert.NotEmpty(codes["recoveryCodes"]!.AsArray());
        await api.GetOk("/api/v1/auth/me"); // la sesión que activó 2FA sigue operativa

        // Las demás sesiones (abiertas sin segundo factor) se revocan y su access token deja de valer de inmediato.
        await TestApi.AssertProblem(await other.Get("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "session_invalidated");
        // Con 2FA activo la clave no se vuelve a revelar.
        await TestApi.AssertProblem(await api.Post("/api/v1/auth/2fa/setup"), HttpStatusCode.UnprocessableEntity, "two_factor_already_enabled");
    }

    // ---------- API-06 ----------

    [Fact]
    public async Task Two_factor_challenge_and_totp_code_are_single_use()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "2FA Replay");
        var key = await EnableTwoFactorAsync(api);
        var code = Totp.Code(key);

        var challenge = await ChallengeAsync(factory, api.Email);
        // Un código incorrecto no consume el desafío: el usuario puede corregirlo.
        await TestApi.AssertProblem(await TwoFactorAsync(factory, challenge, code == "000000" ? "111111" : "000000"), HttpStatusCode.Unauthorized, "invalid_two_factor_code");
        await TestApi.Ok(await TwoFactorAsync(factory, challenge, code));

        // Mismo desafío + mismo código → rechazado; mismo desafío + código de recuperación → rechazado.
        await TestApi.AssertProblem(await TwoFactorAsync(factory, challenge, code), HttpStatusCode.Unauthorized, "two_factor_expired");
        await TestApi.AssertProblem(await TwoFactorAsync(factory, challenge, recovery: "abcd-efgh"), HttpStatusCode.Unauthorized, "two_factor_expired");

        // Desafío nuevo pero el mismo código TOTP ya usado → rechazado.
        var second = await ChallengeAsync(factory, api.Email);
        await TestApi.AssertProblem(await TwoFactorAsync(factory, second, code), HttpStatusCode.Unauthorized, "two_factor_code_reused");
    }

    [Fact]
    public async Task Recovery_code_login_consumes_the_challenge()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "2FA Recovery");
        var setup = await api.PostOk("/api/v1/auth/2fa/setup");
        var codes = await api.PostOk("/api/v1/auth/2fa/enable", new { code = Totp.Code(setup["sharedKey"]!.GetValue<string>()) });
        var recovery = codes["recoveryCodes"]!.AsArray().Select(c => c!.GetValue<string>()).ToList();

        var challenge = await ChallengeAsync(factory, api.Email);
        await TestApi.Ok(await TwoFactorAsync(factory, challenge, recovery: recovery[0]));
        await TestApi.AssertProblem(await TwoFactorAsync(factory, challenge, recovery: recovery[1]), HttpStatusCode.Unauthorized, "two_factor_expired");
    }

    [MySqlFact]
    public async Task Parallel_two_factor_logins_with_the_same_challenge_create_a_single_session()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "2FA Paralelo");
        var key = await EnableTwoFactorAsync(api);
        var challenge = await ChallengeAsync(factory, api.Email);
        var code = Totp.Code(key);
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => TwoFactorAsync(factory, challenge, code)));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    // ---------- API-05 ----------

    [MySqlFact]
    public async Task Parallel_refreshes_with_the_same_cookie_rotate_only_once()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Refresh Paralelo");
        var cookie = api.LastRefreshCookie!;
        for (var round = 0; round < 3; round++)
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => api.ReplayRefreshAsync(cookie)));
            Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
            var winner = results.Single(r => r.StatusCode == HttpStatusCode.OK);
            api.CaptureRefreshCookie(winner);
            // Si todos los perdedores fueron "refresh_conflict" (carrera real) la sesión sigue viva; si alguno llegó tras
            // la confirmación se trata como reuso y la sesión se revoca. En ambos casos sólo hubo UNA rotación.
            var codes = await Task.WhenAll(results.Where(r => r.StatusCode != HttpStatusCode.OK).Select(CodeOf));
            if (codes.Any(c => c != "refresh_conflict")) return;
            cookie = api.LastRefreshCookie!;
        }
    }

    [Fact]
    public async Task Refresh_conflict_does_not_clear_cookies_or_revoke_the_session_and_reuse_still_does()
    {
        // Sin carrera real en SQLite: se verifica el reuso secuencial fuera de la ventana de gracia (sigue revocando la familia).
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Refresh Reuso");
        var old = api.LastRefreshCookie!;
        var ok = await api.ReplayRefreshAsync(old);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        api.CaptureRefreshCookie(ok);
        await api.AgeRotatedRefreshTokensAsync(TimeSpan.FromMinutes(1));
        var reuse = await api.ReplayRefreshAsync(old);
        await TestApi.AssertProblem(reuse, HttpStatusCode.Unauthorized, "invalid_refresh_token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ReplayRefreshAsync(api.LastRefreshCookie!)).StatusCode);
    }

    [Fact]
    public async Task Just_rotated_refresh_token_within_the_grace_window_gets_refresh_conflict_and_keeps_the_session()
    {
        // Dos pestañas (o un reintento lento) presentan la cookie recién rotada: no es robo.
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Refresh Gracia");
        var old = api.LastRefreshCookie!;
        var ok = await api.ReplayRefreshAsync(old);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        api.CaptureRefreshCookie(ok);
        var current = api.LastRefreshCookie!;

        var late = await api.ReplayRefreshAsync(old);
        await TestApi.AssertProblem(late, HttpStatusCode.Unauthorized, "refresh_conflict");
        Assert.False(late.Headers.TryGetValues("Set-Cookie", out var cleared) && cleared.Any(c => c.StartsWith("odontia_rt=;")),
            "Un refresh_conflict no debe borrar las cookies.");

        // La sesión sigue viva: el token vigente renueva normalmente.
        var next = await api.ReplayRefreshAsync(current);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Just_rotated_refresh_token_whose_replacement_was_revoked_is_not_given_grace()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Refresh Gracia Revocada");
        var old = api.LastRefreshCookie!;
        var ok = await api.ReplayRefreshAsync(old);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        api.CaptureRefreshCookie(ok);
        var current = api.LastRefreshCookie!;
        // El reemplazo ya se usó (rotó otra vez): el token original dentro de la ventana ya no tiene un reemplazo activo.
        var again = await api.ReplayRefreshAsync(current);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        api.CaptureRefreshCookie(again);

        await TestApi.AssertProblem(await api.ReplayRefreshAsync(old), HttpStatusCode.Unauthorized, "invalid_refresh_token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ReplayRefreshAsync(api.LastRefreshCookie!)).StatusCode);
    }

    // ---------- API-08 ----------

    [Fact]
    public async Task Lockout_by_failed_logins_does_not_kill_existing_sessions()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Lockout");
        var attacker = new TestApi(factory.NewClient(), factory);
        for (var i = 0; i < 6; i++) await attacker.LoginRawAsync(api.Email, "Incorrecta2026xx");
        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.LoginRawAsync(api.Email, TestApi.DefaultPassword)).StatusCode);

        // La sesión legítima ya abierta sigue funcionando y puede renovarse.
        await api.GetOk("/api/v1/auth/me");
        var refreshed = await api.RefreshRawAsync();
        api.ApplySession((await TestApi.Ok(refreshed)).AsObject());
        await api.GetOk("/api/v1/auth/me");
    }

    // ---------- API-09 ----------

    [Fact]
    public async Task Logout_and_session_revocation_invalidate_the_access_token_immediately()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Logout Inmediato");
        var other = new TestApi(factory.NewClient(), factory);
        await other.LoginAsync(api.Email, TestApi.DefaultPassword);
        await other.GetOk("/api/v1/auth/me");

        var sessions = (await api.GetOk("/api/v1/auth/sessions")).AsArray();
        var foreign = sessions.First(s => !s!["isCurrent"]!.GetValue<bool>())!["sessionId"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.NoContent, (await api.Delete($"/api/v1/auth/sessions/{foreign}")).StatusCode);
        await TestApi.AssertProblem(await other.Get("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "session_invalidated");

        await api.GetOk("/api/v1/auth/me");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        req.Headers.Add("X-CSRF-Token", api.CsrfToken);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Http.SendAsync(req)).StatusCode);
        await TestApi.AssertProblem(await api.Get("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "session_invalidated");
    }

    // ---------- ARQ-07 ----------

    [Fact]
    public async Task Request_auth_state_is_cached_and_invalidated_on_change()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Cache");
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IMembershipResolver>();
        var db = scope.ServiceProvider.GetRequiredService<OdontiaDbContext>();
        var before = await resolver.GetSecurityStampAsync(api.UserId, default);
        Assert.NotNull(before);

        // Cambio por fuera de EF (sin interceptor): la caché sigue sirviendo el valor anterior → no hay consulta por solicitud.
        await db.Users.Where(u => u.Id == api.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, "otro-stamp"));
        Assert.Equal(before, await resolver.GetSecurityStampAsync(api.UserId, default));

        // Invalidación explícita (la misma que dispara el interceptor al guardar cambios del usuario).
        factory.Services.GetRequiredService<AuthStateVersions>().InvalidateUser(api.UserId);
        Assert.Equal("otro-stamp", await resolver.GetSecurityStampAsync(api.UserId, default));
        await TestApi.AssertProblem(await api.Get("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "session_invalidated");
    }

    [Fact]
    public async Task Session_memberships_expose_the_consultorio_time_zone()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Zona Horaria");
        var membership = (await api.GetOk("/api/v1/auth/me"))["memberships"]!.AsArray().Single()!;
        Assert.Equal("America/Argentina/Buenos_Aires", membership["timeZoneId"]!.GetValue<string>());

        var refreshed = await TestApi.Ok(await api.RefreshRawAsync());
        Assert.Equal("America/Argentina/Buenos_Aires",
            refreshed["session"]!["memberships"]!.AsArray().Single()!["timeZoneId"]!.GetValue<string>());
    }

    // ---------- ARQ-12 ----------

    [Fact]
    public async Task Async_audit_writer_persists_the_event()
    {
        var entityId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAuditWriter>().WriteAsync(AuditAction.LoginFailed, "User", entityId, "prueba");
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<OdontiaDbContext>();
        Assert.True(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.EntityId == entityId));
    }
}

/// <summary>Contrato HTTP y endurecimiento no funcional (QA API-10..14, ARQ-16/17).</summary>
public sealed class HttpHardeningTests(OdontiaFactory factory) : IClassFixture<OdontiaFactory>
{
    private static string Mail() => $"x{Guid.NewGuid():N}@odontia.test";
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Huge_page_number_returns_an_empty_page_instead_of_500()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Paginas");
        var page = await api.GetOk("/api/v1/patients?page=2147483647&pageSize=100");
        Assert.Empty(page["items"]!.AsArray());
        Assert.Equal(2147483647, page["page"]!.GetValue<int>());
    }

    [Fact]
    public async Task Framework_errors_are_problem_json()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Errores");

        var invalidJson = await api.Http.PostAsync("/api/v1/patients", new StringContent("{ not json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidJson.StatusCode);
        Assert.Equal(ProblemJson, invalidJson.Content.Headers.ContentType?.MediaType);

        var invalidEnum = await api.Http.PostAsync("/api/v1/patients", new StringContent(
            """{"patient":{"firstName":"A","lastName":"B","documentNumber":"30111222","sex":"NoExiste"}}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidEnum.StatusCode);
        Assert.Equal(ProblemJson, invalidEnum.Content.Headers.ContentType?.MediaType);

        var badQuery = await api.Get("/api/v1/patients?page=abc");
        Assert.Equal(HttpStatusCode.BadRequest, badQuery.StatusCode);
        Assert.Equal(ProblemJson, badQuery.Content.Headers.ContentType?.MediaType);

        var unsupported = await api.Http.PostAsync("/api/v1/patients", new StringContent("hola", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
        Assert.Equal(ProblemJson, unsupported.Content.Headers.ContentType?.MediaType);

        // Las respuestas exitosas siguen siendo application/json.
        Assert.Equal("application/json", (await api.Get("/api/v1/auth/me")).Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Json_bodies_over_the_global_limit_are_rejected_with_413()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Cuerpo");
        var big = new string('a', 3 * 1024 * 1024);
        var r = await api.Http.PostAsync("/api/v1/patients", new StringContent($"{{\"x\":\"{big}\"}}", Encoding.UTF8, "application/json"));
        await TestApi.AssertProblem(r, HttpStatusCode.RequestEntityTooLarge, "payload_too_large");
        Assert.Equal(ProblemJson, r.Content.Headers.ContentType?.MediaType);

        // La carga de documentos conserva su límite propio (mayor que el global).
        var patient = await api.CreatePatientAsync("30999111");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[3 * 1024 * 1024]), "file", "grande.pdf");
        form.Add(new StringContent("Other"), "category");
        var upload = await api.Http.PostAsync($"/api/v1/patients/{patient}/documents", form);
        Assert.NotEqual(HttpStatusCode.RequestEntityTooLarge, upload.StatusCode);
    }

    [Fact]
    public void Kestrel_does_not_send_the_server_header_and_limits_the_body()
    {
        var kestrel = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        Assert.False(kestrel.AddServerHeader);
        Assert.Equal(2 * 1024 * 1024, kestrel.Limits.MaxRequestBodySize);
    }

    [Fact]
    public async Task Readiness_endpoint_exposes_only_the_overall_status()
    {
        var r = await factory.NewClient().GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = JsonNode.Parse(await r.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(["status"], body.Select(p => p.Key));
        Assert.Equal("Healthy", body["status"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await factory.NewClient().GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public void Production_uses_structured_json_console_logging()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => Odontia.Api.Infrastructure.Hardening.ConfigureLogging(b, new TestEnv("Production")));
        using var sp = services.BuildServiceProvider();
        var console = sp.GetRequiredService<IOptionsMonitor<Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions>>().CurrentValue;
        Assert.Equal("json", console.FormatterName);

        var dev = new ServiceCollection();
        dev.AddLogging(b => Odontia.Api.Infrastructure.Hardening.ConfigureLogging(b, new TestEnv("Development")));
        using var devSp = dev.BuildServiceProvider();
        Assert.NotEqual("json", devSp.GetRequiredService<IOptionsMonitor<Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions>>().CurrentValue.FormatterName);
    }

    private sealed class TestEnv(string name) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Odontia.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

/// <summary>Fixture con límites de rate limiting bajos y proveedor de correo lento.</summary>
public sealed class ThrottledFactory : OdontiaFactory
{
    protected override IDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>
    {
        ["RateLimiting:AuthPermitPerMinute"] = "3",
    };
}

/// <summary>Rate limiting (API-11) y correos fuera del ciclo de la solicitud (ARQ-06).</summary>
public sealed class ThrottlingAndEmailTests(ThrottledFactory factory) : IClassFixture<ThrottledFactory>
{
    [Fact]
    public async Task Rejected_requests_include_retry_after_and_problem_json()
    {
        var client = factory.NewClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
            last = await client.PostAsJsonAsync("/api/v1/auth/resend-confirmation", new { email = $"n{i}@odontia.test" });
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Equal("application/problem+json", last.Content.Headers.ContentType?.MediaType);
        var retryAfter = last.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value.TotalSeconds, 1, 60);
    }
}

public sealed class SlowEmailTests(OdontiaFactory factory) : IClassFixture<OdontiaFactory>
{
    [Fact]
    public async Task Forgot_password_does_not_wait_for_a_slow_email_provider()
    {
        var email = $"s{Guid.NewGuid():N}@odontia.test";
        await TestApi.RegisterOwnerAsync(factory, email, "Correo lento");
        factory.Emails.Delay = TimeSpan.FromSeconds(3);
        try
        {
            var client = factory.NewClient();
            var sw = Stopwatch.StartNew();
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).StatusCode);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"La respuesta esperó al proveedor ({sw.Elapsed}).");

            sw.Restart();
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email, password = TestApi.DefaultPassword, firstName = "A", lastName = "B", consultorioName = "Duplicado",
            })).StatusCode);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"El registro duplicado esperó al proveedor ({sw.Elapsed}).");

            // El correo igualmente se envía en segundo plano.
            Assert.Contains("restablecer-contrasena", factory.Emails.LastLinkFor(email, "restablecer-contrasena"));
        }
        finally
        {
            factory.Emails.Delay = TimeSpan.Zero;
        }
    }
}
