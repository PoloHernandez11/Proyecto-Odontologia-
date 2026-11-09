using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

public sealed class AuthTests(OdontiaFactory factory) : IClassFixture<OdontiaFactory>
{
    private static string Mail() => $"u{Guid.NewGuid():N}@odontia.test";

    [Fact]
    public async Task Register_login_and_me_returns_permissions_and_membership()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "Consultorio Méndez");
        var me = await api.GetOk("/api/v1/auth/me");
        Assert.Equal(api.ConsultorioId, me["activeConsultorioId"]!.GetValue<Guid>());
        var perms = me["permissions"]!.AsArray().Select(p => p!.GetValue<string>()).ToList();
        Assert.Contains("users.manage", perms);
        Assert.Contains("clinical.write", perms);
        Assert.Single(me["memberships"]!.AsArray());
        Assert.Equal("", me["accessToken"]!.GetValue<string>());
    }

    [Fact]
    public async Task Login_errors_are_identical_for_unknown_user_and_wrong_password()
    {
        var email = Mail();
        await TestApi.RegisterOwnerAsync(factory, email, "C1");
        var anon = new TestApi(factory.NewClient(), factory);
        var wrong = await anon.LoginRawAsync(email, "Incorrecta2026xx");
        var unknown = await anon.LoginRawAsync(Mail(), "Incorrecta2026xx");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var a = JsonNode.Parse(await wrong.Content.ReadAsStringAsync())!;
        var b = JsonNode.Parse(await unknown.Content.ReadAsStringAsync())!;
        Assert.Equal(a["detail"]!.GetValue<string>(), b["detail"]!.GetValue<string>());
        Assert.Equal("invalid_credentials", a["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Account_is_locked_after_repeated_failures()
    {
        var email = Mail();
        await TestApi.RegisterOwnerAsync(factory, email, "C2");
        var anon = new TestApi(factory.NewClient(), factory);
        for (var i = 0; i < 5; i++) await anon.LoginRawAsync(email, "Incorrecta2026xx");
        var r = await anon.LoginRawAsync(email, TestApi.DefaultPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_authentication()
    {
        var anon = factory.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/patients")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/auth/me")).StatusCode);
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.e30.invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/patients")).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_reuse_revokes_the_whole_session()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "C3");
        var oldCookie = GetRefreshCookie(api);

        var r1 = await api.RefreshRawAsync();
        var body = await TestApi.Ok(r1);
        api.ApplySession(body.AsObject());
        Assert.Contains(r1.Headers.GetValues("Set-Cookie"), c => c.StartsWith("odontia_rt=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase) && c.Contains("secure", StringComparison.OrdinalIgnoreCase));

        // Reuso del refresh token anterior (robado): se rechaza y se revoca la familia completa.
        var attacker = factory.NewClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.Add("Cookie", $"odontia_rt={oldCookie}; odontia_csrf=x");
        req.Headers.Add("X-CSRF-Token", "x");
        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.SendAsync(req)).StatusCode);

        // El usuario legítimo ya tampoco puede renovar (sesión revocada por detección de reuso).
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.RefreshRawAsync()).StatusCode);
    }

    private static string GetRefreshCookie(TestApi api)
    {
        // El cliente de pruebas expone las cookies a través de un CookieContainer interno: se re-emite el login para leerla.
        var login = api.LoginRawAsync(api.Email, api.Password).GetAwaiter().GetResult();
        var body = TestApi.Ok(login).GetAwaiter().GetResult();
        api.ApplySession(body["result"]!.AsObject());
        var cookie = login.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("odontia_rt="));
        return cookie.Split(';')[0]["odontia_rt=".Length..];
    }

    [Fact]
    public async Task Refresh_without_csrf_header_is_rejected()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "C4");
        await TestApi.AssertProblem(await api.RefreshRawAsync(withCsrf: false), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var api = await TestApi.RegisterOwnerAsync(factory, Mail(), "C5");
        var refresh = api.LastRefreshCookie!;
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        req.Headers.Add("X-CSRF-Token", api.CsrfToken);
        var logout = await api.Http.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("odontia_rt=;"));
        // Las cookies se borraron (sin CSRF → 403) y además el token revocado no sirve aunque se reenvíe.
        Assert.Equal(HttpStatusCode.Forbidden, (await api.RefreshRawAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ReplayRefreshAsync(refresh)).StatusCode);
    }

    [Fact]
    public async Task Change_password_invalidates_current_access_token_and_other_sessions()
    {
        var email = Mail();
        var api = await TestApi.RegisterOwnerAsync(factory, email, "C6");
        var otherDevice = new TestApi(factory.NewClient(), factory);
        await otherDevice.LoginAsync(email, TestApi.DefaultPassword);

        await api.PostOk("/api/v1/auth/change-password", new { currentPassword = TestApi.DefaultPassword, newPassword = "NuevaClave2026Segura" });
        await TestApi.AssertProblem(await api.Get("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "session_invalidated");

        // La sesión actual puede renovarse; la del otro dispositivo fue revocada.
        api.ApplySession((await TestApi.Ok(await api.RefreshRawAsync())).AsObject());
        await api.GetOk("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.RefreshRawAsync()).StatusCode);
    }

    [Fact]
    public async Task Forgot_and_reset_password_flow_without_user_enumeration()
    {
        var email = Mail();
        var api = await TestApi.RegisterOwnerAsync(factory, email, "C7");
        var anon = factory.NewClient();
        Assert.Equal(HttpStatusCode.Accepted, (await anon.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = Mail() })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await anon.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).StatusCode);

        var link = factory.Emails.LastLinkFor(email, "restablecer-contrasena");
        var token = CapturingEmailSender.Query(link, "token");
        var bad = await anon.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token = "invalido", newPassword = "OtraClave2026Segura" });
        await TestApi.AssertProblem(bad, HttpStatusCode.UnprocessableEntity, "invalid_reset_token");
        await TestApi.Ok(await anon.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token, newPassword = "OtraClave2026Segura" }));

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.RefreshRawAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.LoginRawAsync(email, "OtraClave2026Segura")).StatusCode);
        // El mensaje del registro saliente no persiste el enlace con el token.
        Assert.DoesNotContain(token, string.Join("", factory.Emails.Sent.Select(m => m.Subject)));
    }

    [Fact]
    public async Task Weak_password_is_rejected_with_validation_errors()
    {
        var anon = factory.NewClient();
        var r = await anon.PostAsJsonAsync("/api/v1/auth/register", new { email = Mail(), password = "corta", firstName = "A", lastName = "B", consultorioName = "X" });
        await TestApi.AssertProblem(r, HttpStatusCode.BadRequest, "validation_error");
        var json = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.NotNull(json["errors"]!["password"]);
    }

    [Fact]
    public async Task Sessions_can_be_listed_and_revoked()
    {
        var email = Mail();
        var api = await TestApi.RegisterOwnerAsync(factory, email, "C8");
        var other = new TestApi(factory.NewClient(), factory);
        await other.LoginAsync(email, TestApi.DefaultPassword);
        var sessions = (await api.GetOk("/api/v1/auth/sessions")).AsArray();
        Assert.Equal(2, sessions.Count);
        var foreign = sessions.First(s => !s!["isCurrent"]!.GetValue<bool>())!["sessionId"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.NoContent, (await api.Delete($"/api/v1/auth/sessions/{foreign}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.RefreshRawAsync()).StatusCode);
    }

    [Fact]
    public async Task Health_endpoints_are_public()
    {
        var anon = factory.NewClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/health/ready")).StatusCode);
    }
}

public sealed class EmailConfirmationTests(ConfirmedEmailFactory factory) : IClassFixture<ConfirmedEmailFactory>
{
    [Fact]
    public async Task Login_requires_confirmed_email_and_confirmation_works()
    {
        var email = $"c{Guid.NewGuid():N}@odontia.test";
        var anon = factory.NewClient();
        Assert.Equal(HttpStatusCode.Accepted, (await anon.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email, password = TestApi.DefaultPassword, firstName = "Tomás", lastName = "Paz", consultorioName = "Consultorio Paz",
        })).StatusCode);

        var api = new TestApi(factory.NewClient(), factory);
        await TestApi.AssertProblem(await api.LoginRawAsync(email, TestApi.DefaultPassword), HttpStatusCode.Unauthorized, "email_not_confirmed");

        var link = factory.Emails.LastLinkFor(email, "confirmar-correo");
        await TestApi.Ok(await anon.PostAsJsonAsync("/api/v1/auth/confirm-email", new
        {
            userId = CapturingEmailSender.Query(link, "userId"), token = CapturingEmailSender.Query(link, "token"),
        }));
        await api.LoginAsync(email, TestApi.DefaultPassword);
    }
}
