using DailyChallenges.Services;

namespace DailyChallenges.Tests;

public class PagingTests
{
    [Theory]
    [InlineData(1, 20, 1, 20)]
    [InlineData(0, 20, 1, 20)]
    [InlineData(-5, 20, 1, 20)]
    [InlineData(3, 0, 3, 1)]
    [InlineData(3, -1, 3, 1)]
    [InlineData(2, 100, 2, 100)]
    [InlineData(2, 101, 2, 100)]
    [InlineData(2, int.MaxValue, 2, 100)]
    public void Clamp_bounds_page_and_page_size(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        Assert.Equal((expectedPage, expectedPageSize), Paging.Clamp(page, pageSize));
    }

    [Fact]
    public void Clamp_honours_custom_max()
    {
        Assert.Equal((1, 50), Paging.Clamp(1, 500, max: 50));
    }
}
