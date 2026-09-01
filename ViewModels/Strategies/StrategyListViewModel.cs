using EsportTeamManager.Domain.Enums;

namespace RepriseWeb.ViewModels.Strategies;

public sealed class StrategyListViewModel
{
    public Guid TeamId { get; }

    public string TeamName { get; }

    public IReadOnlyCollection<StrategyMapOptionViewModel> Maps { get; }

    public IReadOnlyCollection<StrategyCardViewModel> Strategies { get; }

    public int? SelectedMapId { get; }

    public IReadOnlyCollection<StrategySide> SelectedSides { get; }

    public bool IncludeActive { get; }

    public bool IncludeInactive { get; }

    public string? SearchText { get; }

    public bool CanCreateStrategy { get; }

    public StrategyListViewModel(Guid teamId, string teamName, IReadOnlyCollection<StrategyMapOptionViewModel> maps, IReadOnlyCollection<StrategyCardViewModel> strategies, int? selectedMapId, IReadOnlyCollection<StrategySide> selectedSides, bool includeActive, bool includeInactive, string? searchText, bool canCreateStrategy)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(selectedSides);

        TeamId = teamId;
        TeamName = teamName;
        Maps = maps.ToArray();
        Strategies = strategies.ToArray();
        SelectedMapId = selectedMapId;
        SelectedSides = selectedSides.ToArray();
        IncludeActive = includeActive;
        IncludeInactive = includeInactive;
        SearchText = searchText;
        CanCreateStrategy = canCreateStrategy;
    }
}