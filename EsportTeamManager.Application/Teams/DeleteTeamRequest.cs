namespace EsportTeamManager.Application.Teams;

public sealed class DeleteTeamRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public string ConfirmationName { get; }

    public DeleteTeamRequest(Guid actorUserId, Guid teamId, string confirmationName)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        ConfirmationName = confirmationName;
    }
}