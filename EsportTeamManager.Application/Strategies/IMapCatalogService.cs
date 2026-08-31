namespace EsportTeamManager.Application.Strategies;

public interface IMapCatalogService
{
    Task<IReadOnlyCollection<MapOption>> GetOptionsAsync(CancellationToken cancellationToken = default);
}