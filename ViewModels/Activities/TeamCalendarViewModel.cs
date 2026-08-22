namespace RepriseWeb.ViewModels.Activities;

public sealed class TeamCalendarViewModel
{
    public Guid TeamId { get; }

    public string TeamName { get; }

    public string TimeZoneId { get; }

    public TeamCalendarViewModel(Guid teamId, string teamName, string timeZoneId)
    {
        TeamId = teamId;
        TeamName = teamName;
        TimeZoneId = timeZoneId;
    }
}