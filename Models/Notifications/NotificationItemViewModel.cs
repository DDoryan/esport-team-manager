using EsportTeamManager.Application.Notifications;

namespace EsportTeamManager.Web.Models.Notifications;

public sealed class NotificationItemViewModel
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

    public string StatusLabel { get; }

    public bool IsPending { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public NotificationItemViewModel(Guid notificationId, UserNotificationKind kind, Guid sourceId, Guid teamId, string teamName, string? teamTag, string actorPseudo, string actorTag, string? proposedRoleLabel, string statusLabel, bool isPending, DateTimeOffset createdAtUtc)
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
        StatusLabel = statusLabel;
        IsPending = isPending;
        CreatedAtUtc = createdAtUtc;
    }
}