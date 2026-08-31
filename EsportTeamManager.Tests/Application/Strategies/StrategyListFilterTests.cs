using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Tests.Application.Strategies;

public sealed class StrategyListFilterTests
{
    [Fact]
    public void Constructor_WithEmptySearchText_NormalizesToNull()
    {
        StrategyListFilter filter = new(null, null, null, "   ");

        Assert.Null(filter.SearchText);
    }

    [Fact]
    public void Constructor_WithSearchText_TrimsValue()
    {
        StrategyListFilter filter = new(null, null, null, "  Retake  ");

        Assert.Equal("Retake", filter.SearchText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveMapIdentifier_ThrowsArgumentException(int mapId)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new StrategyListFilter(mapId, null, null, null));

        Assert.Equal("mapId", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithInvalidSide_ThrowsArgumentException()
    {
        StrategySide invalidSide = (StrategySide)999;

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new StrategyListFilter(null, invalidSide, null, null));

        Assert.Equal("side", exception.ParamName);
    }
}