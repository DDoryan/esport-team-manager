namespace EsportTeamManager.Application.Strategies;

public sealed class DeleteStrategyRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public Guid StrategyId { get; }

    public DeleteStrategyRequest(Guid actorUserId, Guid teamId, Guid strategyId)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        StrategyId = strategyId;
    }
}