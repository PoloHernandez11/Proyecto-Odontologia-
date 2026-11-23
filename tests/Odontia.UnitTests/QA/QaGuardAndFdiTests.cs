using Odontia.Domain.Common;
using Odontia.Domain.Dental;
using Odontia.Domain.Entities;
using Odontia.Domain.Enums;

namespace Odontia.UnitTests.QA;

/// <summary>
/// Guard es interno del dominio: se ejercita a través de entidades públicas que lo usan
/// (Laboratory, Payment, BudgetItem, Expense) para cubrir obligatorios, longitudes e importes.
/// </summary>
public class QaGuardTests
{
    private static readonly Guid C = Guid.NewGuid();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Required_rejects_null_empty_and_whitespace(string? value)
    {
        var ex = Assert.Throws<DomainException>(() => new Laboratory(C, value!));
        Assert.Equal("required", ex.Code);
    }

    [Fact]
    public void Required_trims_value()
    {
        Assert.Equal("Lab Sur", new Laboratory(C, "  Lab Sur  ").Name);
    }

    [Fact]
    public void Required_max_length_is_inclusive_and_measured_after_trim()
    {
        var exact = new string('a', 120);
        Assert.Equal(exact, new Laboratory(C, $"   {exact}   ").Name);
        var ex = Assert.Throws<DomainException>(() => new Laboratory(C, exact + "b"));
        Assert.Equal("max_length", ex.Code);
    }

    [Fact]
    public void Optional_turns_blank_into_null_and_trims()
    {
        var lab = new Laboratory(C, "Lab");
        lab.Update("Lab", "   ", " 011 4444 ", null, "", "  nota ");
        Assert.Null(lab.ContactName);
        Assert.Equal("011 4444", lab.Phone);
        Assert.Null(lab.AddressLine);
        Assert.Equal("nota", lab.Notes);
    }

    [Fact]
    public void Optional_rejects_over_max_length()
    {
        var lab = new Laboratory(C, "Lab");
        var ex = Assert.Throws<DomainException>(() => lab.Update("Lab", null, new string('1', 41), null, null, null));
        Assert.Equal("max_length", ex.Code);
    }

