using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Strategies;

public sealed class StrategySummary
{
    public Guid StrategyId { get; }

    public int MapId { get; }

    public string MapName { get; }

    public string Name { get; }

    public StrategySide Side { get; }

    public bool IsActive { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public StrategySummary(Guid strategyId, int mapId, string mapName, string name, StrategySide side, bool isActive, DateTimeOffset updatedAtUtc)
    {
        StrategyId = strategyId;
        MapId = mapId;
        MapName = mapName;
        Name = name;
        Side = side;
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }
}