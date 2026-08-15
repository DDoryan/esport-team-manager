using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class TeamTests
    {
        [Fact]
        public void Constructor_WithValidValues_CreatesTeam()
        {
            Guid teamId = Guid.NewGuid();
            Guid ownerUserId = Guid.NewGuid();
            DateTimeOffset createdAt = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(2));

            Team team = new Team(teamId, ownerUserId, "  Phoenix Academy  ", createdAt, "  PHX  ", "  Amateur Valorant team  ");

            Assert.Equal(teamId, team.TeamId);
            Assert.Equal(ownerUserId, team.OwnerUserId);
            Assert.Equal("Phoenix Academy", team.Name);
            Assert.Equal("PHX", team.Tag);
            Assert.Equal("Amateur Valorant team", team.Description);
            Assert.Equal("Europe/Paris", team.TimeZoneId);
            Assert.Equal(createdAt.ToUniversalTime(), team.CreatedAtUtc);
            Assert.Equal(0, team.DeletedMemberCounter);
        }

        [Fact]
        public void Constructor_WithInvalidName_ThrowsDomainException()
        {
            DomainException exception = Assert.Throws<DomainException>(() => new Team(Guid.NewGuid(), Guid.NewGuid(), "AB", DateTimeOffset.UtcNow));

            Assert.Equal("The team name must contain between 3 and 50 characters.", exception.Message);
        }

        [Fact]
        public void UpdateInformation_WithValidValues_UpdatesTeam()
        {
            Team team = new Team(Guid.NewGuid(), Guid.NewGuid(), "Initial team", DateTimeOffset.UtcNow);

            team.UpdateInformation("Updated team", "UPD", "Updated description", "Europe/London");

            Assert.Equal("Updated team", team.Name);
            Assert.Equal("UPD", team.Tag);
            Assert.Equal("Updated description", team.Description);
            Assert.Equal("Europe/London", team.TimeZoneId);
        }

        [Fact]
        public void TransferOwnership_WithValidUser_ChangesOwner()
        {
            Guid initialOwnerUserId = Guid.NewGuid();
            Guid newOwnerUserId = Guid.NewGuid();
            Team team = new Team(Guid.NewGuid(), initialOwnerUserId, "Phoenix Academy", DateTimeOffset.UtcNow);

            team.TransferOwnership(newOwnerUserId);

            Assert.Equal(newOwnerUserId, team.OwnerUserId);
        }

        [Fact]
        public void TransferOwnership_ToCurrentOwner_ThrowsDomainException()
        {
            Guid ownerUserId = Guid.NewGuid();
            Team team = new Team(Guid.NewGuid(), ownerUserId, "Phoenix Academy", DateTimeOffset.UtcNow);

            DomainException exception = Assert.Throws<DomainException>(() => team.TransferOwnership(ownerUserId));

            Assert.Equal("The selected user already owns the team.", exception.Message);
        }

        [Fact]
        public void ReserveDeletedMemberNumber_CalledTwice_ReturnsSequentialNumbers()
        {
            Team team = new Team(Guid.NewGuid(), Guid.NewGuid(), "Phoenix Academy", DateTimeOffset.UtcNow);

            int firstNumber = team.ReserveDeletedMemberNumber();
            int secondNumber = team.ReserveDeletedMemberNumber();

            Assert.Equal(1, firstNumber);
            Assert.Equal(2, secondNumber);
            Assert.Equal(2, team.DeletedMemberCounter);
        }
    }
}