using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class Invitation
{
    public Guid InvitationId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public int ProposedTeamRoleId { get; private set; }

    public Guid? CreatedMembershipId { get; private set; }

    public RequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    private Invitation()
    {
    }

    public Invitation(Guid invitationId, Guid teamId, Guid senderUserId, Guid recipientUserId, int proposedTeamRoleId, DateTimeOffset createdAtUtc)
    {
        if (invitationId == Guid.Empty)
        {
            throw new DomainException("The invitation identifier cannot be empty.");
        }

        if (teamId == Guid.Empty)
        {
            throw new DomainException("The team identifier cannot be empty.");
        }

        if (senderUserId == Guid.Empty)
        {
            throw new DomainException("The sender identifier cannot be empty.");
        }

        if (recipientUserId == Guid.Empty)
        {
            throw new DomainException("The recipient identifier cannot be empty.");
        }

        if (proposedTeamRoleId <= 0)
        {
            throw new DomainException("The proposed team role identifier must be positive.");
        }

        InvitationId = invitationId;
        TeamId = teamId;
        SenderUserId = senderUserId;
        RecipientUserId = recipientUserId;
        ProposedTeamRoleId = proposedTeamRoleId;
        Status = RequestStatus.Pending;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public void Accept(Guid createdMembershipId, DateTimeOffset resolvedAtUtc)
    {
        if (createdMembershipId == Guid.Empty)
        {
            throw new DomainException("The created membership identifier cannot be empty.");
        }

        Resolve(RequestStatus.Accepted, resolvedAtUtc);
        CreatedMembershipId = createdMembershipId;
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
            throw new DomainException("Only a pending invitation can be resolved.");
        }

        if (finalStatus == RequestStatus.Pending)
        {
            throw new DomainException("The final invitation status cannot be pending.");
        }

        DateTimeOffset normalizedDate = resolvedAtUtc.ToUniversalTime();

        if (normalizedDate < CreatedAtUtc)
        {
            throw new DomainException("The resolution date cannot precede the invitation creation date.");
        }

        Status = finalStatus;
        ResolvedAtUtc = normalizedDate;
    }
}