using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Activities;

public sealed class ActivityEditStrategySummary
{
    public Guid StrategyId { get; }

    public string Name { get; }

    public string MapName { get; }

    public StrategySide Side { get; }

    public bool IsActive { get; }

    public bool IsSelected { get; }

    public bool HasImage { get; }

    public ActivityEditStrategySummary(Guid strategyId, string name, string mapName, StrategySide side, bool isActive, bool isSelected, bool hasImage)
    {
        StrategyId = strategyId;
        Name = name;
        MapName = mapName;
        Side = side;
        IsActive = isActive;
        IsSelected = isSelected;
        HasImage = hasImage;
    }
}