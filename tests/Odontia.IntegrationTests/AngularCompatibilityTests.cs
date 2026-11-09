using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Odontia.IntegrationTests.Infrastructure;

namespace Odontia.IntegrationTests;

/// <summary>
/// Verifica lo que necesita un cliente Angular (HttpClient + interceptores + cliente generado desde OpenAPI),
/// usando solicitudes HTTP independientes de cualquier frontend.
/// </summary>
public sealed class AngularCompatibilityTests(ClinicScenario s) : IClassFixture<ClinicScenario>
{
    private const string AllowedOrigin = "https://app.test"; // configurado en OdontiaFactory (Cors:AllowedOrigins)

    // ---------------- CORS ----------------

    [Fact]
    public async Task Cors_preflight_allows_configured_origin_with_credentials_and_csrf_header()
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/refresh");
        req.Headers.Add("Origin", AllowedOrigin);
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "content-type,x-csrf-token,authorization");
        var r = await s.Factory.NewClient().SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Equal(AllowedOrigin, r.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", r.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        var allowed = string.Join(",", r.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant();
        Assert.Contains("x-csrf-token", allowed);
        Assert.Contains("authorization", allowed);
    }

    [Fact]
    public async Task Cors_rejects_unknown_origin()
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/patients");
        req.Headers.Add("Origin", "https://sitio-malicioso.example");
        req.Headers.Add("Access-Control-Request-Method", "GET");
        var r = await s.Factory.NewClient().SendAsync(req);
        Assert.False(r.Headers.Contains("Access-Control-Allow-Origin"));

        var get = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        get.Headers.Add("Origin", "https://sitio-malicioso.example");
        Assert.False((await s.Factory.NewClient().SendAsync(get)).Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_actual_request_exposes_headers_for_allowed_origin()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        req.Headers.Add("Origin", AllowedOrigin);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Doctor.AccessToken);
        var r = await s.Factory.NewClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(AllowedOrigin, r.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("Location", string.Join(",", r.Headers.GetValues("Access-Control-Expose-Headers")));
    }

    // ---------------- Errores uniformes (interceptor de Angular) ----------------

    [Theory]
    [InlineData("/api/v1/patients", null, HttpStatusCode.Unauthorized, "unauthorized")]
    [InlineData("/api/v1/ruta-inexistente", "doctor", HttpStatusCode.NotFound, "not_found")]
    [InlineData("/api/v1/audit", "secretary", HttpStatusCode.Forbidden, "forbidden")]
    public async Task Framework_errors_use_the_same_problem_format(string url, string? who, HttpStatusCode status, string code)
    {
        var http = who switch { "doctor" => s.Doctor.Http, "secretary" => s.Secretary.Http, _ => s.Factory.NewClient() };
        var r = await http.GetAsync(url);
        Assert.Equal(status, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType!.MediaType);
        var json = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Equal(code, json["code"]!.GetValue<string>());
        Assert.Equal((int)status, json["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrEmpty(json["traceId"]?.GetValue<string>()));
        Assert.False(string.IsNullOrEmpty(json["title"]?.GetValue<string>()));
    }

    [Fact]
    public async Task Method_not_allowed_and_unsupported_media_type_are_problem_details()
    {
        var r = await s.Doctor.Http.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/v1/patients"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, r.StatusCode);
        Assert.Equal("method_not_allowed", JsonNode.Parse(await r.Content.ReadAsStringAsync())!["code"]!.GetValue<string>());

        var xml = await s.Doctor.Http.PostAsync("/api/v1/tasks", new StringContent("<task/>", System.Text.Encoding.UTF8, "application/xml"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, xml.StatusCode);
        Assert.Equal("unsupported_media_type", JsonNode.Parse(await xml.Content.ReadAsStringAsync())!["code"]!.GetValue<string>());
    }

    // ---------------- Query string (HttpParams de Angular no codifica '+') ----------------

    [Theory]
    [InlineData("2026-11-01T00:00:00+00:00")]   // '+' sin codificar → llega como espacio
    [InlineData("2026-11-01T00:00:00%2B00:00")] // '+' codificado
    [InlineData("2026-11-01T00:00:00Z")]        // recomendado: toISOString()
    [InlineData("2026-11-01T00:00:00-03:00")]
    public async Task Date_query_parameters_accept_offsets_even_when_plus_is_not_encoded(string from)
    {
        var r = await s.Secretary.Get($"/api/v1/appointments/status-counts?from={from}&to=2026-12-01T00:00:00Z");
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_date_query_returns_field_error_in_spanish()
    {
        var r = await s.Secretary.Get("/api/v1/appointments?from=no-es-fecha");
        await TestApi.AssertProblem(r, HttpStatusCode.BadRequest, "validation_error");
        var json = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Contains("ISO 8601", json["errors"]!["from"]![0]!.GetValue<string>());
    }

    // ---------------- Respuestas JSON tipadas ----------------

    [Fact]
    public async Task Responses_are_camel_case_json_with_string_enums_and_iso_dates()
    {
        var r = await s.Doctor.Get($"/api/v1/patients/{s.PatientA}");
        Assert.Equal("application/json", r.Content.Headers.ContentType!.MediaType);
        var json = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.NotNull(json["firstName"]);
        Assert.Null(json["FirstName"]);
        Assert.Equal("Male", json["sex"]!.GetValue<string>());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", json["birthDate"]!.GetValue<string>());
        Assert.EndsWith("Z", json["createdAtUtc"]!.GetValue<string>());

        var counted = await s.Doctor.PostOk("/api/v1/notifications/read-all");
        Assert.NotNull(counted["updated"]);
    }

    // ---------------- Contrato OpenAPI para generar el cliente TypeScript ----------------

    private async Task<JsonNode> OpenApiAsync() => JsonNode.Parse(await s.Factory.NewClient().GetStringAsync("/swagger/v1/swagger.json"))!;

    [Fact]
    public async Task OpenApi_has_unique_camel_case_operation_ids_and_clean_schema_names()
    {
        var doc = await OpenApiAsync();
        var ops = doc["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject().Select(m => m.Value!)).ToList();
        Assert.True(ops.Count >= 168);
        var ids = ops.Select(o => o["operationId"]?.GetValue<string>()).ToList();
        Assert.All(ids, id => Assert.Matches("^[a-z][A-Za-z0-9]+$", id));
        Assert.Equal(ids.Count, ids.Distinct().Count());

        var schemas = doc["components"]!["schemas"]!.AsObject().Select(x => x.Key).ToList();
        Assert.All(schemas, n => Assert.Matches("^[A-Za-z][A-Za-z0-9]*$", n));
        Assert.Contains("PagedResultOfPatientListItemDto", schemas);
        Assert.Contains("ApiProblem", schemas);
    }

    [Fact]
    public async Task OpenApi_documents_security_permissions_and_errors()
    {
        var doc = await OpenApiAsync();
        var paths = doc["paths"]!.AsObject();
        foreach (var (path, item) in paths)
        foreach (var (method, op) in item!.AsObject())
        {
            var responses = op!["responses"]!.AsObject();
            Assert.True(responses.ContainsKey("429"), $"{method} {path} sin 429");
            var secured = op["security"] is JsonArray sec && sec.Count > 0;
            if (!secured) continue;
            Assert.True(responses.ContainsKey("401") && responses.ContainsKey("403"), $"{method} {path} sin 401/403");
            Assert.NotNull(responses["401"]!["content"]!["application/problem+json"]);
        }

        var create = paths["/api/v1/appointments"]!["post"]!;
        Assert.Equal("appointmentsCreate", create["operationId"]!.GetValue<string>());
        Assert.Contains("appointments.write", create["x-permissions"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.NotNull(create["responses"]!["409"]);
        Assert.NotNull(create["responses"]!["422"]);

        var download = paths["/api/v1/documents/{id}/download"]!["get"]!["responses"]!["200"]!["content"]!;
        Assert.NotNull(download["application/octet-stream"]);
        Assert.Null(download["application/json"]);

        var login = paths["/api/v1/auth/login"]!["post"]!;
        Assert.Null(login["security"]);
    }

    [Fact]
    public async Task OpenApi_is_also_available_as_yaml()
    {
        var r = await s.Factory.NewClient().GetAsync("/swagger/v1/swagger.yaml");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var text = await r.Content.ReadAsStringAsync();
        Assert.StartsWith("openapi:", text.TrimStart());
        Assert.Matches(new Regex("operationId: patientsSearch"), text);
    }
}