    private static Payment Pay(decimal amount) =>
        new(C, Guid.NewGuid(), null, amount, PaymentMethod.Cash, DateTime.UtcNow, null, null, Guid.NewGuid(), 1);

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("-100")]
    [InlineData("10.001")]
    [InlineData("1000000000.00")]
    public void Money_rejects_zero_negative_too_many_decimals_and_overflow_when_zero_not_allowed(string raw)
    {
        var ex = Assert.Throws<DomainException>(() => Pay(decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal("invalid_amount", ex.Code);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("10.10")]
    [InlineData("10.100")] // escala 3 con valor representable en 2 decimales
    [InlineData("999999999.99")]
    public void Money_accepts_boundaries(string raw)
    {
        var v = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(v, Pay(v).Amount);
    }

    [Fact]
    public void Money_allows_zero_where_configured()
    {
        var b = new Budget(C, Guid.NewGuid(), 1, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 0, null);
        var item = b.AddItem(null, "Control sin cargo", null, 1, 0m, 0, null);
        Assert.Equal(0m, item.LineTotal);
        Assert.Equal(0m, b.Total);
    }

    [Fact]
    public void Expense_requires_positive_amount_and_concept()
    {
        Assert.Throws<DomainException>(() => new Expense(C, "Insumos", ExpenseCategory.Supplies, 0, PaymentMethod.Cash, DateTime.UtcNow, null, null, null, null, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new Expense(C, " ", ExpenseCategory.Supplies, 10, PaymentMethod.Cash, DateTime.UtcNow, null, null, null, null, Guid.NewGuid()));
        var e = new Expense(C, " Guantes ", ExpenseCategory.Supplies, 10.5m, PaymentMethod.Cash, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), "  ", null, null, null, Guid.NewGuid());
        Assert.Equal("Guantes", e.Description);
        Assert.Null(e.Supplier);
        Assert.Equal(DateTimeKind.Utc, e.PaidAtUtc.Kind);
        Assert.Equal(ExpenseStatus.Registered, e.Status);
    }

    [Fact]
    public void Expense_void_requires_reason_and_happens_once()
    {
        var e = new Expense(C, "Alquiler", ExpenseCategory.Rent, 1000, PaymentMethod.BankTransfer, DateTime.UtcNow, null, null, null, null, Guid.NewGuid());
        Assert.Throws<DomainException>(() => e.Void("  ", Guid.NewGuid(), DateTime.UtcNow));
        Assert.Equal(ExpenseStatus.Registered, e.Status);
        e.Void("Carga duplicada", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(ExpenseStatus.Voided, e.Status);
        var ex = Assert.Throws<DomainException>(() => e.Void("otra", Guid.NewGuid(), DateTime.UtcNow));
        Assert.Equal("expense_already_voided", ex.Code);
    }
}

public class QaFdiTests
{
    public static IEnumerable<object[]> AllValidCodes()
    {
        for (var q = 1; q <= 4; q++) for (var p = 1; p <= 8; p++) yield return [q * 10 + p];
        for (var q = 5; q <= 8; q++) for (var p = 1; p <= 5; p++) yield return [q * 10 + p];
    }

    [Theory]
    [MemberData(nameof(AllValidCodes))]
    public void Every_fdi_code_is_valid_and_consistent(int code)
    {
        Assert.True(Fdi.IsValid(code));
        var t = Fdi.Get(code);
        Assert.Equal(code / 10, t.Quadrant);
        Assert.Equal(code % 10, t.Position);
        Assert.Equal(t.Quadrant <= 4 ? Dentition.Permanent : Dentition.Temporary, t.Dentition);
        Assert.Equal(5, t.ValidSurfaces.Count);
        Assert.Equal(5, t.ValidSurfaces.Distinct().Count());
        Assert.False(string.IsNullOrWhiteSpace(t.Name));
    }

    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(30)] [InlineData(40)] [InlineData(49)]
    [InlineData(50)] [InlineData(56)] [InlineData(66)] [InlineData(76)] [InlineData(86)]
    [InlineData(9)] [InlineData(-11)] [InlineData(91)] [InlineData(111)] [InlineData(int.MaxValue)]
    public void Invalid_codes_are_rejected(int code)
    {
        Assert.False(Fdi.IsValid(code));
        var ex = Assert.Throws<DomainException>(() => Fdi.Get(code));
        Assert.Equal("invalid_tooth", ex.Code);
        Assert.False(Fdi.IsValidSurface(code, ToothSurface.Mesial));
    }

    [Fact]
    public void Catalog_has_52_unique_teeth()
    {
        Assert.Equal(52, Fdi.All.Count);
        Assert.Equal(52, Fdi.All.Select(t => t.Code).Distinct().Count());
    }

    [Theory]
    [InlineData(11, true)] [InlineData(28, true)] [InlineData(55, true)] [InlineData(65, true)]
    [InlineData(31, false)] [InlineData(48, false)] [InlineData(75, false)] [InlineData(85, false)]
    public void Upper_arch_is_derived_from_quadrant(int code, bool upper) => Assert.Equal(upper, Fdi.Get(code).IsUpper);

    [Theory]
    [InlineData(13, ToothType.Canine)]
    [InlineData(14, ToothType.Premolar)]
    [InlineData(18, ToothType.Molar)]
    [InlineData(53, ToothType.Canine)]
    [InlineData(54, ToothType.Molar)] // los temporales no tienen premolares
    [InlineData(82, ToothType.Incisor)]
    public void Tooth_type_follows_position(int code, ToothType type) => Assert.Equal(type, Fdi.Get(code).Type);

    [Fact]
    public void Temporary_dentition_has_no_premolars()
    {
        Assert.DoesNotContain(Fdi.All, t => t.Dentition == Dentition.Temporary && t.Type == ToothType.Premolar);
    }

    [Theory]
    [InlineData(51, ToothSurface.Incisal, true)]
    [InlineData(51, ToothSurface.Palatal, true)]
    [InlineData(51, ToothSurface.Lingual, false)]
    [InlineData(84, ToothSurface.Occlusal, true)]
    [InlineData(84, ToothSurface.Lingual, true)]
    [InlineData(84, ToothSurface.Palatal, false)]
    [InlineData(43, ToothSurface.Incisal, true)]
    [InlineData(43, ToothSurface.Occlusal, false)]
    [InlineData(26, ToothSurface.Vestibular, true)]
    public void Surface_validity_matrix(int code, ToothSurface s, bool valid) => Assert.Equal(valid, Fdi.IsValidSurface(code, s));

    [Fact]
    public void Undefined_surface_enum_value_is_not_valid()
    {
        Assert.False(Fdi.IsValidSurface(16, (ToothSurface)99));
    }

    [Fact]
    public void Dental_piece_copies_fdi_data()
    {
        var p = new DentalPiece(Fdi.Get(36));
        Assert.Equal(36, p.Code);
        Assert.Equal(3, p.Quadrant);
        Assert.Equal(ToothType.Molar, p.Type);
        Assert.Contains("inferior izquierdo", p.Name);
    }

    [Fact]
    public void Surface_seed_has_unique_codes_and_abbreviations()
    {
        var seed = DentalSurface.Seed;
        Assert.Equal(Enum.GetValues<ToothSurface>().Length, seed.Count);
        Assert.Equal(seed.Count, seed.Select(s => s.Abbreviation).Distinct().Count());
        Assert.Equal(seed.Count, seed.Select(s => s.Code).Distinct().Count());
    }
}
