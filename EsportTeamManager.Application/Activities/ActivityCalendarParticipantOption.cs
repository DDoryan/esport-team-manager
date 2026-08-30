namespace EsportTeamManager.Application.Activities;

public sealed class ActivityCalendarParticipantOption
{
    public Guid? UserId { get; }

    public Guid? FormerMemberId { get; }

    public string DisplayName { get; }

    public ActivityCalendarParticipantOption(Guid? userId, Guid? formerMemberId, string displayName)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
        }

        if (formerMemberId == Guid.Empty)
        {
            throw new ArgumentException("The former member identifier cannot be empty.", nameof(formerMemberId));
        }

        if (userId.HasValue == formerMemberId.HasValue)
        {
            throw new ArgumentException("Exactly one participant identity is required.");
        }

        string normalizedDisplayName = displayName.Trim();

        if (normalizedDisplayName.Length == 0)
        {
            throw new ArgumentException("The participant display name is required.", nameof(displayName));
        }

        UserId = userId;
        FormerMemberId = formerMemberId;
        DisplayName = normalizedDisplayName;
    }
}