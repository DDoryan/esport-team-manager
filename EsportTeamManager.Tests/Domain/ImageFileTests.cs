using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class ImageFileTests
    {
        [Fact]
        public void CreateTeamLogo_WithValidValues_CreatesTeamImage()
        {
            Guid teamId = Guid.NewGuid();
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;

            ImageFile image = ImageFile.CreateTeamLogo(teamId, "generated-logo.webp", "original-logo.png", "image/png", 150000, 512, 512, "teams/logo/generated-logo.webp", "teams/logo/generated-logo-thumbnail.webp", nowUtc);

            Assert.NotEqual(Guid.Empty, image.ImageFileId);
            Assert.Equal(teamId, image.TeamLogoForTeamId);
            Assert.Null(image.StrategyImageForStrategyId);
            Assert.Equal("image/png", image.MediaType);
            Assert.Equal(150000, image.FileSizeBytes);
            Assert.Equal(nowUtc, image.CreatedAtUtc);
        }

        [Fact]
        public void CreateStrategyImage_WithValidValues_CreatesStrategyImage()
        {
            Guid strategyId = Guid.NewGuid();

            ImageFile image = ImageFile.CreateStrategyImage(strategyId, "generated-strategy.webp", "strategy.jpg", "image/jpeg", 250000, 1280, 720, "strategies/generated-strategy.webp", "strategies/generated-strategy-thumbnail.webp", DateTimeOffset.UtcNow);

            Assert.Null(image.TeamLogoForTeamId);
            Assert.Equal(strategyId, image.StrategyImageForStrategyId);
            Assert.Equal("image/jpeg", image.MediaType);
            Assert.Equal(1280, image.WidthPixels);
            Assert.Equal(720, image.HeightPixels);
        }

        [Fact]
        public void CreateTeamLogo_WithUnsupportedMediaType_ThrowsDomainException()
        {
            DomainException exception = Assert.Throws<DomainException>(() => ImageFile.CreateTeamLogo(Guid.NewGuid(), "generated-logo.gif", "logo.gif", "image/gif", 150000, 512, 512, "teams/logo/generated-logo.gif", "teams/logo/generated-logo-thumbnail.gif", DateTimeOffset.UtcNow));

            Assert.Equal("The image format must be PNG, JPEG or WebP.", exception.Message);
        }

        [Fact]
        public void CreateStrategyImage_WithInvalidDimensions_ThrowsDomainException()
        {
            DomainException exception = Assert.Throws<DomainException>(() => ImageFile.CreateStrategyImage(Guid.NewGuid(), "generated-strategy.webp", "strategy.webp", "image/webp", 250000, 0, 720, "strategies/generated-strategy.webp", "strategies/generated-strategy-thumbnail.webp", DateTimeOffset.UtcNow));

            Assert.Equal("The image dimensions must be positive.", exception.Message);
        }
    }
}