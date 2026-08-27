using System.Security.Claims;
using EsportTeamManager.Application.Notifications;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Web.Models.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace EsportTeamManager.Web.ViewComponents;

public sealed class NotificationNavigationViewComponent : ViewComponent
{
    private readonly IUserNotificationService _userNotificationService;

    public NotificationNavigationViewComponent(IUserNotificationService userNotificationService)
    {
        _userNotificationService = userNotificationService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Content(string.Empty);
        }

        int unreadCount = await _userNotificationService.GetUnreadCountAsync(userId.Value, HttpContext.RequestAborted);
        IReadOnlyCollection<UserNotificationSummary> notifications = await _userNotificationService.GetForUserAsync(userId.Value, HttpContext.RequestAborted);
        IReadOnlyCollection<NotificationItemViewModel> items =
        [
            .. notifications.Select(notification => new NotificationItemViewModel(
                notification.NotificationId,
                notification.Kind,
                notification.SourceId,
                notification.TeamId,
                notification.TeamName,
                notification.TeamTag,
                notification.ActorPseudo,
                notification.ActorTag,
                notification.ProposedRoleLabel,
                GetStatusLabel(notification.Status),
                notification.Status == RequestStatus.Pending,
                notification.CreatedAtUtc))
        ];
        NotificationNavigationViewModel viewModel = new(unreadCount, items);

        return View(viewModel);
    }

    private Guid? GetCurrentUserId()
    {
        string? userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }

    private static string GetStatusLabel(RequestStatus status)
    {
        return status switch
        {
            RequestStatus.Pending => "En attente",
            RequestStatus.Accepted => "Acceptée",
            RequestStatus.Refused => "Refusée",
            RequestStatus.Cancelled => "Annulée",
            _ => "Inconnue"
        };
    }
}