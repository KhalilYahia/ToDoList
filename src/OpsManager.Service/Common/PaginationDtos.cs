using OpsManager.Domain.Repositories;

namespace OpsManager.Service.Common;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
;

public static class PagedResponse
{
    public static PagedResponse<T> From<T>(PagedResult<T> result) =>
        new(result.Items, result.Page, result.PageSize, result.TotalCount);

    public static PagedResponse<TOutput> Map<TSource, TOutput>(
        PagedResult<TSource> result,
        Func<TSource, TOutput> mapper) =>
        new(result.Items.Select(mapper).ToArray(), result.Page, result.PageSize, result.TotalCount);
}

public sealed record PageQuery(int Page = 1, int PageSize = 20)
{
    public PageRequest ToDomain() => new PageRequest(Page, PageSize).Validate();
}
