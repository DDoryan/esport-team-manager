using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Activities;

public sealed class CalendarActivitySummary
{
    public Guid ActivityId { get; }

    public string TypeLabel { get; }

    public string? Subtitle { get; }

    public DateTimeOffset PlannedStartUtc { get; }

    public DateTimeOffset PlannedEndUtc { get; }

    public string TimeZoneId { get; }

    public ActivityStatus Status { get; }

    public CalendarActivitySummary(Guid activityId, string typeLabel, string? subtitle, DateTimeOffset plannedStartUtc, DateTimeOffset plannedEndUtc, string timeZoneId, ActivityStatus status)
    {
        ActivityId = activityId;
        TypeLabel = typeLabel;
        Subtitle = subtitle;
        PlannedStartUtc = plannedStartUtc;
        PlannedEndUtc = plannedEndUtc;
        TimeZoneId = timeZoneId;
        Status = status;
    }
}