namespace RepriseWeb.ViewModels.Activities;

public sealed class TeamCalendarViewModel
{
    public Guid TeamId { get; }

    public string TeamName { get; }

    public string TimeZoneId { get; }

    public bool CanCreateActivity { get; }

    public IReadOnlyCollection<ActivityCalendarParticipantOptionViewModel> ParticipantOptions { get; }

    public TeamCalendarViewModel(Guid teamId, string teamName, string timeZoneId, bool canCreateActivity, IReadOnlyCollection<ActivityCalendarParticipantOptionViewModel> participantOptions)
    {
        ArgumentNullException.ThrowIfNull(participantOptions);

        TeamId = teamId;
        TeamName = teamName;
        TimeZoneId = timeZoneId;
        CanCreateActivity = canCreateActivity;
        ParticipantOptions = participantOptions.ToArray();
    }
}