using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Odontia.Application.Common;
using Odontia.Domain.Entities;

namespace Odontia.UnitTests.QA;

public class QaPagingTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void Page_is_at_least_one(int input, int expected) => Assert.Equal(expected, new PageQuery { Page = input }.Page);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Page_size_is_clamped(int input, int expected) => Assert.Equal(expected, new PageQuery { PageSize = input }.PageSize);

    [Fact]
    public void Defaults()
    {
        var q = new PageQuery();
        Assert.Equal(1, q.Page);
        Assert.Equal(20, q.PageSize);
        Assert.Null(q.Sort);
    }

    [Theory]
    [InlineData(0, 20, 0, false)]
    [InlineData(20, 20, 1, false)]
    [InlineData(21, 20, 2, true)]
    [InlineData(5, 0, 0, false)]
    public void Paged_result_pages(int total, int size, int pages, bool hasNext)
    {
        var r = new PagedResult<int>([], 1, size, total);
        Assert.Equal(pages, r.TotalPages);
        Assert.Equal(hasNext, r.HasNext);
    }

    private static readonly IReadOnlyDictionary<string, Expression<Func<(string Name, int Age), object?>>> Sorts =
        new Dictionary<string, Expression<Func<(string Name, int Age), object?>>>
        {
            ["name"] = x => x.Name,
            ["age"] = x => x.Age,
        };

    private static readonly (string Name, int Age)[] Data = [("b", 30), ("a", 40), ("c", 20)];

    [Theory]
    [InlineData(null, "abc")]
    [InlineData("", "abc")]
    [InlineData("name", "abc")]
    [InlineData("+name", "abc")]
    [InlineData("-name", "cba")]
    [InlineData("  -NAME  ", "cba")]
    [InlineData("age", "cba")]
    [InlineData("-age", "abc")]
    [InlineData("password", "abc")] // campo no permitido: se ignora y se usa el orden por defecto
    [InlineData("-password", "abc")] // tampoco hereda la dirección pedida
    public void Apply_sort_uses_whitelist(string? sort, string expected)
    {
        var result = Data.AsQueryable().ApplySort(sort, Sorts, "name").Select(x => x.Name);
        Assert.Equal(expected, string.Concat(result));
    }

    [Fact]
    public void Apply_sort_with_descending_default()
    {
        Assert.Equal("cba", string.Concat(Data.AsQueryable().ApplySort("unknown", Sorts, "-name").Select(x => x.Name)));
    }

    [Fact]
    public async Task To_paged_returns_slice_and_total()
    {
        using var qa = new QaSqliteDb();
        await using (var seed = qa.Create(qa.Tenant()))
        {
            for (var i = 0; i < 4; i++) seed.Patients.Add(new Patient(qa.ConsultorioId, $"N{i}", "Apellido", $"4000000{i}"));
            await seed.SaveChangesAsync();
        }
        await using var db = qa.Create(qa.Tenant());
        var page = await db.Patients.OrderBy(p => p.DocumentNumber).Select(p => p.DocumentNumber)
            .ToPagedAsync(new PageQuery { Page = 2, PageSize = 2 }, CancellationToken.None);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["40000001", "40000002"], page.Items);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNext);

        var beyond = await db.Patients.OrderBy(p => p.DocumentNumber).ToPagedAsync(new PageQuery { Page = 10, PageSize = 2 }, CancellationToken.None);
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);
    }
}

public class QaSecureTokensAndMaskTests
{
    [Fact]
    public void Token_is_url_safe_without_padding()
    {
        for (var i = 0; i < 50; i++)
        {
            var t = SecureTokens.Create();
            Assert.Matches("^[A-Za-z0-9_-]+$", t);
            Assert.Equal(64, t.Length); // 48 bytes => 64 caracteres base64 sin relleno
        }
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(16, 22)]
    [InlineData(32, 43)]
    public void Token_length_depends_on_bytes(int bytes, int length) => Assert.Equal(length, SecureTokens.Create(bytes).Length);

