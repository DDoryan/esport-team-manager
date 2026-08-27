namespace EsportTeamManager.Web.Models.Teams;

public sealed class PendingOwnershipTransferViewModel
{
    public Guid OwnershipTransferId { get; }

    public Guid RecipientMembershipId { get; }

    public string RecipientPseudo { get; }

    public string RecipientTag { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public PendingOwnershipTransferViewModel(Guid ownershipTransferId, Guid recipientMembershipId, string recipientPseudo, string recipientTag, DateTimeOffset createdAtUtc)
    {
        OwnershipTransferId = ownershipTransferId;
        RecipientMembershipId = recipientMembershipId;
        RecipientPseudo = recipientPseudo;
        RecipientTag = recipientTag;
        CreatedAtUtc = createdAtUtc;
    }
}