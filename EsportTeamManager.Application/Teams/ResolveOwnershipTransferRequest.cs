namespace EsportTeamManager.Application.Teams;

public sealed class ResolveOwnershipTransferRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public Guid OwnershipTransferId { get; }

    public ResolveOwnershipTransferRequest(Guid actorUserId, Guid teamId, Guid ownershipTransferId)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        OwnershipTransferId = ownershipTransferId;
    }
}