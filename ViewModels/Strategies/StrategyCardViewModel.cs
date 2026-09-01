namespace RepriseWeb.ViewModels.Strategies;

public sealed class StrategyCardViewModel
{
    public Guid StrategyId { get; }

    public string Name { get; }

    public string MapName { get; }

    public string SideLabel { get; }

    public string StatusLabel { get; }

    public bool IsActive { get; }

    public bool HasImage { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public StrategyCardViewModel(Guid strategyId, string name, string mapName, string sideLabel, string statusLabel, bool isActive, bool hasImage, DateTimeOffset updatedAtUtc)
    {
        StrategyId = strategyId;
        Name = name;
        MapName = mapName;
        SideLabel = sideLabel;
        StatusLabel = statusLabel;
        IsActive = isActive;
        HasImage = hasImage;
        UpdatedAtUtc = updatedAtUtc;
    }
}