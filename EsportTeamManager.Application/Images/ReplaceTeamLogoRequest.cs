namespace EsportTeamManager.Application.Images;

public sealed class ReplaceTeamLogoRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public string OriginalFileName { get; }

    public Stream Content { get; }

    public ReplaceTeamLogoRequest(Guid actorUserId, Guid teamId, string originalFileName, Stream content)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        OriginalFileName = originalFileName;
        Content = content;
    }
}