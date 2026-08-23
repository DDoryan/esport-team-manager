namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityEditLinkViewModel
{
    public Guid ActivityLinkId { get; }

    public string Name { get; }

    public string Url { get; }

    public ActivityEditLinkViewModel(Guid activityLinkId, string name, string url)
    {
        ActivityLinkId = activityLinkId;
        Name = name;
        Url = url;
    }
}