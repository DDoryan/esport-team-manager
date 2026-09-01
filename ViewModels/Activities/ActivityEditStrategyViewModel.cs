using EsportTeamManager.Domain.Enums;

namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityEditStrategyViewModel
{
    public Guid StrategyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MapName { get; set; } = string.Empty;

    public StrategySide Side { get; set; }

    public bool IsActive { get; set; }

    public bool IsSelected { get; set; }

    public bool HasImage { get; set; }
}