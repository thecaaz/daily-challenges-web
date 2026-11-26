using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Moq;

namespace DailyChallenges.Tests;

public class FavoriteServiceTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static (FavoriteService Service, Mock<IFavoriteRepository> Favorites, Mock<IGameRepository> Games) Build()
    {
        var favorites = new Mock<IFavoriteRepository>();
        var games = new Mock<IGameRepository>();
        return (new FavoriteService(favorites.Object, games.Object, new FixedTimeProvider(Now)), favorites, games);
    }

    [Fact]
    public async Task AddFavorite_UnknownGame_ThrowsKeyNotFound()
    {
        var (service, favorites, _) = Build();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.AddFavoriteAsync(1, 9, CancellationToken.None));

        Assert.Equal("Game not found", ex.Message);
        favorites.Verify(r => r.AddAsync(It.IsAny<Favorite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddFavorite_KnownGame_AddsTheUsersFavorite()
    {
        var (service, favorites, games) = Build();
        games.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(new Game { Id = 9 });

        await service.AddFavoriteAsync(1, 9, CancellationToken.None);

        favorites.Verify(r => r.AddAsync(It.Is<Favorite>(f => f.UserId == 1 && f.GameId == 9 && f.CreatedAt == Now), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveFavorite_DelegatesToTheRepository()
    {
        var (service, favorites, _) = Build();

        await service.RemoveFavoriteAsync(1, 9, CancellationToken.None);

        favorites.Verify(r => r.RemoveAsync(1, 9, It.IsAny<CancellationToken>()), Times.Once);
    }
}
