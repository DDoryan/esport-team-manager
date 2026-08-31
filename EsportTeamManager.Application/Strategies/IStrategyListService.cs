namespace EsportTeamManager.Application.Strategies;

public interface IStrategyListService
{
    Task<IReadOnlyCollection<StrategySummary>> GetAsync(Guid teamId, StrategyListFilter filter, CancellationToken cancellationToken = default);
}