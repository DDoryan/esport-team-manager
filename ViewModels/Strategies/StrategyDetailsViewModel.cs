namespace RepriseWeb.ViewModels.Strategies;

public sealed class StrategyDetailsViewModel : StrategyFormViewModel
{
    public string CurrentName { get; set; } = string.Empty;

    public string MapName { get; set; } = string.Empty;

    public string SideLabel { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;

    public string? CurrentDescription { get; set; }

    public string? CurrentExternalUrl { get; set; }

    public bool HasImage { get; set; }

    public bool CanManage { get; set; }
}