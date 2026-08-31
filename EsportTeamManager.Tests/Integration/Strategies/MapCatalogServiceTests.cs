using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Strategies;
using EsportTeamManager.Tests.Integration.Persistence;
using Xunit;

namespace EsportTeamManager.Tests.Integration.Strategies;

public sealed class MapCatalogServiceTests
{
    [Fact]
    public async Task GetOptionsAsync_WithSeededDatabase_ReturnsControlledMapsAlphabetically()
    {
        await using SqliteTestDatabase database = new();

        await database.InitializeAsync();

        await using var context = database.CreateContext();
        MapCatalogService service = new(context);

        IReadOnlyCollection<MapOption> options = await service.GetOptionsAsync();

        string[] expectedNames =
        [
            "Abyss",
            "Ascent",
            "Bind",
            "Breeze",
            "Corrode",
            "Fracture",
            "Haven",
            "Icebox",
            "Lotus",
            "Pearl",
            "Split",
            "Summit",
            "Sunset"
        ];

        Assert.Equal(expectedNames, options.Select(option => option.Name));
        Assert.Equal(13, options.Select(option => option.MapId).Distinct().Count());
        Assert.All(options, option => Assert.True(option.MapId > 0));
        Assert.Empty(context.ChangeTracker.Entries<Map>());
    }

    [Fact]
    public async Task GetOptionsAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        await using SqliteTestDatabase database = new();
        await using var context = database.CreateContext();
        MapCatalogService service = new(context);
        using CancellationTokenSource cancellationTokenSource = new();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetOptionsAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public void Contract_ExposesOnlyReadOperation()
    {
        var methods = typeof(IMapCatalogService).GetMethods();

        var method = Assert.Single(methods);

        Assert.Equal(nameof(IMapCatalogService.GetOptionsAsync), method.Name);
    }
}