namespace EsportTeamManager.Application.Activities;

public sealed class ActivityTypeOption
{
    public int ActivityTypeId { get; }

    public string Code { get; }

    public string Label { get; }

    public ActivityTypeOption(int activityTypeId, string code, string label)
    {
        ActivityTypeId = activityTypeId;
        Code = code;
        Label = label;
    }
}