namespace EsportTeamManager.Application.Teams;

public sealed class InitiateOwnershipTransferRequest
{
    public Guid InitiatorUserId { get; }

    public Guid TeamId { get; }

    public Guid RecipientMembershipId { get; }

    public InitiateOwnershipTransferRequest(Guid initiatorUserId, Guid teamId, Guid recipientMembershipId)
    {
        InitiatorUserId = initiatorUserId;
        TeamId = teamId;
        RecipientMembershipId = recipientMembershipId;
    }
}