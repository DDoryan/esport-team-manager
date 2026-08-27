namespace EsportTeamManager.Web.Models.Teams;

public sealed class LeaveTeamViewModel
{
    public Guid TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;
}