namespace DailyChallenges.Repositories;

/// <summary>Offset paging as the repositories apply it: 1-based pages, a page below 1 reads as the first page.</summary>
internal static class PageQueryExtensions
{
    private const int FirstPage = 1;
    private const int MinPageSize = 1;

    public static IQueryable<T> TakePage<T>(this IQueryable<T> query, int page, int pageSize) =>
        query.Skip((Math.Max(page, FirstPage) - 1) * pageSize).Take(pageSize);

    /// <summary><paramref name="pageSize"/>, or <paramref name="fallback"/> when it is below 1.</summary>
    public static int ValidPageSizeOr(this int pageSize, int fallback) =>
        pageSize < MinPageSize ? fallback : pageSize;
}
