namespace EsportTeamManager.Application.Activities;

public sealed class UpdateActivityLinkRequest
{
    public Guid ActivityLinkId { get; }

    public string Name { get; }

    public string Url { get; }

    public UpdateActivityLinkRequest(Guid activityLinkId, string name, string url)
    {
        ActivityLinkId = activityLinkId;
        Name = name;
        Url = url;
    }
}