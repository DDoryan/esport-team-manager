namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityListItemViewModel
{
    public Guid ActivityId { get; init; }

    public string TypeLabel { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public DateTimeOffset PlannedStartUtc { get; init; }

    public DateTimeOffset PlannedEndUtc { get; init; }

    public string TimeZoneId { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;
}