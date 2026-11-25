namespace DailyChallenges.Services;

/// <summary>Bounds for client-supplied paging parameters. Out-of-range values are clamped silently.</summary>
public static class Paging
{
    public const int FirstPage = 1;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Clamp(int page, int pageSize, int max = MaxPageSize)
        => (Math.Max(FirstPage, page), Math.Clamp(pageSize, MinPageSize, max));
}
