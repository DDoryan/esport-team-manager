namespace RepriseWeb.ViewModels.Strategies;

public sealed class StrategyMapOptionViewModel
{
    public int MapId { get; }

    public string Name { get; }

    public StrategyMapOptionViewModel(int mapId, string name)
    {
        MapId = mapId;
        Name = name;
    }
}