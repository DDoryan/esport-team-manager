using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Strategies;

public sealed class MapCatalogService : IMapCatalogService
{
    private readonly ApplicationDbContext _context;

    public MapCatalogService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<MapOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _context.Maps
            .AsNoTracking()
            .OrderBy(map => map.Name)
            .Select(map => new MapOption(map.MapId, map.Name))
            .ToListAsync(cancellationToken);
    }
}