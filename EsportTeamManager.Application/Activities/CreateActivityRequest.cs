namespace EsportTeamManager.Application.Activities;

public sealed class CreateActivityRequest
{
    public Guid UserId { get; }

    public Guid TeamId { get; }

    public int ActivityTypeId { get; }

    public DateTime PlannedStartLocal { get; }

    public DateTime PlannedEndLocal { get; }

    public IReadOnlyCollection<Guid> ParticipantMembershipIds { get; }

    public string? Subtitle { get; }

    public string? Description { get; }

    public string? OpponentName { get; }

    public IReadOnlyCollection<CreateActivityLinkRequest> Links { get; }

    public CreateActivityRequest(Guid userId, Guid teamId, int activityTypeId, DateTime plannedStartLocal, DateTime plannedEndLocal, IReadOnlyCollection<Guid> participantMembershipIds, string? subtitle, string? description, string? opponentName = null, IReadOnlyCollection<CreateActivityLinkRequest>? links = null)
    {
        UserId = userId;
        TeamId = teamId;
        ActivityTypeId = activityTypeId;
        PlannedStartLocal = plannedStartLocal;
        PlannedEndLocal = plannedEndLocal;
        ParticipantMembershipIds = participantMembershipIds;
        Subtitle = subtitle;
        Description = description;
        OpponentName = opponentName;
        Links = links?.ToArray() ?? [];
    }
}