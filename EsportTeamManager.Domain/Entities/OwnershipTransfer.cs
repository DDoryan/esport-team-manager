using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class OwnershipTransfer
{
    public Guid OwnershipTransferId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid InitiatorMembershipId { get; private set; }

    public Guid RecipientMembershipId { get; private set; }

    public RequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    private OwnershipTransfer()
    {
    }

    public OwnershipTransfer(Guid ownershipTransferId, Guid teamId, Guid initiatorMembershipId, Guid recipientMembershipId, DateTimeOffset createdAtUtc)
    {
        if (ownershipTransferId == Guid.Empty)
        {
            throw new DomainException("The ownership transfer identifier cannot be empty.");
        }

        if (teamId == Guid.Empty)
        {
            throw new DomainException("The team identifier cannot be empty.");
        }

        if (initiatorMembershipId == Guid.Empty)
        {
            throw new DomainException("The initiator membership identifier cannot be empty.");
        }

        if (recipientMembershipId == Guid.Empty)
        {
            throw new DomainException("The recipient membership identifier cannot be empty.");
        }

        if (initiatorMembershipId == recipientMembershipId)
        {
            throw new DomainException("The initiator and recipient memberships must be different.");
        }

        OwnershipTransferId = ownershipTransferId;
        TeamId = teamId;
        InitiatorMembershipId = initiatorMembershipId;
        RecipientMembershipId = recipientMembershipId;
        Status = RequestStatus.Pending;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public void Accept(DateTimeOffset resolvedAtUtc)
    {
        Resolve(RequestStatus.Accepted, resolvedAtUtc);
    }

    public void Refuse(DateTimeOffset resolvedAtUtc)
    {
        Resolve(RequestStatus.Refused, resolvedAtUtc);
    }

    public void Cancel(DateTimeOffset resolvedAtUtc)
    {
        Resolve(RequestStatus.Cancelled, resolvedAtUtc);
    }

    private void Resolve(RequestStatus finalStatus, DateTimeOffset resolvedAtUtc)
    {
        if (Status != RequestStatus.Pending)
        {
            throw new DomainException("Only a pending ownership transfer can be resolved.");
        }

        if (finalStatus == RequestStatus.Pending)
        {
            throw new DomainException("The final ownership transfer status cannot be pending.");
        }

        DateTimeOffset normalizedDate = resolvedAtUtc.ToUniversalTime();

        if (normalizedDate < CreatedAtUtc)
        {
            throw new DomainException("The resolution date cannot precede the ownership transfer creation date.");
        }

        Status = finalStatus;
        ResolvedAtUtc = normalizedDate;
    }
}