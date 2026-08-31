using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain;

public sealed class MapTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesMapWithTrimmedName()
    {
        Map map = new(1, "  Ascent  ");

        Assert.Equal(1, map.MapId);
        Assert.Equal("Ascent", map.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveIdentifier_ThrowsDomainException(int mapId)
    {
        DomainException exception = Assert.Throws<DomainException>(() => new Map(mapId, "Ascent"));

        Assert.Equal("The map identifier must be positive.", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyName_ThrowsDomainException(string name)
    {
        DomainException exception = Assert.Throws<DomainException>(() => new Map(1, name));

        Assert.Equal("The map name must contain between 1 and 50 characters.", exception.Message);
    }

    [Fact]
    public void Constructor_WithNameLongerThanMaximum_ThrowsDomainException()
    {
        string name = new('A', 51);

        DomainException exception = Assert.Throws<DomainException>(() => new Map(1, name));

        Assert.Equal("The map name must contain between 1 and 50 characters.", exception.Message);
    }
}