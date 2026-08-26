namespace EsportTeamManager.Application.Teams;

public sealed class InviteTeamMemberRequest
{
    public Guid SenderUserId { get; }

    public Guid TeamId { get; }

    public string RecipientIdentity { get; }

    public int ProposedTeamRoleId { get; }

    public InviteTeamMemberRequest(Guid senderUserId, Guid teamId, string recipientIdentity, int proposedTeamRoleId)
    {
        SenderUserId = senderUserId;
        TeamId = teamId;
        RecipientIdentity = recipientIdentity;
        ProposedTeamRoleId = proposedTeamRoleId;
    }
}