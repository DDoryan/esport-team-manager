namespace EsportTeamManager.Application.Activities;

public sealed class ActivityEditLinkSummary
{
    public Guid ActivityLinkId { get; }

    public string Name { get; }

    public string Url { get; }

    public ActivityEditLinkSummary(Guid activityLinkId, string name, string url)
    {
        ActivityLinkId = activityLinkId;
        Name = name;
        Url = url;
    }
}