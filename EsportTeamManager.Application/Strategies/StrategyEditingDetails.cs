using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Strategies;

public sealed class StrategyEditingDetails
{
    public Guid StrategyId { get; }

    public Guid TeamId { get; }

    public string TeamName { get; }

    public int MapId { get; }

    public string Name { get; }

    public StrategySide Side { get; }

    public string? Description { get; }

    public string? ExternalUrl { get; }

    public bool IsActive { get; }

    public bool HasImage { get; }

    public int AssociationCount { get; }

    public bool CanManage { get; }

    public StrategyEditingDetails(Guid strategyId, Guid teamId, string teamName, int mapId, string name, StrategySide side, string? description, string? externalUrl, bool isActive, bool hasImage, int associationCount, bool canManage)
    {
        StrategyId = strategyId;
        TeamId = teamId;
        TeamName = teamName;
        MapId = mapId;
        Name = name;
        Side = side;
        Description = description;
        ExternalUrl = externalUrl;
        IsActive = isActive;
        HasImage = hasImage;
        CanManage = canManage;
        AssociationCount = associationCount;
    }
}