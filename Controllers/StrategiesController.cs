using System.Security.Claims;
using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.ViewModels.Strategies;
using EsportTeamManager.Application.Images;

namespace RepriseWeb.Controllers;

[Authorize]
public sealed class StrategiesController : Controller
{
    private const int MaximumSearchTextLength = 100;

    private readonly IMapCatalogService _mapCatalogService;
    private readonly IStrategyListService _strategyListService;
    private readonly IUserTeamService _userTeamService;
    private readonly IStrategyEditingService _strategyEditingService;
    private readonly IPrivateImageService _privateImageService;

    public StrategiesController(IMapCatalogService mapCatalogService, IStrategyListService strategyListService, IStrategyEditingService strategyEditingService, IUserTeamService userTeamService, IPrivateImageService privateImageService)
    {
        _mapCatalogService = mapCatalogService;
        _strategyListService = strategyListService;
        _strategyEditingService = strategyEditingService;
        _userTeamService = userTeamService;
        _privateImageService = privateImageService;
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

        bool canCreateStrategy = await _strategyEditingService.CanManageAsync(currentUserId.Value, teamId, cancellationToken);

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
                strategy.HasImage,
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
            searchText?.Trim(),
            canCreateStrategy);

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid teamId, CancellationToken cancellationToken)
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

        if (currentTeam is null || !await _strategyEditingService.CanManageAsync(currentUserId.Value, teamId, cancellationToken))
        {
            return Forbid();
        }

        IReadOnlyCollection<StrategyMapOptionViewModel> maps = await GetMapViewModelsAsync(cancellationToken);
        StrategyFormViewModel viewModel = new()
        {
            TeamId = currentTeam.TeamId,
            TeamName = currentTeam.Name,
            Maps = maps,
            IsActive = true
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Create(StrategyFormViewModel viewModel, CancellationToken cancellationToken)
    {
        Guid? currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        UserTeamSummary? currentTeam = await FindCurrentTeamAsync(currentUserId.Value, viewModel.TeamId, cancellationToken);

        if (currentTeam is null || !await _strategyEditingService.CanManageAsync(currentUserId.Value, viewModel.TeamId, cancellationToken))
        {
            return Forbid();
        }

        IReadOnlyCollection<StrategyMapOptionViewModel> maps = await GetMapViewModelsAsync(cancellationToken);

        PopulateCreateViewModel(viewModel, currentTeam, maps);

        if (string.IsNullOrWhiteSpace(viewModel.Description) && string.IsNullOrWhiteSpace(viewModel.ExternalUrl) && viewModel.Image is null)
        {
            ModelState.AddModelError(string.Empty, "Ajoutez au moins une description, une URL externe ou une image.");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        SaveStrategyResult result;

        if (viewModel.Image is null)
        {
            CreateStrategyRequest request = new(
                currentUserId.Value,
                currentTeam.TeamId,
                viewModel.MapId!.Value,
                viewModel.Name,
                viewModel.Side!.Value,
                viewModel.Description,
                viewModel.ExternalUrl,
                viewModel.IsActive,
                null);

            result = await _strategyEditingService.CreateAsync(request, cancellationToken);
        }
        else
        {
            await using Stream imageContent = viewModel.Image.OpenReadStream();
            StrategyImageUpload image = new(viewModel.Image.FileName, imageContent);
            CreateStrategyRequest request = new(
                currentUserId.Value,
                currentTeam.TeamId,
                viewModel.MapId!.Value,
                viewModel.Name,
                viewModel.Side!.Value,
                viewModel.Description,
                viewModel.ExternalUrl,
                viewModel.IsActive,
                image);

            result = await _strategyEditingService.CreateAsync(request, cancellationToken);
        }

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(viewModel);
        }

        TempData["SuccessMessage"] = "La stratégie a été créée.";

        return RedirectToAction(nameof(Details), new
        {
            teamId = currentTeam.TeamId,
            strategyId = result.StrategyId
        });
    }

    [HttpGet]
    public async Task<IActionResult> Image(Guid teamId, Guid strategyId, CancellationToken cancellationToken)
    {
        Guid? currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty || strategyId == Guid.Empty)
        {
            return NotFound();
        }

        PrivateImageContent? image = await _privateImageService.GetStrategyImageAsync(currentUserId.Value, teamId, strategyId, cancellationToken);

        if (image is null)
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "private, no-store";

        return File(image.Content, image.MediaType);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid teamId, Guid strategyId, CancellationToken cancellationToken)
    {
        Guid? currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        UserTeamSummary? currentTeam = await FindCurrentTeamAsync(currentUserId.Value, teamId, cancellationToken);

        if (currentTeam is null)
        {
            return Forbid();
        }

        StrategyEditingDetails? details = await _strategyEditingService.GetAsync(currentUserId.Value, teamId, strategyId, cancellationToken);

        if (details is null)
        {
            return NotFound();
        }

        IReadOnlyCollection<StrategyMapOptionViewModel> maps = await GetMapViewModelsAsync(cancellationToken);
        StrategyDetailsViewModel viewModel = CreateDetailsViewModel(details, maps);

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Details(Guid teamId, Guid strategyId, StrategyDetailsViewModel viewModel, CancellationToken cancellationToken)
    {
        Guid? currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        UserTeamSummary? currentTeam = await FindCurrentTeamAsync(currentUserId.Value, teamId, cancellationToken);

        if (currentTeam is null)
        {
            return Forbid();
        }

        StrategyEditingDetails? currentDetails = await _strategyEditingService.GetAsync(currentUserId.Value, teamId, strategyId, cancellationToken);

        if (currentDetails is null)
        {
            return NotFound();
        }

        if (!currentDetails.CanManage)
        {
            return Forbid();
        }

        IReadOnlyCollection<StrategyMapOptionViewModel> maps = await GetMapViewModelsAsync(cancellationToken);

        PopulateDetailsViewModel(viewModel, currentDetails, maps);

        if (!currentDetails.HasImage && string.IsNullOrWhiteSpace(viewModel.Description) && string.IsNullOrWhiteSpace(viewModel.ExternalUrl) && viewModel.Image is null)
        {
            ModelState.AddModelError(string.Empty, "Conservez au moins une description, une URL externe ou une image.");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        SaveStrategyResult result;

        if (viewModel.Image is null)
        {
            UpdateStrategyRequest request = new(
                currentUserId.Value,
                teamId,
                strategyId,
                viewModel.MapId!.Value,
                viewModel.Name,
                viewModel.Side!.Value,
                viewModel.Description,
                viewModel.ExternalUrl,
                viewModel.IsActive,
                null);

            result = await _strategyEditingService.UpdateAsync(request, cancellationToken);
        }
        else
        {
            await using Stream imageContent = viewModel.Image.OpenReadStream();
            StrategyImageUpload image = new(viewModel.Image.FileName, imageContent);
            UpdateStrategyRequest request = new(
                currentUserId.Value,
                teamId,
                strategyId,
                viewModel.MapId!.Value,
                viewModel.Name,
                viewModel.Side!.Value,
                viewModel.Description,
                viewModel.ExternalUrl,
                viewModel.IsActive,
                image);

            result = await _strategyEditingService.UpdateAsync(request, cancellationToken);
        }

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(viewModel);
        }

        TempData["SuccessMessage"] = "La stratégie a été modifiée.";

        return RedirectToAction(nameof(Details), new
        {
            teamId,
            strategyId
        });
    }

    private async Task<IReadOnlyCollection<StrategyMapOptionViewModel>> GetMapViewModelsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<MapOption> maps = await _mapCatalogService.GetOptionsAsync(cancellationToken);

        return maps
            .Select(map => new StrategyMapOptionViewModel(map.MapId, map.Name))
            .ToArray();
    }

    private static void PopulateCreateViewModel(StrategyFormViewModel viewModel, UserTeamSummary team, IReadOnlyCollection<StrategyMapOptionViewModel> maps)
    {
        viewModel.TeamId = team.TeamId;
        viewModel.TeamName = team.Name;
        viewModel.Maps = maps;
    }

    private static StrategyDetailsViewModel CreateDetailsViewModel(StrategyEditingDetails details, IReadOnlyCollection<StrategyMapOptionViewModel> maps)
    {
        StrategyDetailsViewModel viewModel = new()
        {
            TeamId = details.TeamId,
            StrategyId = details.StrategyId,
            TeamName = details.TeamName,
            Maps = maps,
            Name = details.Name,
            MapId = details.MapId,
            Side = details.Side,
            Description = details.Description,
            ExternalUrl = details.ExternalUrl,
            IsActive = details.IsActive
        };

        PopulateDetailsViewModel(viewModel, details, maps);

        return viewModel;
    }

    private static void PopulateDetailsViewModel(StrategyDetailsViewModel viewModel, StrategyEditingDetails details, IReadOnlyCollection<StrategyMapOptionViewModel> maps)
    {
        viewModel.TeamId = details.TeamId;
        viewModel.StrategyId = details.StrategyId;
        viewModel.TeamName = details.TeamName;
        viewModel.Maps = maps;
        viewModel.CurrentName = details.Name;
        viewModel.MapName = maps.SingleOrDefault(map => map.MapId == details.MapId)?.Name ?? "Carte inconnue";
        viewModel.SideLabel = CreateSideLabel(details.Side);
        viewModel.StatusLabel = details.IsActive ? "Active" : "Inactive";
        viewModel.CurrentDescription = details.Description;
        viewModel.CurrentExternalUrl = details.ExternalUrl;
        viewModel.HasImage = details.HasImage;
        viewModel.CanManage = details.CanManage;
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