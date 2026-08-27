using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Notifications;

public sealed class UserNotificationSummary
{
    public Guid NotificationId { get; }

    public UserNotificationKind Kind { get; }

    public Guid SourceId { get; }

    public Guid TeamId { get; }

    public string TeamName { get; }

    public string? TeamTag { get; }

    public string ActorPseudo { get; }

    public string ActorTag { get; }

    public string? ProposedRoleLabel { get; }

    public RequestStatus Status { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? ReadAtUtc { get; }

    public UserNotificationSummary(Guid notificationId, UserNotificationKind kind, Guid sourceId, Guid teamId, string teamName, string? teamTag, string actorPseudo, string actorTag, string? proposedRoleLabel, RequestStatus status, DateTimeOffset createdAtUtc, DateTimeOffset? readAtUtc)
    {
        NotificationId = notificationId;
        Kind = kind;
        SourceId = sourceId;
        TeamId = teamId;
        TeamName = teamName;
        TeamTag = teamTag;
        ActorPseudo = actorPseudo;
        ActorTag = actorTag;
        ProposedRoleLabel = proposedRoleLabel;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        ReadAtUtc = readAtUtc;
    }
}