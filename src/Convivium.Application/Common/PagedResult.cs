namespace Convivium.Application.Common;

/// <summary>Pagina de resultados com o total, para o front montar a paginacao.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public bool HasNext => Page < TotalPages;
}

/// <summary>Parametros de paginacao vindos da query string, com limites sensatos.</summary>
public sealed record PageRequest
{
    private const int MaxPageSize = 200;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public int NormalizedPage => Page < 1 ? 1 : Page;

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
}
