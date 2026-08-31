using System.Security.Claims;
using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.ViewModels.Strategies;

namespace RepriseWeb.Controllers;

[Authorize]
public sealed class StrategiesController : Controller
{
    private const int MaximumSearchTextLength = 100;

    private readonly IMapCatalogService _mapCatalogService;
    private readonly IStrategyListService _strategyListService;
    private readonly IUserTeamService _userTeamService;

    public StrategiesController(IMapCatalogService mapCatalogService, IStrategyListService strategyListService, IUserTeamService userTeamService)
    {
        _mapCatalogService = mapCatalogService;
        _strategyListService = strategyListService;
        _userTeamService = userTeamService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid teamId, bool filtersSubmitted, int? mapId, StrategySide[]? sides, bool includeActive, bool includeInactive, string? searchText, CancellationToken cancellationToken)
    {
        Guid? currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty)
        {
            return RedirectToAction("Entry", "Teams");
        }

        UserTeamSummary? currentTeam = await FindCurrentTeamAsync(currentUserId.Value, teamId, cancellationToken);

        if (currentTeam is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        if (searchText?.Trim().Length > MaximumSearchTextLength)
        {
            return BadRequest();
        }

        IReadOnlyCollection<MapOption> mapOptions = await _mapCatalogService.GetOptionsAsync(cancellationToken);

        if (mapId.HasValue && mapOptions.All(map => map.MapId != mapId.Value))
        {
            return BadRequest();
        }

        StrategySide[] selectedSides = filtersSubmitted
            ? (sides ?? []).Distinct().ToArray()
            :
            [
                StrategySide.Attack,
                StrategySide.Defense
            ];

        bool selectedIncludeActive = filtersSubmitted ? includeActive : true;
        bool selectedIncludeInactive = filtersSubmitted && includeInactive;
        bool hasSelectedSide = selectedSides.Length > 0;
        bool hasSelectedState = selectedIncludeActive || selectedIncludeInactive;

        IReadOnlyCollection<StrategySummary> strategies = Array.Empty<StrategySummary>();

        if (hasSelectedSide && hasSelectedState)
        {
            StrategySide? sideFilter = selectedSides.Length == 1 ? selectedSides[0] : null;
            bool? activeFilter = selectedIncludeActive == selectedIncludeInactive ? null : selectedIncludeActive;
            StrategyListFilter filter = new(mapId, sideFilter, activeFilter, searchText);

            strategies = await _strategyListService.GetAsync(teamId, filter, cancellationToken);
        }

        IReadOnlyCollection<StrategyMapOptionViewModel> mapViewModels = mapOptions
            .Select(map => new StrategyMapOptionViewModel(map.MapId, map.Name))
            .ToArray();

        IReadOnlyCollection<StrategyCardViewModel> strategyViewModels = strategies
            .Select(strategy => new StrategyCardViewModel(
                strategy.StrategyId,
                strategy.Name,
                strategy.MapName,
                CreateSideLabel(strategy.Side),
                strategy.IsActive ? "Active" : "Inactive",
                strategy.IsActive,
                strategy.UpdatedAtUtc))
            .ToArray();

        StrategyListViewModel viewModel = new(
            currentTeam.TeamId,
            currentTeam.Name,
            mapViewModels,
            strategyViewModels,
            mapId,
            selectedSides,
            selectedIncludeActive,
            selectedIncludeInactive,
            searchText?.Trim());

        return View(viewModel);
    }

    private async Task<UserTeamSummary?> FindCurrentTeamAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<UserTeamSummary> userTeams = await _userTeamService.GetTeamsForUserAsync(userId, cancellationToken);

        return userTeams.SingleOrDefault(team => team.TeamId == teamId);
    }

    private static string CreateSideLabel(StrategySide side)
    {
        return side switch
        {
            StrategySide.Attack => "Attaque",
            StrategySide.Defense => "Défense",
            _ => side.ToString()
        };
    }

    private Guid? GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }
}