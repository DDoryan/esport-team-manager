using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using Xunit;

namespace EsportTeamManager.Tests.Domain
{
    public sealed class TeamActivityTests
    {
        [Fact]
        public void Constructor_WithMeetingAndParticipant_CreatesPlannedActivity()
        {
            DateTimeOffset createdAtUtc = new DateTimeOffset(2026, 8, 15, 8, 0, 0, TimeSpan.Zero);
            DateTimeOffset startUtc = createdAtUtc.AddHours(2);
            DateTimeOffset endUtc = startUtc.AddHours(1);
            Guid participantMembershipId = Guid.NewGuid();
            ActivityType activityType = new ActivityType(3, "Meeting", "Réunion", true);

            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), startUtc, endUtc, "Europe/Paris", [participantMembershipId], createdAtUtc, "Weekly review", "Review of the previous matches.");

            Assert.Equal(ActivityStatus.Planned, activity.Status);
            Assert.Equal("Meeting", activity.ActivityType.Code);
            Assert.Equal("Weekly review", activity.Subtitle);
            Assert.Equal(startUtc, activity.PlannedStartUtc);
            Assert.Equal(endUtc, activity.PlannedEndUtc);
            Assert.Equal("Europe/Paris", activity.TimeZoneId);
            Assert.Single(activity.Participants);
            Assert.Null(activity.MatchDetail);
            Assert.False(activity.RequiresScores);
        }

        [Fact]
        public void Constructor_WithoutParticipant_ThrowsDomainException()
        {
            DateTimeOffset startUtc = DateTimeOffset.UtcNow.AddHours(1);
            ActivityType activityType = new ActivityType(3, "Meeting", "Réunion", true);

            DomainException exception = Assert.Throws<DomainException>(() => new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), startUtc, startUtc.AddHours(1), "Europe/Paris", [], DateTimeOffset.UtcNow));

            Assert.Equal("An activity must contain at least one participant.", exception.Message);
        }

        [Fact]
        public void Constructor_WithEndBeforeStart_ThrowsDomainException()
        {
            DateTimeOffset startUtc = DateTimeOffset.UtcNow.AddHours(2);
            ActivityType activityType = new ActivityType(3, "Meeting", "Réunion", true);

            DomainException exception = Assert.Throws<DomainException>(() => new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), startUtc, startUtc.AddMinutes(-30), "Europe/Paris", [Guid.NewGuid()], DateTimeOffset.UtcNow));

            Assert.Equal("The planned end time must be later than the planned start time.", exception.Message);
        }

        [Fact]
        public void Constructor_WithPracc_CreatesMatchDetail()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            DateTimeOffset startUtc = createdAtUtc.AddHours(1);
            ActivityType activityType = new ActivityType(1, "Pracc", "Pracc", true);

            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), startUtc, startUtc.AddHours(2), "Europe/Paris", [Guid.NewGuid()], createdAtUtc);

            Assert.True(activity.RequiresScores);
            Assert.NotNull(activity.MatchDetail);
            Assert.Equal(activity.ActivityId, activity.MatchDetail.ActivityId);
        }

        [Fact]
        public void ChangeType_FromMeetingToPracc_CreatesMatchDetail()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            ActivityType meetingType = new ActivityType(3, "Meeting", "Réunion", true);
            ActivityType praccType = new ActivityType(1, "Pracc", "Pracc", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), meetingType, Guid.NewGuid(), createdAtUtc.AddHours(1), createdAtUtc.AddHours(2), "Europe/Paris", [Guid.NewGuid()], createdAtUtc);

            activity.ChangeType(praccType, createdAtUtc.AddMinutes(10));

            Assert.Equal(praccType.ActivityTypeId, activity.ActivityTypeId);
            Assert.Equal("Pracc", activity.ActivityType.Code);
            Assert.True(activity.RequiresScores);
            Assert.NotNull(activity.MatchDetail);
            Assert.Equal(activity.ActivityId, activity.MatchDetail.ActivityId);
        }

        [Fact]
        public void ChangeType_FromPraccToMeeting_RemovesMatchDetail()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            ActivityType praccType = new ActivityType(1, "Pracc", "Pracc", true);
            ActivityType meetingType = new ActivityType(3, "Meeting", "Réunion", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), praccType, Guid.NewGuid(), createdAtUtc.AddHours(1), createdAtUtc.AddHours(2), "Europe/Paris", [Guid.NewGuid()], createdAtUtc);

            activity.ChangeType(meetingType, createdAtUtc.AddMinutes(10));

            Assert.Equal(meetingType.ActivityTypeId, activity.ActivityTypeId);
            Assert.Equal("Meeting", activity.ActivityType.Code);
            Assert.False(activity.RequiresScores);
            Assert.Null(activity.MatchDetail);
        }

        [Fact]
        public void ChangeType_CompletedActivityCannotSwitchToMatchType()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            Guid participantMembershipId = Guid.NewGuid();
            ActivityType meetingType = new ActivityType(3, "Meeting", "Réunion", true);
            ActivityType praccType = new ActivityType(1, "Pracc", "Pracc", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), meetingType, Guid.NewGuid(), createdAtUtc.AddHours(1), createdAtUtc.AddHours(2), "Europe/Paris", [participantMembershipId], createdAtUtc);
            Dictionary<Guid, Attendance> attendance = new Dictionary<Guid, Attendance>
            {
                [participantMembershipId] = Attendance.Present
            };

            activity.Complete(attendance, createdAtUtc.AddHours(2));

            DomainException exception = Assert.Throws<DomainException>(() => activity.ChangeType(praccType, createdAtUtc.AddHours(3)));

            Assert.Equal("A completed activity cannot switch between match and non-match types.", exception.Message);
            Assert.Equal("Meeting", activity.ActivityType.Code);
            Assert.Null(activity.MatchDetail);
        }

        [Fact]
        public void Complete_MeetingWithAttendance_CompletesActivity()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            DateTimeOffset startUtc = createdAtUtc.AddHours(1);
            Guid participantMembershipId = Guid.NewGuid();
            ActivityType activityType = new ActivityType(3, "Meeting", "Réunion", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), startUtc, startUtc.AddHours(1), "Europe/Paris", [participantMembershipId], createdAtUtc);
            Dictionary<Guid, Attendance> attendance = new Dictionary<Guid, Attendance>
            {
                [participantMembershipId] = Attendance.Present
            };

            activity.Complete(attendance, startUtc.AddHours(1));

            Assert.Equal(ActivityStatus.Completed, activity.Status);
            Assert.Equal(Attendance.Present, Assert.Single(activity.Participants).Attendance);
        }

        [Fact]
        public void Complete_PraccWithoutScores_ThrowsWithoutSavingAttendance()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            DateTimeOffset startUtc = createdAtUtc.AddHours(1);
            Guid participantMembershipId = Guid.NewGuid();
            ActivityType activityType = new ActivityType(1, "Pracc", "Pracc", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), startUtc, startUtc.AddHours(2), "Europe/Paris", [participantMembershipId], createdAtUtc);
            Dictionary<Guid, Attendance> attendance = new Dictionary<Guid, Attendance>
            {
                [participantMembershipId] = Attendance.Present
            };

            DomainException exception = Assert.Throws<DomainException>(() => activity.Complete(attendance, startUtc.AddHours(2)));

            Assert.Equal("Both match scores are required to complete the activity.", exception.Message);
            Assert.Equal(ActivityStatus.Planned, activity.Status);
            Assert.Null(Assert.Single(activity.Participants).Attendance);
        }

        [Fact]
        public void Complete_PraccWithScores_CompletesActivity()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            DateTimeOffset startUtc = createdAtUtc.AddHours(1);
            Guid participantMembershipId = Guid.NewGuid();
            ActivityType activityType = new ActivityType(1, "Pracc", "Pracc", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), startUtc, startUtc.AddHours(2), "Europe/Paris", [participantMembershipId], createdAtUtc);
            Dictionary<Guid, Attendance> attendance = new Dictionary<Guid, Attendance>
            {
                [participantMembershipId] = Attendance.Present
            };

            activity.SetScores(13, 8, startUtc.AddHours(2));
            activity.Complete(attendance, startUtc.AddHours(2));

            Assert.Equal(ActivityStatus.Completed, activity.Status);
            Assert.Equal(13, activity.MatchDetail!.TeamScore);
            Assert.Equal(8, activity.MatchDetail.OpponentScore);
        }

        [Fact]
        public void ReplaceParticipants_PlannedActivity_AddsAndRemovesParticipantsWithoutAttendance()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            Guid firstMembershipId = Guid.NewGuid();
            Guid retainedMembershipId = Guid.NewGuid();
            Guid addedMembershipId = Guid.NewGuid();
            ActivityType activityType = new ActivityType(3, "Meeting", "Réunion", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), createdAtUtc.AddHours(1), createdAtUtc.AddHours(2), "Europe/Paris", [firstMembershipId, retainedMembershipId], createdAtUtc);
            ActivityParticipant retainedParticipant = activity.Participants.Single(participant => participant.TeamMembershipId == retainedMembershipId);
            DateTimeOffset updatedAtUtc = createdAtUtc.AddMinutes(30);

            activity.ReplaceParticipants([retainedMembershipId, addedMembershipId], updatedAtUtc);

            Assert.Equal(2, activity.Participants.Count);
            Assert.DoesNotContain(activity.Participants, participant => participant.TeamMembershipId == firstMembershipId);
            Assert.Same(retainedParticipant, activity.Participants.Single(participant => participant.TeamMembershipId == retainedMembershipId));
            Assert.Contains(activity.Participants, participant => participant.TeamMembershipId == addedMembershipId);
            Assert.All(activity.Participants, participant => Assert.Null(participant.Attendance));
            Assert.Equal(updatedAtUtc, activity.UpdatedAtUtc);
        }

        [Fact]
        public void UpdateAttendances_CompletedActivity_UpdatesEveryParticipant()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            Guid firstMembershipId = Guid.NewGuid();
            Guid secondMembershipId = Guid.NewGuid();
            ActivityType activityType = new ActivityType(3, "Meeting", "Réunion", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), createdAtUtc.AddHours(1), createdAtUtc.AddHours(2), "Europe/Paris", [firstMembershipId, secondMembershipId], createdAtUtc);
            DateTimeOffset completedAtUtc = createdAtUtc.AddHours(2);

            activity.Complete(
                new Dictionary<Guid, Attendance>
                {
                    [firstMembershipId] = Attendance.Present,
                    [secondMembershipId] = Attendance.Absent
                },
                completedAtUtc);

            DateTimeOffset updatedAtUtc = completedAtUtc.AddMinutes(30);

            activity.UpdateAttendances(
                new Dictionary<Guid, Attendance>
                {
                    [firstMembershipId] = Attendance.Absent,
                    [secondMembershipId] = Attendance.Present
                },
                updatedAtUtc);

            Assert.Equal(Attendance.Absent, activity.Participants.Single(participant => participant.TeamMembershipId == firstMembershipId).Attendance);
            Assert.Equal(Attendance.Present, activity.Participants.Single(participant => participant.TeamMembershipId == secondMembershipId).Attendance);
            Assert.Equal(updatedAtUtc, activity.UpdatedAtUtc);
        }

        [Fact]
        public void UpdateAttendances_WithIncompleteData_ThrowsWithoutChangingAttendance()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            Guid firstMembershipId = Guid.NewGuid();
            Guid secondMembershipId = Guid.NewGuid();
            ActivityType activityType = new ActivityType(3, "Meeting", "Réunion", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), createdAtUtc.AddHours(1), createdAtUtc.AddHours(2), "Europe/Paris", [firstMembershipId, secondMembershipId], createdAtUtc);
            DateTimeOffset completedAtUtc = createdAtUtc.AddHours(2);

            activity.Complete(
                new Dictionary<Guid, Attendance>
                {
                    [firstMembershipId] = Attendance.Present,
                    [secondMembershipId] = Attendance.Absent
                },
                completedAtUtc);

            DomainException exception = Assert.Throws<DomainException>(() => activity.UpdateAttendances(
                new Dictionary<Guid, Attendance>
                {
                    [firstMembershipId] = Attendance.Absent
                },
                completedAtUtc.AddMinutes(30)));

            Assert.Equal("Attendance must be provided for every activity participant.", exception.Message);
            Assert.Equal(Attendance.Present, activity.Participants.Single(participant => participant.TeamMembershipId == firstMembershipId).Attendance);
            Assert.Equal(Attendance.Absent, activity.Participants.Single(participant => participant.TeamMembershipId == secondMembershipId).Attendance);
            Assert.Equal(completedAtUtc, activity.UpdatedAtUtc);
        }

        [Fact]
        public void UpdateAttendances_PlannedActivity_ThrowsWithoutSavingAttendance()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            Guid participantMembershipId = Guid.NewGuid();
            ActivityType activityType = new ActivityType(3, "Meeting", "Réunion", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), createdAtUtc.AddHours(1), createdAtUtc.AddHours(2), "Europe/Paris", [participantMembershipId], createdAtUtc);

            DomainException exception = Assert.Throws<DomainException>(() => activity.UpdateAttendances(
                new Dictionary<Guid, Attendance>
                {
                    [participantMembershipId] = Attendance.Present
                },
                createdAtUtc.AddMinutes(30)));

            Assert.Equal("Attendance can only be corrected on a completed activity.", exception.Message);
            Assert.Null(Assert.Single(activity.Participants).Attendance);
            Assert.Equal(createdAtUtc, activity.UpdatedAtUtc);
        }

        [Fact]
        public void Cancel_PlannedActivity_PreventsFurtherModification()
        {
            DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
            DateTimeOffset startUtc = createdAtUtc.AddHours(1);
            ActivityType activityType = new ActivityType(4, "ReviewVod", "Review VOD", true);
            TeamActivity activity = new TeamActivity(Guid.NewGuid(), Guid.NewGuid(), activityType, Guid.NewGuid(), startUtc, startUtc.AddHours(1), "Europe/Paris", [Guid.NewGuid()], createdAtUtc);

            activity.Cancel("Unavailable players", createdAtUtc.AddMinutes(10));
            DomainException exception = Assert.Throws<DomainException>(() => activity.UpdateTexts("Updated subtitle", null, null, createdAtUtc.AddMinutes(20)));

            Assert.Equal(ActivityStatus.Cancelled, activity.Status);
            Assert.Equal("Unavailable players", activity.CancellationReason);
            Assert.Equal("A cancelled activity cannot be modified.", exception.Message);
        }
    }
}