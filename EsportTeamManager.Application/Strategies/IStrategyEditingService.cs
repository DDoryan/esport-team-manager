namespace EsportTeamManager.Application.Strategies;

public interface IStrategyEditingService
{
    Task<bool> CanManageAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default);

    Task<StrategyEditingDetails?> GetAsync(Guid userId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default);

    Task<SaveStrategyResult> CreateAsync(CreateStrategyRequest request, CancellationToken cancellationToken = default);

    Task<SaveStrategyResult> UpdateAsync(UpdateStrategyRequest request, CancellationToken cancellationToken = default);

    Task<DeleteStrategyResult> DeleteAsync(DeleteStrategyRequest request, CancellationToken cancellationToken = default);
}