using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class StrategyTests
    {
        [Fact]
        public void Constructor_WithValidValues_CreatesActiveStrategy()
        {
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;

            Strategy strategy = new Strategy(Guid.NewGuid(), Guid.NewGuid(), 1, "Ascent attack", StrategySide.Attack, "Execute on site A.", "https://example.com/strategy", nowUtc);

            Assert.NotEqual(Guid.Empty, strategy.StrategyId);
            Assert.Equal(1, strategy.MapId);
            Assert.Equal("Ascent attack", strategy.Name);
            Assert.Equal(StrategySide.Attack, strategy.Side);
            Assert.True(strategy.IsActive);
            Assert.Equal(nowUtc, strategy.CreatedAtUtc);
            Assert.Equal(nowUtc, strategy.UpdatedAtUtc);
        }

        [Fact]
        public void Constructor_WithNameTooShort_ThrowsDomainException()
        {
            DomainException exception = Assert.Throws<DomainException>(() => new Strategy(Guid.NewGuid(), Guid.NewGuid(), 1, "A", StrategySide.Attack, "Description", null, DateTimeOffset.UtcNow));

            Assert.Equal("The strategy name must contain between 3 and 100 characters.", exception.Message);
        }

        [Fact]
        public void UpdateContent_WithInvalidUrl_ThrowsDomainException()
        {
            Strategy strategy = new Strategy(Guid.NewGuid(), Guid.NewGuid(), 1, "Ascent attack", StrategySide.Attack, "Description", null, DateTimeOffset.UtcNow);

            DomainException exception = Assert.Throws<DomainException>(() => strategy.UpdateContent("Description", "valoplant.gg/strategy", DateTimeOffset.UtcNow));

            Assert.Equal("The strategy URL must use HTTP or HTTPS.", exception.Message);
        }

        [Fact]
        public void EnsureHasContent_WithoutDescriptionUrlOrImage_ThrowsDomainException()
        {
            Strategy strategy = new Strategy(Guid.NewGuid(), Guid.NewGuid(), 1, "Ascent attack", StrategySide.Attack, null, null, DateTimeOffset.UtcNow);

            DomainException exception = Assert.Throws<DomainException>(() => strategy.EnsureHasContent(false));

            Assert.Equal("A strategy must contain a description, an external URL or an image.", exception.Message);
        }

        [Fact]
        public void SetActive_WithFalse_DeactivatesStrategy()
        {
            Strategy strategy = new Strategy(Guid.NewGuid(), Guid.NewGuid(), 1, "Ascent attack", StrategySide.Attack, "Description", null, DateTimeOffset.UtcNow);
            DateTimeOffset updateDateUtc = DateTimeOffset.UtcNow.AddMinutes(1);

            strategy.SetActive(false, updateDateUtc);

            Assert.False(strategy.IsActive);
            Assert.Equal(updateDateUtc, strategy.UpdatedAtUtc);
        }
    }
}