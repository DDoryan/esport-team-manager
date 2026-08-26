namespace EsportTeamManager.Application.Teams;

public sealed class TeamRoleOption
{
    public int TeamRoleId { get; }

    public string Label { get; }

    public TeamRoleOption(int teamRoleId, string label)
    {
        TeamRoleId = teamRoleId;
        Label = label;
    }
}