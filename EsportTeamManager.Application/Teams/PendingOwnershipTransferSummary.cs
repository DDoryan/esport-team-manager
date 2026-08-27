namespace EsportTeamManager.Application.Teams;

public sealed class PendingOwnershipTransferSummary
{
    public Guid OwnershipTransferId { get; }

    public Guid RecipientMembershipId { get; }

    public string RecipientPseudo { get; }

    public string RecipientTag { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public PendingOwnershipTransferSummary(Guid ownershipTransferId, Guid recipientMembershipId, string recipientPseudo, string recipientTag, DateTimeOffset createdAtUtc)
    {
        OwnershipTransferId = ownershipTransferId;
        RecipientMembershipId = recipientMembershipId;
        RecipientPseudo = recipientPseudo;
        RecipientTag = recipientTag;
        CreatedAtUtc = createdAtUtc;
    }
}