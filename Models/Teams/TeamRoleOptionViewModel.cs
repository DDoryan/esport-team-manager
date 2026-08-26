namespace EsportTeamManager.Web.Models.Teams;

public sealed class TeamRoleOptionViewModel
{
    public int TeamRoleId { get; }

    public string Label { get; }

    public TeamRoleOptionViewModel(int teamRoleId, string label)
    {
        TeamRoleId = teamRoleId;
        Label = label;
    }
}