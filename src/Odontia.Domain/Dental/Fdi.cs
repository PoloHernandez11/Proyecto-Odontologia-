using Odontia.Domain.Enums;

namespace Odontia.Domain.Dental;

/// <summary>Pieza dental según nomenclatura FDI (ISO 3950).</summary>
public sealed record FdiTooth(int Code, int Quadrant, int Position, Dentition Dentition, ToothType Type, string Name)
{
    public bool IsUpper => Quadrant is 1 or 2 or 5 or 6;
    public bool IsAnterior => Type is ToothType.Incisor or ToothType.Canine;

    /// <summary>Superficies válidas para la pieza (oclusal vs. incisal; palatina en superiores, lingual en inferiores).</summary>
    public IReadOnlyList<ToothSurface> ValidSurfaces =>
    [
        IsAnterior ? ToothSurface.Incisal : ToothSurface.Occlusal,
        ToothSurface.Mesial,
        ToothSurface.Distal,
        ToothSurface.Vestibular,
        IsUpper ? ToothSurface.Palatal : ToothSurface.Lingual,
    ];
}

/// <summary>Catálogo FDI para dentición permanente (11–48) y temporal (51–85).</summary>
public static class Fdi
{
    private static readonly Dictionary<int, FdiTooth> Teeth = Build();

    public static IReadOnlyCollection<FdiTooth> All => Teeth.Values;

    public static bool IsValid(int code) => Teeth.ContainsKey(code);

    public static FdiTooth Get(int code) =>
        Teeth.TryGetValue(code, out var t) ? t : throw new Common.DomainException($"Pieza dental FDI inválida: {code}.", "invalid_tooth");

    public static bool IsValidSurface(int toothCode, ToothSurface surface) =>
        Teeth.TryGetValue(toothCode, out var t) && t.ValidSurfaces.Contains(surface);

    private static Dictionary<int, FdiTooth> Build()
    {
        var dict = new Dictionary<int, FdiTooth>();
        string[] quadrantNames = ["", "superior derecho", "superior izquierdo", "inferior izquierdo", "inferior derecho"];

        for (var q = 1; q <= 4; q++)
        for (var p = 1; p <= 8; p++)
        {
            var (type, name) = p switch
            {
                1 => (ToothType.Incisor, "Incisivo central"),
                2 => (ToothType.Incisor, "Incisivo lateral"),
                3 => (ToothType.Canine, "Canino"),
                4 => (ToothType.Premolar, "1.er premolar"),
                5 => (ToothType.Premolar, "2.º premolar"),
                6 => (ToothType.Molar, "1.er molar"),
                7 => (ToothType.Molar, "2.º molar"),
                _ => (ToothType.Molar, "3.er molar"),
            };
            var code = q * 10 + p;
            dict[code] = new FdiTooth(code, q, p, Dentition.Permanent, type, $"{name} {quadrantNames[q]}");
        }

        for (var q = 5; q <= 8; q++)
        for (var p = 1; p <= 5; p++)
        {
            var (type, name) = p switch
            {
                1 => (ToothType.Incisor, "Incisivo central temporal"),
                2 => (ToothType.Incisor, "Incisivo lateral temporal"),
                3 => (ToothType.Canine, "Canino temporal"),
                4 => (ToothType.Molar, "1.er molar temporal"),
                _ => (ToothType.Molar, "2.º molar temporal"),
            };
            var code = q * 10 + p;
            dict[code] = new FdiTooth(code, q, p, Dentition.Temporary, type, $"{name} {quadrantNames[q - 4]}");
        }
        return dict;
    }
}
