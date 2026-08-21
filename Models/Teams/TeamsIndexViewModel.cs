namespace EsportTeamManager.Web.Models.Teams;

public sealed class TeamsIndexViewModel
{
    public IReadOnlyCollection<TeamCardViewModel> Teams { get; }

    public CreateTeamViewModel CreateTeam { get; }

    public TeamsIndexViewModel(IReadOnlyCollection<TeamCardViewModel> teams, CreateTeamViewModel createTeam)
    {
        Teams = teams;
        CreateTeam = createTeam;
    }
}