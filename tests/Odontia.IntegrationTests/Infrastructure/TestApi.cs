using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Odontia.Domain.Authorization;

namespace Odontia.IntegrationTests.Infrastructure;

/// <summary>Cliente de pruebas con sesión (access token en memoria + cookies HttpOnly de refresh).</summary>
public sealed class TestApi(HttpClient http, OdontiaFactory factory)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public HttpClient Http { get; } = http;
    public OdontiaFactory Factory { get; } = factory;
    public string? AccessToken { get; private set; }
    public string? CsrfToken { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ConsultorioId { get; private set; }
    public string Email { get; private set; } = "";
    public string Password { get; private set; } = "";

    public const string DefaultPassword = "Odontia2026Seguro";

    public static async Task<TestApi> RegisterOwnerAsync(OdontiaFactory factory, string email, string consultorio, bool professional = true)
    {
        var api = new TestApi(factory.NewClient(), factory);
        var r = await api.Http.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email, password = DefaultPassword, firstName = "Laura", lastName = "Méndez", consultorioName = consultorio,
            title = "Dra.", specialty = "Endodoncia", licenseNumber = "M.P. 4521", isProfessional = professional,
        });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        await api.LoginAsync(email, DefaultPassword);
        return api;
    }

    public async Task<HttpResponseMessage> LoginRawAsync(string email, string password) =>
        await Http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    /// <summary>Último refresh token recibido (sólo para reproducir ataques de reuso en pruebas).</summary>
    public string? LastRefreshCookie { get; private set; }

    public void CaptureRefreshCookie(HttpResponseMessage r)
    {
        if (!r.Headers.TryGetValues("Set-Cookie", out var cookies)) return;
        var rt = cookies.FirstOrDefault(c => c.StartsWith("odontia_rt=") && !c.StartsWith("odontia_rt=;"));
        if (rt is not null) LastRefreshCookie = rt.Split(';')[0]["odontia_rt=".Length..];
    }

    /// <summary>Envía un refresh con un token explícito (sin el contenedor de cookies del cliente).</summary>
    public async Task<HttpResponseMessage> ReplayRefreshAsync(string refreshToken)
    {
        var http = Factory.NewClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.Add("Cookie", $"odontia_rt={refreshToken}; odontia_csrf=replay");
        req.Headers.Add("X-CSRF-Token", "replay");
        return await http.SendAsync(req);
    }

    public async Task LoginAsync(string email, string password)
    {
        var r = await LoginRawAsync(email, password);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        CaptureRefreshCookie(r);
        var body = await r.Content.ReadFromJsonAsync<JsonObject>(Json);
        ApplySession(body!["result"]!.AsObject());
        Email = email;
        Password = password;
    }

    public void ApplySession(JsonObject sessionResponse)
    {
        var s = sessionResponse["session"]!;
        AccessToken = s["accessToken"]!.GetValue<string>();
        CsrfToken = sessionResponse["csrfToken"]!.GetValue<string>();
        UserId = s["user"]!["id"]!.GetValue<Guid>();
        var active = s["activeConsultorioId"];
        ConsultorioId = active is null ? Guid.Empty : active.GetValue<Guid>();
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
    }

    public async Task<HttpResponseMessage> RefreshRawAsync(bool withCsrf = true)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        if (withCsrf && CsrfToken is not null) req.Headers.Add("X-CSRF-Token", CsrfToken);
        return await Http.SendAsync(req);
    }

    public Task<HttpResponseMessage> Get(string url) => Http.GetAsync(url);
    public Task<HttpResponseMessage> Post(string url, object? body = null) => Http.PostAsJsonAsync(url, body ?? new { }, Json);
    public Task<HttpResponseMessage> Put(string url, object body) => Http.PutAsJsonAsync(url, body, Json);
    public Task<HttpResponseMessage> Delete(string url) => Http.DeleteAsync(url);

    public async Task<JsonNode> GetOk(string url) => await Ok(await Get(url));
    public async Task<JsonNode> PostOk(string url, object? body = null) => await Ok(await Post(url, body));
    public async Task<JsonNode> PutOk(string url, object body) => await Ok(await Put(url, body));

    public static async Task<JsonNode> Ok(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode} {r.RequestMessage?.Method} {r.RequestMessage?.RequestUri}: {text}");
        return JsonNode.Parse(text)!;
    }

    public static async Task AssertProblem(HttpResponseMessage r, HttpStatusCode status, string? code = null)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"Esperado {(int)status}, obtenido {(int)r.StatusCode}: {text}");
        if (code is not null)
        {
            var json = JsonNode.Parse(text)!;
            Assert.Equal(code, json["code"]?.GetValue<string>());
        }
    }

    // ---------------- Escenario común ----------------

    /// <summary>Invita a un usuario con un rol y devuelve su cliente ya autenticado.</summary>
    public async Task<TestApi> InviteAndLoginAsync(string email, Guid roleId, string firstName = "Carla", string lastName = "Suárez")
    {
        await PostOk("/api/v1/invitations", new { email, roleIds = new[] { roleId }, isProfessional = roleId == SystemRoles.OdontologoId });
        var link = Factory.Emails.LastLinkFor(email, "aceptar-invitacion");
        var token = CapturingEmailSender.Query(link, "token");
        var other = new TestApi(Factory.NewClient(), Factory);
        await Ok(await other.Http.PostAsJsonAsync("/api/v1/auth/invitations/accept", new { token, firstName, lastName, password = DefaultPassword }));
        await other.LoginAsync(email, DefaultPassword);
        return other;
    }

    public async Task<Guid> CreatePatientAsync(string dni, string firstName = "Martín", string lastName = "Rodríguez", object? medicalHistory = null)
    {
        var r = await PostOk("/api/v1/patients", new
        {
            patient = new
            {
                firstName, lastName, documentNumber = dni, birthDate = "1984-03-02", sex = "Male", occupation = "Contador",
                phone = "+54 9 11 5523-8810", email = $"p{dni}@mail.test", addressLine = "Av. Corrientes 2210",
            },
            medicalHistory,
        });
        return r["id"]!.GetValue<Guid>();
    }

    /// <summary>Horario laboral amplio (todos los días 07:00–21:00) para el profesional.</summary>
    public async Task SetFullWorkingHoursAsync(Guid professionalUserId)
    {
        var slots = Enum.GetValues<DayOfWeek>().Select(d => new { dayOfWeek = d.ToString(), startTime = "07:00", endTime = "21:00", gabineteId = (Guid?)null });
        await PutOk($"/api/v1/agenda/professionals/{professionalUserId}/working-hours", new { slots });
    }

    /// <summary>Fecha/hora local Argentina (UTC-3) de dentro de N días.</summary>
    public static string LocalSlot(int daysAhead, int hour, int minute = 0)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        var local = today.AddDays(daysAhead).AddHours(hour).AddMinutes(minute);
        return new DateTimeOffset(local, tz.GetUtcOffset(local)).ToString("yyyy-MM-ddTHH:mm:sszzz");
    }
}
