namespace EsportTeamManager.Application.Teams;

public sealed class RemoveTeamMemberRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public Guid TeamMembershipId { get; }

    public RemoveTeamMemberRequest(Guid actorUserId, Guid teamId, Guid teamMembershipId)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        TeamMembershipId = teamMembershipId;
    }
}