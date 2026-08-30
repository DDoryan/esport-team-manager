using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class TeamActivity
{
    private readonly List<ActivityParticipant> _participants = [];

    public Guid ActivityId { get; private set; }

    public Guid TeamId { get; private set; }

    public int ActivityTypeId { get; private set; }

    public ActivityType ActivityType { get; private set; } = null!;

    public Guid CreatedByMembershipId { get; private set; }

    public string? Subtitle { get; private set; }

    public string? Description { get; private set; }

    public string? Report { get; private set; }

    public DateTimeOffset PlannedStartUtc { get; private set; }

    public DateTimeOffset PlannedEndUtc { get; private set; }

    public string TimeZoneId { get; private set; } = string.Empty;

    public ActivityStatus Status { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public MatchDetail? MatchDetail { get; private set; }

    public IReadOnlyCollection<ActivityParticipant> Participants => _participants.AsReadOnly();

    public bool RequiresScores => ActivityTypeRequiresScores(ActivityType.Code);

    private TeamActivity()
    {
    }

    public TeamActivity(Guid activityId, Guid teamId, ActivityType activityType, Guid createdByMembershipId, DateTimeOffset plannedStartUtc, DateTimeOffset plannedEndUtc, string timeZoneId, IEnumerable<Guid> participantMembershipIds, DateTimeOffset createdAtUtc, string? subtitle = null, string? description = null, string? report = null)
    {
        if (activityId == Guid.Empty)
        {
            throw new DomainException("The activity identifier cannot be empty.");
        }

        if (teamId == Guid.Empty)
        {
            throw new DomainException("The team identifier cannot be empty.");
        }

        if (activityType is null)
        {
            throw new DomainException("The activity type is required.");
        }

        if (createdByMembershipId == Guid.Empty)
        {
            throw new DomainException("The creator membership identifier cannot be empty.");
        }

        ActivityId = activityId;
        TeamId = teamId;
        ActivityTypeId = activityType.ActivityTypeId;
        ActivityType = activityType;
        CreatedByMembershipId = createdByMembershipId;
        Status = ActivityStatus.Planned;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;

        SetSchedule(plannedStartUtc, plannedEndUtc, timeZoneId);
        SetTexts(subtitle, description, report);
        ReplaceParticipantList(participantMembershipIds, null);

        if (RequiresScores)
        {
            MatchDetail = new MatchDetail(ActivityId);
        }
    }

    public void ChangeType(ActivityType activityType, DateTimeOffset updatedAtUtc)
    {
        if (activityType is null)
        {
            throw new DomainException("The activity type is required.");
        }

        bool currentTypeRequiresScores = RequiresScores;
        bool newTypeRequiresScores = ActivityTypeRequiresScores(activityType.Code);

        if (Status == ActivityStatus.Completed && currentTypeRequiresScores != newTypeRequiresScores)
        {
            throw new DomainException("A completed activity cannot switch between match and non-match types.");
        }

        ActivityTypeId = activityType.ActivityTypeId;
        ActivityType = activityType;

        if (newTypeRequiresScores && MatchDetail is null)
        {
            MatchDetail = new MatchDetail(ActivityId);
        }
        else if (!newTypeRequiresScores && MatchDetail is not null)
        {
            MatchDetail = null;
        }

        Touch(updatedAtUtc);
    }

    public void UpdateTexts(string? subtitle, string? description, string? report, DateTimeOffset updatedAtUtc)
    {
        SetTexts(subtitle, description, report);
        Touch(updatedAtUtc);
    }

    public void Reschedule(DateTimeOffset plannedStartUtc, DateTimeOffset plannedEndUtc, string timeZoneId, DateTimeOffset updatedAtUtc)
    {
        SetSchedule(plannedStartUtc, plannedEndUtc, timeZoneId);
        Touch(updatedAtUtc);
    }

    public void ReplaceParticipants(IEnumerable<Guid> participantMembershipIds, DateTimeOffset updatedAtUtc, IReadOnlyDictionary<Guid, Attendance>? attendanceByMembershipId = null)
    {
        ReplaceParticipantList(participantMembershipIds, attendanceByMembershipId);
        Touch(updatedAtUtc);
    }

    public void UpdateOpponent(string? opponentName, DateTimeOffset updatedAtUtc)
    {
        EnsureMatchActivity();
        MatchDetail!.UpdateOpponent(opponentName);
        Touch(updatedAtUtc);
    }

    public void SetScores(int teamScore, int opponentScore, DateTimeOffset updatedAtUtc)
    {
        EnsureMatchActivity();
        MatchDetail!.SetScores(teamScore, opponentScore);
        Touch(updatedAtUtc);
    }

    public void ClearScores(DateTimeOffset updatedAtUtc)
    {
        EnsureMatchActivity();

        if (Status == ActivityStatus.Completed)
        {
            throw new DomainException("The scores of a completed activity cannot be cleared.");
        }

        MatchDetail!.ClearScores();
        Touch(updatedAtUtc);
    }

    public void ChangeStatus(ActivityStatus status, IReadOnlyDictionary<Guid, Attendance>? attendanceByMembershipId, string? cancellationReason, bool departureConfirmed, DateTimeOffset changedAtUtc)
    {
        if (!Enum.IsDefined(typeof(ActivityStatus), status))
        {
            throw new DomainException("The activity status is invalid.");
        }

        bool departureRequiresConfirmation = Status != status && (Status is ActivityStatus.Completed or ActivityStatus.Cancelled);

        if (departureRequiresConfirmation && !departureConfirmed)
        {
            throw new DomainException("Leaving a completed or cancelled activity requires confirmation.");
        }

        if (status != ActivityStatus.Completed && attendanceByMembershipId is not null)
        {
            throw new DomainException("Attendance can only be supplied when completing an activity.");
        }

        switch (status)
        {
            case ActivityStatus.Planned:
                Status = ActivityStatus.Planned;
                CancellationReason = null;
                break;

            case ActivityStatus.Completed:
                ValidateAttendances(attendanceByMembershipId);

                if (RequiresScores)
                {
                    MatchDetail!.EnsureReadyForCompletion();
                }

                ApplyAttendances(attendanceByMembershipId!);
                Status = ActivityStatus.Completed;
                CancellationReason = null;
                break;

            case ActivityStatus.Cancelled:
                string? normalizedReason = NormalizeOptionalText(cancellationReason);

                if (normalizedReason is not null && normalizedReason.Length > 500)
                {
                    throw new DomainException("The cancellation reason cannot exceed 500 characters.");
                }

                Status = ActivityStatus.Cancelled;
                CancellationReason = normalizedReason;
                break;
        }

        Touch(changedAtUtc);
    }

    public void Complete(IReadOnlyDictionary<Guid, Attendance> attendanceByMembershipId, DateTimeOffset completedAtUtc)
    {
        if (Status != ActivityStatus.Planned)
        {
            throw new DomainException("Only a planned activity can be completed.");
        }

        ChangeStatus(ActivityStatus.Completed, attendanceByMembershipId, null, false, completedAtUtc);
    }

    public void UpdateAttendances(IReadOnlyDictionary<Guid, Attendance> attendanceByMembershipId, DateTimeOffset updatedAtUtc)
    {
        if (Status != ActivityStatus.Completed)
        {
            throw new DomainException("Attendance can only be corrected on a completed activity.");
        }

        ValidateAttendances(attendanceByMembershipId);
        ApplyAttendances(attendanceByMembershipId);
        Touch(updatedAtUtc);
    }

    public void UpdateAttendance(Guid teamMembershipId, Attendance attendance, DateTimeOffset updatedAtUtc)
    {
        if (Status != ActivityStatus.Completed)
        {
            throw new DomainException("Attendance can only be corrected on a completed activity.");
        }

        ActivityParticipant? participant = _participants.FirstOrDefault(item => item.TeamMembershipId == teamMembershipId);

        if (participant is null)
        {
            throw new DomainException("The selected membership is not an activity participant.");
        }

        participant.MarkAttendance(attendance);
        Touch(updatedAtUtc);
    }

    public void Cancel(string? cancellationReason, DateTimeOffset cancelledAtUtc)
    {
        if (Status != ActivityStatus.Planned)
        {
            throw new DomainException("Only a planned activity can be cancelled.");
        }

        ChangeStatus(ActivityStatus.Cancelled, null, cancellationReason, false, cancelledAtUtc);
    }

    private void SetSchedule(DateTimeOffset plannedStartUtc, DateTimeOffset plannedEndUtc, string timeZoneId)
    {
        DateTimeOffset normalizedStart = plannedStartUtc.ToUniversalTime();
        DateTimeOffset normalizedEnd = plannedEndUtc.ToUniversalTime();

        if (normalizedEnd <= normalizedStart)
        {
            throw new DomainException("The planned end time must be later than the planned start time.");
        }

        string normalizedTimeZone = timeZoneId.Trim();

        if (normalizedTimeZone.Length == 0 || normalizedTimeZone.Length > 64)
        {
            throw new DomainException("The activity time zone must contain between 1 and 64 characters.");
        }

        PlannedStartUtc = normalizedStart;
        PlannedEndUtc = normalizedEnd;
        TimeZoneId = normalizedTimeZone;
    }

    private void SetTexts(string? subtitle, string? description, string? report)
    {
        Subtitle = ValidateOptionalText(subtitle, 100, "The activity subtitle cannot exceed 100 characters.");
        Description = ValidateOptionalText(description, 2000, "The activity description cannot exceed 2000 characters.");
        Report = ValidateOptionalText(report, 5000, "The activity report cannot exceed 5000 characters.");
    }

    private void ReplaceParticipantList(IEnumerable<Guid> participantMembershipIds, IReadOnlyDictionary<Guid, Attendance>? attendanceByMembershipId)
    {
        if (participantMembershipIds is null)
        {
            throw new DomainException("Activity participants are required.");
        }

        List<Guid> identifiers = participantMembershipIds.Distinct().ToList();

        if (identifiers.Count == 0)
        {
            throw new DomainException("An activity must contain at least one participant.");
        }

        if (identifiers.Any(identifier => identifier == Guid.Empty))
        {
            throw new DomainException("Participant membership identifiers cannot be empty.");
        }

        if (Status == ActivityStatus.Completed)
        {
            if (attendanceByMembershipId is null || attendanceByMembershipId.Count != identifiers.Count)
            {
                throw new DomainException("Attendance must be provided for every participant of a completed activity.");
            }

            foreach (Guid identifier in identifiers)
            {
                if (!attendanceByMembershipId.TryGetValue(identifier, out Attendance attendance) || !Enum.IsDefined(typeof(Attendance), attendance))
                {
                    throw new DomainException("Attendance must be provided for every participant.");
                }
            }
        }
        else if (attendanceByMembershipId is not null)
        {
            throw new DomainException("A planned activity cannot store attendance.");
        }

        HashSet<Guid> requestedIdentifiers = identifiers.ToHashSet();
        ActivityParticipant[] removedParticipants = _participants
            .Where(participant => !requestedIdentifiers.Contains(participant.TeamMembershipId))
            .ToArray();

        foreach (ActivityParticipant removedParticipant in removedParticipants)
        {
            _participants.Remove(removedParticipant);
        }

        HashSet<Guid> existingIdentifiers = _participants
            .Select(participant => participant.TeamMembershipId)
            .ToHashSet();

        foreach (Guid identifier in identifiers)
        {
            if (!existingIdentifiers.Contains(identifier))
            {
                _participants.Add(new ActivityParticipant(ActivityId, identifier));
            }
        }

        if (attendanceByMembershipId is not null)
        {
            foreach (ActivityParticipant participant in _participants)
            {
                participant.MarkAttendance(attendanceByMembershipId[participant.TeamMembershipId]);
            }
        }
    }

    private void ValidateAttendances(IReadOnlyDictionary<Guid, Attendance>? attendanceByMembershipId)
    {
        if (attendanceByMembershipId is null || attendanceByMembershipId.Count != _participants.Count)
        {
            throw new DomainException("Attendance must be provided for every activity participant.");
        }

        foreach (ActivityParticipant participant in _participants)
        {
            if (!attendanceByMembershipId.TryGetValue(participant.TeamMembershipId, out Attendance attendance) || !Enum.IsDefined(typeof(Attendance), attendance))
            {
                throw new DomainException("Attendance must be provided for every activity participant.");
            }
        }
    }

    private void ApplyAttendances(IReadOnlyDictionary<Guid, Attendance> attendanceByMembershipId)
    {
        foreach (ActivityParticipant participant in _participants)
        {
            participant.MarkAttendance(attendanceByMembershipId[participant.TeamMembershipId]);
        }
    }

    private static bool ActivityTypeRequiresScores(string typeCode)
    {
        return typeCode is "Pracc" or "OfficialMatch";
    }

    private void EnsureMatchActivity()
    {
        if (!RequiresScores || MatchDetail is null)
        {
            throw new DomainException("Match information is only available for a pracc or an official match.");
        }
    }

    private void Touch(DateTimeOffset updatedAtUtc)
    {
        DateTimeOffset normalizedDate = updatedAtUtc.ToUniversalTime();

        if (normalizedDate < CreatedAtUtc)
        {
            throw new DomainException("The update date cannot precede the activity creation date.");
        }

        UpdatedAtUtc = normalizedDate;
    }

    private static string? ValidateOptionalText(string? value, int maximumLength, string errorMessage)
    {
        string? normalizedValue = NormalizeOptionalText(value);

        if (normalizedValue is not null && normalizedValue.Length > maximumLength)
        {
            throw new DomainException(errorMessage);
        }

        return normalizedValue;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}