namespace EsportTeamManager.Application.Activities;

public sealed class DeleteActivityRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public Guid ActivityId { get; }

    public DeleteActivityRequest(Guid actorUserId, Guid teamId, Guid activityId)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        ActivityId = activityId;
    }
}