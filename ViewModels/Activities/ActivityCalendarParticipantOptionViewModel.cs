namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityCalendarParticipantOptionViewModel
{
    public string Value { get; }

    public string DisplayName { get; }

    public ActivityCalendarParticipantOptionViewModel(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}