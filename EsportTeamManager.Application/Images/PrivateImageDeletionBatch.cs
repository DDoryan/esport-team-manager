namespace EsportTeamManager.Application.Images;

public sealed class PrivateImageDeletionBatch
{
    public Guid BatchId { get; }

    public Guid TeamId { get; }

    public IReadOnlyCollection<Guid> StrategyIds { get; }

    public PrivateImageDeletionBatch(Guid batchId, Guid teamId, IReadOnlyCollection<Guid> strategyIds)
    {
        ArgumentNullException.ThrowIfNull(strategyIds);

        BatchId = batchId;
        TeamId = teamId;
        StrategyIds = strategyIds.ToArray();
    }
}