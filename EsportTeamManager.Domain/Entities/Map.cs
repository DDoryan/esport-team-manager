using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public sealed class Map
{
    public int MapId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    private Map()
    {
    }

    public Map(int mapId, string name)
    {
        if (mapId <= 0)
        {
            throw new DomainException("The map identifier must be positive.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
        {
            throw new DomainException("The map name must contain between 1 and 50 characters.");
        }

        MapId = mapId;
        Name = name.Trim();
    }
}