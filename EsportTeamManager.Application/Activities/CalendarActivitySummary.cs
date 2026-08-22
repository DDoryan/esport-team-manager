using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Activities;

public sealed class CalendarActivitySummary
{
    public Guid ActivityId { get; }

    public string TypeCode { get; }

    public string TypeLabel { get; }

    public string? Subtitle { get; }

    public string? OpponentName { get; }

    public DateTimeOffset PlannedStartUtc { get; }

    public DateTimeOffset PlannedEndUtc { get; }

    public string TimeZoneId { get; }

    public ActivityStatus Status { get; }

    public CalendarActivitySummary(Guid activityId, string typeCode, string typeLabel, string? subtitle, string? opponentName, DateTimeOffset plannedStartUtc, DateTimeOffset plannedEndUtc, string timeZoneId, ActivityStatus status)
    {
        ActivityId = activityId;
        TypeCode = typeCode;
        TypeLabel = typeLabel;
        Subtitle = subtitle;
        OpponentName = opponentName;
        PlannedStartUtc = plannedStartUtc;
        PlannedEndUtc = plannedEndUtc;
        TimeZoneId = timeZoneId;
        Status = status;
    }
}