using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Strategies;

public sealed class CreateStrategyRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public int MapId { get; }

    public string Name { get; }

    public StrategySide Side { get; }

    public string? Description { get; }

    public string? ExternalUrl { get; }

    public bool IsActive { get; }

    public StrategyImageUpload? Image { get; }

    public CreateStrategyRequest(Guid actorUserId, Guid teamId, int mapId, string name, StrategySide side, string? description, string? externalUrl, bool isActive, StrategyImageUpload? image)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        MapId = mapId;
        Name = name;
        Side = side;
        Description = description;
        ExternalUrl = externalUrl;
        IsActive = isActive;
        Image = image;
    }
}