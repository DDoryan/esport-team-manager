using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Activities;

public sealed class ActivityCalendarFilter
{
    public IReadOnlyCollection<string> TypeCodes { get; }

    public IReadOnlyCollection<ActivityStatus> Statuses { get; }

    public Guid? ParticipantUserId { get; }

    public Guid? FormerMemberId { get; }

    public MatchResult? Result { get; }

    public string? SearchText { get; }

    public ActivityCalendarFilter(IReadOnlyCollection<string> typeCodes, IReadOnlyCollection<ActivityStatus> statuses, Guid? participantUserId, Guid? formerMemberId, MatchResult? result, string? searchText)
    {
        ArgumentNullException.ThrowIfNull(typeCodes);
        ArgumentNullException.ThrowIfNull(statuses);

        if (participantUserId == Guid.Empty)
        {
            throw new ArgumentException("The participant user identifier cannot be empty.", nameof(participantUserId));
        }

        if (formerMemberId == Guid.Empty)
        {
            throw new ArgumentException("The former member identifier cannot be empty.", nameof(formerMemberId));
        }

        if (participantUserId.HasValue && formerMemberId.HasValue)
        {
            throw new ArgumentException("Only one participant identity can be selected.");
        }

        if (statuses.Any(status => !Enum.IsDefined(typeof(ActivityStatus), status)))
        {
            throw new ArgumentException("An activity status is invalid.", nameof(statuses));
        }

        if (result.HasValue && !Enum.IsDefined(typeof(MatchResult), result.Value))
        {
            throw new ArgumentException("The match result is invalid.", nameof(result));
        }

        TypeCodes = typeCodes
            .Where(typeCode => !string.IsNullOrWhiteSpace(typeCode))
            .Select(typeCode => typeCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Statuses = statuses
            .Distinct()
            .ToArray();

        ParticipantUserId = participantUserId;
        FormerMemberId = formerMemberId;
        Result = result;
        SearchText = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
    }
}