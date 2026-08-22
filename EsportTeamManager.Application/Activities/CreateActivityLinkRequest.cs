namespace EsportTeamManager.Application.Activities;

public sealed class CreateActivityLinkRequest
{
    public string Name { get; }

    public string Url { get; }

    public CreateActivityLinkRequest(string name, string url)
    {
        Name = name;
        Url = url;
    }
}