namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityCalendarEventViewModel
{
    public Guid Id { get; }

    public string Title { get; }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public string? Subtitle { get; }

    public string TimeZoneId { get; }

    public string Status { get; }

    public ActivityCalendarEventViewModel(Guid id, string title, DateTimeOffset start, DateTimeOffset end, string? subtitle, string timeZoneId, string status)
    {
        Id = id;
        Title = title;
        Start = start;
        End = end;
        Subtitle = subtitle;
        TimeZoneId = timeZoneId;
        Status = status;
    }
}