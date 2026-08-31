using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Strategies;

public sealed class StrategyListFilter
{
    public int? MapId { get; }

    public StrategySide? Side { get; }

    public bool? IsActive { get; }

    public string? SearchText { get; }

    public StrategyListFilter(int? mapId, StrategySide? side, bool? isActive, string? searchText)
    {
        if (mapId.HasValue && mapId.Value <= 0)
        {
            throw new ArgumentException("The map identifier must be positive.", nameof(mapId));
        }

        if (side.HasValue && !Enum.IsDefined(typeof(StrategySide), side.Value))
        {
            throw new ArgumentException("The strategy side is invalid.", nameof(side));
        }

        MapId = mapId;
        Side = side;
        IsActive = isActive;
        SearchText = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
    }
}