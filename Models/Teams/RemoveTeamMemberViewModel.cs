namespace EsportTeamManager.Web.Models.Teams;

public sealed class RemoveTeamMemberViewModel
{
    public Guid TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public Guid TeamMembershipId { get; set; }

    public string MemberIdentity { get; set; } = string.Empty;
}