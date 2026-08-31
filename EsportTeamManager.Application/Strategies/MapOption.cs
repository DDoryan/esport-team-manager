namespace EsportTeamManager.Application.Strategies;

public sealed class MapOption
{
    public int MapId { get; }

    public string Name { get; }

    public MapOption(int mapId, string name)
    {
        MapId = mapId;
        Name = name;
    }
}