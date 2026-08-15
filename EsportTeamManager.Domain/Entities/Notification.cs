using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class Notification
{
    public Guid NotificationId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public Guid? InvitationId { get; private set; }

    public Guid? OwnershipTransferId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    private Notification()
    {
    }

    private Notification(Guid notificationId, Guid recipientUserId, Guid? invitationId, Guid? ownershipTransferId, DateTimeOffset createdAtUtc)
    {
        if (notificationId == Guid.Empty)
        {
            throw new DomainException("The notification identifier cannot be empty.");
        }

        if (recipientUserId == Guid.Empty)
        {
            throw new DomainException("The recipient identifier cannot be empty.");
        }

        bool hasInvitation = invitationId.HasValue;
        bool hasOwnershipTransfer = ownershipTransferId.HasValue;

        if (hasInvitation == hasOwnershipTransfer)
        {
            throw new DomainException("A notification must reference exactly one source.");
        }

        NotificationId = notificationId;
        RecipientUserId = recipientUserId;
        InvitationId = invitationId;
        OwnershipTransferId = ownershipTransferId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public static Notification CreateForInvitation(Guid notificationId, Guid recipientUserId, Guid invitationId, DateTimeOffset createdAtUtc)
    {
        if (invitationId == Guid.Empty)
        {
            throw new DomainException("The invitation identifier cannot be empty.");
        }

        return new Notification(notificationId, recipientUserId, invitationId, null, createdAtUtc);
    }

    public static Notification CreateForOwnershipTransfer(Guid notificationId, Guid recipientUserId, Guid ownershipTransferId, DateTimeOffset createdAtUtc)
    {
        if (ownershipTransferId == Guid.Empty)
        {
            throw new DomainException("The ownership transfer identifier cannot be empty.");
        }

        return new Notification(notificationId, recipientUserId, null, ownershipTransferId, createdAtUtc);
    }

    public void MarkAsRead(DateTimeOffset readAtUtc)
    {
        if (ReadAtUtc.HasValue)
        {
            return;
        }

        DateTimeOffset normalizedDate = readAtUtc.ToUniversalTime();

        if (normalizedDate < CreatedAtUtc)
        {
            throw new DomainException("The reading date cannot precede the notification creation date.");
        }

        ReadAtUtc = normalizedDate;
    }
}