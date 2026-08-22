namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityCalendarEventViewModel
{
    public Guid Id { get; }

    public string Title { get; }

    public string TypeCode { get; }

    public string? Subtitle { get; }

    public string? OpponentName { get; }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public string TimeZoneId { get; }

    public string Status { get; }

    public ActivityCalendarEventViewModel(Guid id, string title, string typeCode, string? subtitle, string? opponentName, DateTimeOffset start, DateTimeOffset end, string timeZoneId, string status)
    {
        Id = id;
        Title = title;
        TypeCode = typeCode;
        Subtitle = subtitle;
        OpponentName = opponentName;
        Start = start;
        End = end;
        TimeZoneId = timeZoneId;
        Status = status;
    }
}