namespace EsportTeamManager.Application.Activities;

public sealed class UpdateActivityRequest
{
    public Guid UserId { get; }

    public Guid TeamId { get; }

    public Guid ActivityId { get; }

    public int ActivityTypeId { get; }

    public DateTime PlannedStartLocal { get; }

    public DateTime PlannedEndLocal { get; }

    public string? Subtitle { get; }

    public string? Description { get; }

    public string? Report { get; }

    public IReadOnlyCollection<UpdateActivityParticipantRequest> Participants { get; }

    public IReadOnlyCollection<UpdateActivityLinkRequest> Links { get; }

    public UpdateActivityRequest(Guid userId, Guid teamId, Guid activityId, int activityTypeId, DateTime plannedStartLocal, DateTime plannedEndLocal, string? subtitle, string? description, string? report, IReadOnlyCollection<UpdateActivityParticipantRequest> participants, IReadOnlyCollection<UpdateActivityLinkRequest> links)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(links);

        UserId = userId;
        TeamId = teamId;
        ActivityId = activityId;
        ActivityTypeId = activityTypeId;
        PlannedStartLocal = plannedStartLocal;
        PlannedEndLocal = plannedEndLocal;
        Subtitle = subtitle;
        Description = description;
        Report = report;
        Participants = participants.ToArray();
        Links = links.ToArray();
    }
}