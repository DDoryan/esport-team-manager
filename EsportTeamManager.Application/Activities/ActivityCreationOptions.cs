namespace EsportTeamManager.Application.Activities;

public sealed class ActivityCreationOptions
{
    public Guid TeamId { get; }

    public string TeamName { get; }

    public string TimeZoneId { get; }

    public IReadOnlyCollection<ActivityTypeOption> ActivityTypes { get; }

    public IReadOnlyCollection<ActivityParticipantOption> Participants { get; }

    public ActivityCreationOptions(Guid teamId, string teamName, string timeZoneId, IReadOnlyCollection<ActivityTypeOption> activityTypes, IReadOnlyCollection<ActivityParticipantOption> participants)
    {
        TeamId = teamId;
        TeamName = teamName;
        TimeZoneId = timeZoneId;
        ActivityTypes = activityTypes;
        Participants = participants;
    }
}