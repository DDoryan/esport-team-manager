namespace EsportTeamManager.Application.Notifications;

public sealed class ResolveInvitationRequest
{
    public Guid ActorUserId { get; }

    public Guid InvitationId { get; }

    public ResolveInvitationRequest(Guid actorUserId, Guid invitationId)
    {
        ActorUserId = actorUserId;
        InvitationId = invitationId;
    }
}