    [Fact]
    public void Hash_is_known_sha256_lower_hex()
    {
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", SecureTokens.Hash(""));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", SecureTokens.Hash("abc"));
        Assert.NotEqual(SecureTokens.Hash("abc"), SecureTokens.Hash("ABC"));
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData("  ", "-")]
    [InlineData("a@b.com", "***")]
    [InlineData("@b.com", "***")]
    [InlineData("sin-arroba", "***")]
    [InlineData("ana@clinica.com", "a***@clinica.com")]
    public void Mask_email(string? email, string expected) => Assert.Equal(expected, Mask.Email(email));

    [Theory]
    [InlineData(null, "***")]
    [InlineData("123", "***")]
    [InlineData("1234", "***1234")]
    [InlineData("+54 11 4555-1234", "***1234")]
    public void Mask_phone(string? phone, string expected) => Assert.Equal(expected, Mask.Phone(phone));
}

public class QaAppExceptionTests
{
    [Fact]
    public void Exceptions_map_to_http_status_and_code()
    {
        Assert.Equal((404, "not_found"), (new NotFoundException().StatusCode, new NotFoundException().Code));
        Assert.Equal((403, "forbidden"), (new ForbiddenAccessException().StatusCode, new ForbiddenAccessException().Code));
        Assert.Equal((409, "x"), (new ConflictException("m", "x").StatusCode, new ConflictException("m", "x").Code));
        Assert.Equal(422, new BusinessRuleException("m").StatusCode);
        Assert.Equal(401, new UnauthorizedException().StatusCode);
        var v = new RequestValidationException("email", "inválido");
        Assert.Equal(400, v.StatusCode);
        Assert.Equal(["inválido"], v.Errors["email"]);
    }

    [Fact]
    public void Domain_exception_default_code()
    {
        Assert.Equal("domain_rule_violation", new Odontia.Domain.Common.DomainException("x").Code);
    }
}

public class QaConsultorioClockTests
{
    [Fact]
    public async Task Today_uses_consultorio_time_zone_near_midnight()
    {
        using var qa = new QaSqliteDb("America/Argentina/Buenos_Aires");
        var tp = new FixedTimeProvider(new DateTimeOffset(2026, 11, 20, 2, 30, 0, TimeSpan.Zero)); // 23:30 del 19 en AR
        await using var db = qa.Create(qa.Tenant());
        var clock = new ConsultorioClock(db, qa.Tenant(), tp);
        Assert.Equal(new DateOnly(2026, 11, 19), await clock.TodayAsync(CancellationToken.None));
        tp.Now = new DateTimeOffset(2026, 11, 20, 3, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 11, 20), await clock.TodayAsync(CancellationToken.None));
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }

    [Fact]
    public async Task Unknown_time_zone_falls_back_to_utc()
    {
        using var qa = new QaSqliteDb("Mars/Olympus_Mons");
        await using var db = qa.Create(qa.Tenant());
        var clock = new ConsultorioClock(db, qa.Tenant(), new FixedTimeProvider(new DateTimeOffset(2026, 11, 20, 1, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TimeZoneInfo.Utc, await clock.GetTimeZoneAsync(CancellationToken.None));
        Assert.Equal(new DateOnly(2026, 11, 20), await clock.TodayAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Time_zone_is_cached_per_instance()
    {
        using var qa = new QaSqliteDb("America/Argentina/Buenos_Aires");
        await using var db = qa.Create(qa.Tenant());
        var clock = new ConsultorioClock(db, qa.Tenant(), TimeProvider.System);
        var first = await clock.GetTimeZoneAsync(CancellationToken.None);
        var c = await db.Consultorios.SingleAsync();
        c.UpdateSettings("UTC", 30, 15, 24);
        await db.SaveChangesAsync();
        Assert.Same(first, await clock.GetTimeZoneAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Without_tenant_clock_cannot_resolve_zone()
    {
        using var qa = new QaSqliteDb();
        await using var db = qa.Create(null);
        var clock = new ConsultorioClock(db, new TenantContext(), TimeProvider.System);
        // EF envuelve la ForbiddenAccessException del contexto en InvalidOperationException al evaluar el parámetro de la consulta.
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => clock.TodayAsync(CancellationToken.None));
        Assert.IsType<ForbiddenAccessException>(ex as ForbiddenAccessException ?? ex.InnerException);
    }
}
