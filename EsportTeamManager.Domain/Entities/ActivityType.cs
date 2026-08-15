using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class ActivityType
{
    public int ActivityTypeId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    public Guid? TeamId { get; private set; }

    private ActivityType()
    {
    }

    public ActivityType(int activityTypeId, string code, string label, bool isSystem, Guid? teamId = null)
    {
        if (activityTypeId <= 0)
        {
            throw new DomainException("The activity type identifier must be positive.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 30)
        {
            throw new DomainException("The activity type code must contain between 1 and 30 characters.");
        }

        if (string.IsNullOrWhiteSpace(label) || label.Length > 50)
        {
            throw new DomainException("The activity type label must contain between 1 and 50 characters.");
        }

        if (isSystem && teamId.HasValue)
        {
            throw new DomainException("A system activity type cannot belong to a team.");
        }

        if (!isSystem && !teamId.HasValue)
        {
            throw new DomainException("A custom activity type must belong to a team.");
        }

        ActivityTypeId = activityTypeId;
        Code = code.Trim();
        Label = label.Trim();
        IsSystem = isSystem;
        TeamId = teamId;
    }
}