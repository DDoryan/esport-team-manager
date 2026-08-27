namespace EsportTeamManager.Application.Notifications;

public interface IUserNotificationService
{
    Task<InvitationActionResult> AcceptInvitationAsync(ResolveInvitationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserNotificationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<InvitationActionResult> RefuseInvitationAsync(ResolveInvitationRequest request, CancellationToken cancellationToken = default);
}