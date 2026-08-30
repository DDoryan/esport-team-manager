using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class MatchDetailTests
    {
        [Fact]
        public void Constructor_WithOpponent_NormalizesOpponentName()
        {
            MatchDetail matchDetail = new MatchDetail(Guid.NewGuid(), "  Navi  ");

            Assert.Equal("Navi", matchDetail.OpponentName);
            Assert.Null(matchDetail.Result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void UpdateOpponent_WhenOpponentIsMissing_Throws(string? opponentName)
        {
            MatchDetail matchDetail = new MatchDetail(Guid.NewGuid());

            DomainException exception = Assert.Throws<DomainException>(() => matchDetail.UpdateOpponent(opponentName));

            Assert.Equal("The opponent name is required.", exception.Message);
        }

        [Theory]
        [InlineData(13, 8, MatchResult.Victory)]
        [InlineData(8, 13, MatchResult.Defeat)]
        [InlineData(10, 10, MatchResult.Draw)]
        public void SetScores_WithValidScores_CalculatesResult(int teamScore, int opponentScore, MatchResult expectedResult)
        {
            MatchDetail matchDetail = new MatchDetail(Guid.NewGuid(), "Opponent");

            matchDetail.SetScores(teamScore, opponentScore);

            Assert.Equal(teamScore, matchDetail.TeamScore);
            Assert.Equal(opponentScore, matchDetail.OpponentScore);
            Assert.Equal(expectedResult, matchDetail.Result);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, -1)]
        public void SetScores_WhenScoreIsNegative_Throws(int teamScore, int opponentScore)
        {
            MatchDetail matchDetail = new MatchDetail(Guid.NewGuid(), "Opponent");

            DomainException exception = Assert.Throws<DomainException>(() => matchDetail.SetScores(teamScore, opponentScore));

            Assert.Equal("Match scores cannot be negative.", exception.Message);
            Assert.Null(matchDetail.TeamScore);
            Assert.Null(matchDetail.OpponentScore);
        }

        [Fact]
        public void ClearScores_WhenScoresExist_RemovesScoresAndResult()
        {
            MatchDetail matchDetail = new MatchDetail(Guid.NewGuid(), "Opponent");
            matchDetail.SetScores(13, 8);

            matchDetail.ClearScores();

            Assert.Null(matchDetail.TeamScore);
            Assert.Null(matchDetail.OpponentScore);
            Assert.Null(matchDetail.Result);
        }

        [Fact]
        public void EnsureReadyForCompletion_WhenOpponentIsMissing_Throws()
        {
            MatchDetail matchDetail = new MatchDetail(Guid.NewGuid());
            matchDetail.SetScores(13, 8);

            DomainException exception = Assert.Throws<DomainException>(() => matchDetail.EnsureReadyForCompletion());

            Assert.Equal("The opponent name is required to complete the activity.", exception.Message);
        }

        [Fact]
        public void EnsureReadyForCompletion_WhenScoresAreMissing_Throws()
        {
            MatchDetail matchDetail = new MatchDetail(Guid.NewGuid(), "Opponent");

            DomainException exception = Assert.Throws<DomainException>(() => matchDetail.EnsureReadyForCompletion());

            Assert.Equal("Both match scores are required to complete the activity.", exception.Message);
        }
    }
}