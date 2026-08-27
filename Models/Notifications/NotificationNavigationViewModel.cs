namespace EsportTeamManager.Web.Models.Notifications;

public sealed class NotificationNavigationViewModel
{
    public int UnreadCount { get; }

    public IReadOnlyCollection<NotificationItemViewModel> Notifications { get; }

    public NotificationNavigationViewModel(int unreadCount, IReadOnlyCollection<NotificationItemViewModel> notifications)
    {
        UnreadCount = unreadCount;
        Notifications = notifications;
    }
}