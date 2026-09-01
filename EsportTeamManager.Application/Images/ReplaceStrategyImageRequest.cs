namespace EsportTeamManager.Application.Images;

public sealed class ReplaceStrategyImageRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public Guid StrategyId { get; }

    public string OriginalFileName { get; }

    public Stream Content { get; }

    public ReplaceStrategyImageRequest(Guid actorUserId, Guid teamId, Guid strategyId, string originalFileName, Stream content)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        StrategyId = strategyId;
        OriginalFileName = originalFileName;
        Content = content;
    }
}