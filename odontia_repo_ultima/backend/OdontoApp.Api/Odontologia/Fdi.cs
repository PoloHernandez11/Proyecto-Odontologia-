namespace OdontoApp.Api.Odontologia;

/// <summary>Numeración dental FDI, la misma que usa la ficha en papel.</summary>
public static class Fdi
{
    // Permanentes: cuadrantes 1–4, piezas 1–8. Temporales: cuadrantes 5–8, piezas 1–5.
    public static readonly IReadOnlySet<byte> PiezasValidas = new HashSet<byte>(
        from c in Enumerable.Range(1, 8)
        from p in Enumerable.Range(1, c <= 4 ? 8 : 5)
        select (byte)(c * 10 + p));

    public static bool EsValida(byte numero) => PiezasValidas.Contains(numero);
}
