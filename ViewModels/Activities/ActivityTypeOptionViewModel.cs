namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityTypeOptionViewModel
{
    public int ActivityTypeId { get; }

    public string Code { get; }

    public string Label { get; }

    public ActivityTypeOptionViewModel(int activityTypeId, string code, string label)
    {
        ActivityTypeId = activityTypeId;
        Code = code;
        Label = label;
    }
}