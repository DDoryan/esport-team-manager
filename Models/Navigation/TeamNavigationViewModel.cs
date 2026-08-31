namespace EsportTeamManager.Web.Models.Navigation;

public sealed class TeamNavigationViewModel
{
    public TeamNavigationItemViewModel? ActiveTeam { get; }

    public IReadOnlyCollection<TeamNavigationItemViewModel> Teams { get; }

    public bool CalendarIsActive { get; }

    public bool ManagementIsActive { get; }

    public bool StrategiesIsActive { get; }

    public TeamNavigationViewModel(TeamNavigationItemViewModel? activeTeam, IReadOnlyCollection<TeamNavigationItemViewModel> teams, bool calendarIsActive, bool managementIsActive, bool strategiesIsActive)
    {
        ActiveTeam = activeTeam;
        Teams = teams;
        CalendarIsActive = calendarIsActive;
        ManagementIsActive = managementIsActive;
        StrategiesIsActive = strategiesIsActive;
    }
}