using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Strategies;

public sealed class StrategyListService : IStrategyListService
{
    private readonly ApplicationDbContext _context;

    public StrategyListService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<StrategySummary>> GetAsync(Guid teamId, StrategyListFilter filter, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(filter);

        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("The team identifier cannot be empty.", nameof(teamId));
        }

        IQueryable<Strategy> query = _context.Strategies
            .AsNoTracking()
            .Where(strategy => strategy.TeamId == teamId);

        if (filter.MapId.HasValue)
        {
            query = query.Where(strategy => strategy.MapId == filter.MapId.Value);
        }

        if (filter.Side.HasValue)
        {
            query = query.Where(strategy => strategy.Side == filter.Side.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(strategy => strategy.IsActive == filter.IsActive.Value);
        }

        if (filter.SearchText is not null)
        {
            string normalizedSearchText = filter.SearchText.ToLowerInvariant();

            query = query.Where(strategy => strategy.Name.ToLower().Contains(normalizedSearchText));
        }

        List<StrategySummary> strategies = await query
            .Select(strategy => new StrategySummary(
                strategy.StrategyId,
                strategy.MapId,
                strategy.Map.Name,
                strategy.Name,
                strategy.Side,
                strategy.IsActive,
                strategy.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return strategies
            .OrderByDescending(strategy => strategy.UpdatedAtUtc)
            .ThenBy(strategy => strategy.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(strategy => strategy.StrategyId)
            .ToArray();
    }
}