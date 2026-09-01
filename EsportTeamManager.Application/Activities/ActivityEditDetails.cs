using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Activities;

public sealed class ActivityEditDetails
{
    public Guid ActivityId { get; }

    public Guid TeamId { get; }

    public string TeamName { get; }

    public string TimeZoneId { get; }

    public int ActivityTypeId { get; }

    public string TypeCode { get; }

    public string TypeLabel { get; }

    public string? Subtitle { get; }

    public DateTime PlannedStartLocal { get; }

    public DateTime PlannedEndLocal { get; }

    public string? Description { get; }

    public string? Report { get; }

    public ActivityStatus Status { get; }

    public string? CancellationReason { get; }

    public string? OpponentName { get; }

    public int? TeamScore { get; }

    public int? OpponentScore { get; }

    public MatchResult? Result { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public bool CanEdit { get; }

    public IReadOnlyCollection<ActivityTypeOption> ActivityTypes { get; }

    public IReadOnlyCollection<ActivityEditParticipantSummary> Participants { get; }

    public IReadOnlyCollection<ActivityEditLinkSummary> Links { get; }

    public IReadOnlyCollection<ActivityEditStrategySummary> Strategies { get; }

    public ActivityEditDetails(Guid activityId, Guid teamId, string teamName, string timeZoneId, int activityTypeId, string typeCode, string typeLabel, string? subtitle, DateTime plannedStartLocal, DateTime plannedEndLocal, string? description, string? report, ActivityStatus status, string? cancellationReason, string? opponentName, int? teamScore, int? opponentScore, DateTimeOffset updatedAtUtc, bool canEdit, IReadOnlyCollection<ActivityTypeOption> activityTypes, IReadOnlyCollection<ActivityEditParticipantSummary> participants, IReadOnlyCollection<ActivityEditLinkSummary> links, MatchResult? result = null, IReadOnlyCollection<ActivityEditStrategySummary>? strategies = null)
    {
        ActivityId = activityId;
        TeamId = teamId;
        TeamName = teamName;
        TimeZoneId = timeZoneId;
        ActivityTypeId = activityTypeId;
        TypeCode = typeCode;
        TypeLabel = typeLabel;
        Subtitle = subtitle;
        PlannedStartLocal = plannedStartLocal;
        PlannedEndLocal = plannedEndLocal;
        Description = description;
        Report = report;
        Status = status;
        CancellationReason = cancellationReason;
        OpponentName = opponentName;
        TeamScore = teamScore;
        OpponentScore = opponentScore;
        Result = result;
        UpdatedAtUtc = updatedAtUtc;
        CanEdit = canEdit;
        ActivityTypes = activityTypes;
        Participants = participants;
        Links = links;
        Strategies = strategies?.ToArray() ?? [];
    }
}