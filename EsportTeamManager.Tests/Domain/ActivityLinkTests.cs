using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class ActivityLinkTests
    {
        [Fact]
        public void Constructor_WithValidValues_CreatesActivityLink()
        {
            Guid activityId = Guid.NewGuid();

            ActivityLink link = new ActivityLink(activityId, "VOD review", "https://example.com/vod");

            Assert.NotEqual(Guid.Empty, link.ActivityLinkId);
            Assert.Equal(activityId, link.ActivityId);
            Assert.Equal("VOD review", link.Name);
            Assert.Equal("https://example.com/vod", link.Url);
        }

        [Fact]
        public void Constructor_WithInvalidUrl_ThrowsDomainException()
        {
            Guid activityId = Guid.NewGuid();

            DomainException exception = Assert.Throws<DomainException>(() => new ActivityLink(activityId, "Invalid link", "example.com"));

            Assert.Equal("The link URL must use HTTP or HTTPS.", exception.Message);
        }

        [Fact]
        public void Update_WithValidValues_ChangesNameAndUrl()
        {
            ActivityLink link = new ActivityLink(Guid.NewGuid(), "Old link", "https://example.com/old");

            link.Update("New link", "https://example.com/new");

            Assert.Equal("New link", link.Name);
            Assert.Equal("https://example.com/new", link.Url);
        }
    }
}