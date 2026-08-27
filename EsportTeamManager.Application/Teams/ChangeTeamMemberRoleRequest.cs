namespace EsportTeamManager.Application.Teams;

public sealed class ChangeTeamMemberRoleRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public Guid TeamMembershipId { get; }

    public int NewTeamRoleId { get; }

    public ChangeTeamMemberRoleRequest(Guid actorUserId, Guid teamId, Guid teamMembershipId, int newTeamRoleId)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        TeamMembershipId = teamMembershipId;
        NewTeamRoleId = newTeamRoleId;
    }
}