namespace EsportTeamManager.Web.Models.Notifications;

public sealed class NotificationsIndexViewModel
{
    public IReadOnlyCollection<NotificationItemViewModel> Notifications { get; }

    public NotificationsIndexViewModel(IReadOnlyCollection<NotificationItemViewModel> notifications)
    {
        Notifications = notifications;
    }
}