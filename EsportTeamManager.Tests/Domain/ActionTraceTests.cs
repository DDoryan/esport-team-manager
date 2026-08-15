using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class ActionTraceTests
    {
        [Fact]
        public void Constructor_WithValidValues_CreatesTraceWithSixMonthExpiration()
        {
            Guid actorUserId = Guid.NewGuid();
            Guid teamId = Guid.NewGuid();
            DateTimeOffset occurredAtUtc = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

            ActionTrace trace = new ActionTrace(actorUserId, teamId, "TEAM_DELETED", "Team", teamId.ToString(), TraceOutcome.Succeeded, occurredAtUtc);

            Assert.NotEqual(Guid.Empty, trace.ActionTraceId);
            Assert.Equal(actorUserId, trace.ActorUserId);
            Assert.Equal(teamId, trace.TeamId);
            Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
            Assert.Equal(occurredAtUtc.AddMonths(6), trace.ExpiresAtUtc);
        }

        [Fact]
        public void RemoveActor_RemovesUserReference()
        {
            ActionTrace trace = new ActionTrace(Guid.NewGuid(), Guid.NewGuid(), "TEAM_UPDATED", "Team", Guid.NewGuid().ToString(), TraceOutcome.Succeeded, DateTimeOffset.UtcNow);

            trace.RemoveActor();

            Assert.Null(trace.ActorUserId);
        }

        [Fact]
        public void RemoveTeam_RemovesTeamReference()
        {
            ActionTrace trace = new ActionTrace(Guid.NewGuid(), Guid.NewGuid(), "TEAM_DELETED", "Team", Guid.NewGuid().ToString(), TraceOutcome.Succeeded, DateTimeOffset.UtcNow);

            trace.RemoveTeam();

            Assert.Null(trace.TeamId);
        }

        [Fact]
        public void Constructor_WithActionCodeTooLong_ThrowsDomainException()
        {
            string actionCode = new string('A', 61);

            DomainException exception = Assert.Throws<DomainException>(() => new ActionTrace(Guid.NewGuid(), Guid.NewGuid(), actionCode, "Team", null, TraceOutcome.Failed, DateTimeOffset.UtcNow));

            Assert.Equal("The action code is required and cannot exceed 60 characters.", exception.Message);
        }
    }
}