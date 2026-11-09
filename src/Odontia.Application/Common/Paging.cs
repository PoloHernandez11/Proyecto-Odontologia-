using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Odontia.Application.Common;

/// <summary>Parámetros estándar de paginación y ordenamiento (query string).</summary>
public class PageQuery
{
    public const int MaxPageSize = 100;
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>Página (1..n).</summary>
    public int Page { get => _page; set => _page = value < 1 ? 1 : value; }

    /// <summary>Tamaño de página (1..100, por defecto 20).</summary>
    public int PageSize { get => _pageSize; set => _pageSize = value is < 1 ? 20 : Math.Min(value, MaxPageSize); }

    /// <summary>Campo de ordenamiento (lista blanca por endpoint). Prefijo '-' para descendente.</summary>
    public string? Sort { get; set; }
}

/// <summary>Respuesta paginada consistente.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PageQuery page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }

    /// <summary>Aplica ordenamiento a partir de una lista blanca; ignora campos no permitidos.</summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sort,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> allowed, string defaultSort)
    {
        var spec = string.IsNullOrWhiteSpace(sort) ? defaultSort : sort.Trim();
        var desc = spec.StartsWith('-');
        var key = spec.TrimStart('-', '+').ToLowerInvariant();
        if (!allowed.TryGetValue(key, out var expr))
        {
            desc = defaultSort.StartsWith('-');
            expr = allowed[defaultSort.TrimStart('-', '+').ToLowerInvariant()];
        }
        return desc ? query.OrderByDescending(expr) : query.OrderBy(expr);
    }
}
