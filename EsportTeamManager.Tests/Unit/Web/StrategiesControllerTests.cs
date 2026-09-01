using System.Security.Claims;
using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.Controllers;
using RepriseWeb.ViewModels.Strategies;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using EsportTeamManager.Application.Images;

namespace EsportTeamManager.Tests.Unit.Web;

public sealed class StrategiesControllerTests
{
    [Fact]
    public async Task Index_WhenUserIsNotAuthenticated_ReturnsChallenge()
    {
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), null);

        IActionResult result = await controller.Index(Guid.NewGuid(), false, null, null, false, false, null, CancellationToken.None);

        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Index_WithEmptyTeamIdentifier_RedirectsToTeamEntry()
    {
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), Guid.NewGuid());

        IActionResult result = await controller.Index(Guid.Empty, false, null, null, false, false, null, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal("Entry", redirect.ActionName);
        Assert.Equal("Teams", redirect.ControllerName);
    }

    [Fact]
    public async Task Index_WhenTeamIsNotAccessible_ReturnsForbid()
    {
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), Guid.NewGuid());

        IActionResult result = await controller.Index(Guid.NewGuid(), false, null, null, false, false, null, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_WithoutSubmittedFilters_UsesWireframeDefaults()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubMapCatalogService mapCatalogService = new(
        [
            new MapOption(1, "Ascent"),
            new MapOption(2, "Lotus")
        ]);
        StubStrategyListService strategyListService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        StrategiesController controller = CreateController(mapCatalogService, strategyListService, userTeamService, userId);

        IActionResult result = await controller.Index(teamId, false, null, null, false, false, null, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        StrategyListViewModel model = Assert.IsType<StrategyListViewModel>(view.Model);
        StrategyListFilter filter = Assert.IsType<StrategyListFilter>(strategyListService.LastFilter);

        Assert.Null(model.SelectedMapId);
        Assert.Contains(StrategySide.Attack, model.SelectedSides);
        Assert.Contains(StrategySide.Defense, model.SelectedSides);
        Assert.True(model.IncludeActive);
        Assert.False(model.IncludeInactive);
        Assert.Null(filter.MapId);
        Assert.Null(filter.Side);
        Assert.True(filter.IsActive);
        Assert.Null(filter.SearchText);
    }

    [Fact]
    public async Task Index_WithSubmittedFilters_ForwardsSelectedMapSideStatesAndSearch()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubMapCatalogService mapCatalogService = new(
        [
            new MapOption(1, "Ascent"),
            new MapOption(2, "Lotus")
        ]);
        StubStrategyListService strategyListService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        StrategiesController controller = CreateController(mapCatalogService, strategyListService, userTeamService, userId);

        IActionResult result = await controller.Index(
            teamId,
            true,
            2,
            [StrategySide.Defense],
            true,
            true,
            "  Retake  ",
            CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        StrategyListViewModel model = Assert.IsType<StrategyListViewModel>(view.Model);
        StrategyListFilter filter = Assert.IsType<StrategyListFilter>(strategyListService.LastFilter);

        Assert.Equal(2, model.SelectedMapId);
        Assert.Single(model.SelectedSides);
        Assert.Contains(StrategySide.Defense, model.SelectedSides);
        Assert.True(model.IncludeActive);
        Assert.True(model.IncludeInactive);
        Assert.Equal("Retake", model.SearchText);
        Assert.Equal(2, filter.MapId);
        Assert.Equal(StrategySide.Defense, filter.Side);
        Assert.Null(filter.IsActive);
        Assert.Equal("Retake", filter.SearchText);
    }

    [Fact]
    public async Task Index_WithUnknownMapIdentifier_ReturnsBadRequest()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubMapCatalogService mapCatalogService = new(
        [
            new MapOption(1, "Ascent")
        ]);
        StubStrategyListService strategyListService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        StrategiesController controller = CreateController(mapCatalogService, strategyListService, userTeamService, userId);

        IActionResult result = await controller.Index(teamId, true, 999, [StrategySide.Attack], true, false, null, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Null(strategyListService.LastFilter);
    }

    [Fact]
    public async Task Index_WhenSearchTextExceedsMaximumLength_ReturnsBadRequest()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubStrategyListService strategyListService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        StrategiesController controller = CreateController(new StubMapCatalogService(), strategyListService, userTeamService, userId);

        IActionResult result = await controller.Index(teamId, true, null, [StrategySide.Attack], true, false, new string('a', 101), CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Null(strategyListService.LastFilter);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task Index_WhenNoSideOrNoStateIsSelected_ReturnsEmptyListWithoutQuery(bool hasSide, bool includeActive, bool includeInactive)
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubStrategyListService strategyListService = new();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", true)
        ]);
        StrategiesController controller = CreateController(new StubMapCatalogService(), strategyListService, userTeamService, userId);
        StrategySide[] sides = hasSide ? [StrategySide.Attack] : [];

        IActionResult result = await controller.Index(teamId, true, null, sides, includeActive, includeInactive, null, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        StrategyListViewModel model = Assert.IsType<StrategyListViewModel>(view.Model);

        Assert.Empty(model.Strategies);
        Assert.Null(strategyListService.LastFilter);
    }

    [Fact]
    public async Task CreateGet_WhenUserCanManageTeam_ReturnsCreationView()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        StubMapCatalogService mapCatalogService = new(
        [
            new MapOption(1, "Ascent"),
        new MapOption(2, "Lotus")
        ]);
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", false)
        ]);
        StubStrategyEditingService strategyEditingService = new();
        StrategiesController controller = CreateController(mapCatalogService, new StubStrategyListService(), userTeamService, userId, strategyEditingService);

        IActionResult result = await controller.Create(teamId, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        StrategyFormViewModel model = Assert.IsType<StrategyFormViewModel>(view.Model);

        Assert.Equal(teamId, model.TeamId);
        Assert.Equal("Phoenix Academy", model.TeamName);
        Assert.Equal(2, model.Maps.Count);
        Assert.True(model.IsActive);
    }

    [Fact]
    public async Task CreatePost_WhenServiceSucceeds_RedirectsToDetails()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid strategyId = Guid.NewGuid();
        StubMapCatalogService mapCatalogService = new(
        [
            new MapOption(4, "Ascent")
        ]);
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Coach", false)
        ]);
        StubStrategyEditingService strategyEditingService = new()
        {
            CreateResult = SaveStrategyResult.Success(strategyId)
        };
        StrategiesController controller = CreateController(mapCatalogService, new StubStrategyListService(), userTeamService, userId, strategyEditingService);
        StrategyFormViewModel viewModel = new()
        {
            TeamId = teamId,
            Name = "Exécution site A",
            MapId = 4,
            Side = StrategySide.Attack,
            Description = "Prise rapide du site A.",
            IsActive = true
        };

        IActionResult result = await controller.Create(viewModel, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        CreateStrategyRequest request = Assert.IsType<CreateStrategyRequest>(strategyEditingService.LastCreateRequest);

        Assert.Equal(nameof(StrategiesController.Details), redirect.ActionName);
        Assert.Equal(teamId, redirect.RouteValues?["teamId"]);
        Assert.Equal(strategyId, redirect.RouteValues?["strategyId"]);
        Assert.Equal(userId, request.ActorUserId);
        Assert.Equal(teamId, request.TeamId);
        Assert.Equal("Exécution site A", request.Name);
        Assert.Equal(StrategySide.Attack, request.Side);
    }

    [Fact]
    public async Task DetailsGet_WhenPlayerIsActive_ReturnsReadOnlyView()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid strategyId = Guid.NewGuid();
        StubMapCatalogService mapCatalogService = new(
        [
            new MapOption(9, "Lotus")
        ]);
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", false)
        ]);
        StubStrategyEditingService strategyEditingService = new()
        {
            Details = new StrategyEditingDetails(
                strategyId,
                teamId,
                "Phoenix Academy",
                9,
                "Retake site C",
                StrategySide.Defense,
                "Reprise coordonnée.",
                null,
                true,
                false,
                false)
        };
        StrategiesController controller = CreateController(mapCatalogService, new StubStrategyListService(), userTeamService, userId, strategyEditingService);

        IActionResult result = await controller.Details(teamId, strategyId, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        StrategyDetailsViewModel model = Assert.IsType<StrategyDetailsViewModel>(view.Model);

        Assert.Equal(strategyId, model.StrategyId);
        Assert.Equal("Retake site C", model.CurrentName);
        Assert.Equal("Lotus", model.MapName);
        Assert.Equal("Défense", model.SideLabel);
        Assert.False(model.CanManage);
    }

    [Fact]
    public async Task DetailsPost_WhenPlayerCannotManageStrategy_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid strategyId = Guid.NewGuid();
        StubUserTeamService userTeamService = new(
        [
            new UserTeamSummary(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", false)
        ]);
        StubStrategyEditingService strategyEditingService = new()
        {
            Details = new StrategyEditingDetails(
                strategyId,
                teamId,
                "Phoenix Academy",
                4,
                "Exécution site A",
                StrategySide.Attack,
                "Prise rapide.",
                null,
                true,
                false,
                false)
        };
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), userTeamService, userId, strategyEditingService);
        StrategyDetailsViewModel viewModel = new()
        {
            TeamId = teamId,
            StrategyId = strategyId,
            Name = "Modification interdite",
            MapId = 4,
            Side = StrategySide.Defense,
            Description = "Modification interdite.",
            IsActive = false
        };

        IActionResult result = await controller.Details(teamId, strategyId, viewModel, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(strategyEditingService.LastUpdateRequest);
    }

    [Fact]
    public async Task Image_WhenUserIsNotAuthenticated_ReturnsChallenge()
    {
        StubPrivateImageService privateImageService = new();
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), null, privateImageService: privateImageService);

        IActionResult result = await controller.Image(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<ChallengeResult>(result);
        Assert.Null(privateImageService.LastActorUserId);
    }

    [Fact]
    public async Task Image_WhenImageIsNotAccessible_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid strategyId = Guid.NewGuid();
        StubPrivateImageService privateImageService = new();
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), userId, privateImageService: privateImageService);

        IActionResult result = await controller.Image(teamId, strategyId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(userId, privateImageService.LastActorUserId);
        Assert.Equal(teamId, privateImageService.LastTeamId);
        Assert.Equal(strategyId, privateImageService.LastStrategyId);
    }

    [Fact]
    public async Task Image_WhenImageIsAccessible_ReturnsPrivateImageFile()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid strategyId = Guid.NewGuid();
        MemoryStream imageContent = new([1, 2, 3, 4]);
        StubPrivateImageService privateImageService = new()
        {
            StrategyImage = new PrivateImageContent(imageContent, "image/webp")
        };
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), userId, privateImageService: privateImageService);

        IActionResult result = await controller.Image(teamId, strategyId, CancellationToken.None);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);

        Assert.Same(imageContent, file.FileStream);
        Assert.Equal("image/webp", file.ContentType);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Thumbnail_WhenImageIsAccessible_ReturnsPrivateThumbnailFile()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid strategyId = Guid.NewGuid();
        MemoryStream thumbnailContent = new([1, 2, 3]);
        StubPrivateImageService privateImageService = new()
        {
            StrategyThumbnail = new PrivateImageContent(thumbnailContent, "image/webp")
        };
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), userId, privateImageService: privateImageService);

        IActionResult result = await controller.Thumbnail(teamId, strategyId, CancellationToken.None);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);

        Assert.Same(thumbnailContent, file.FileStream);
        Assert.Equal("image/webp", file.ContentType);
        Assert.True(privateImageService.LastRequestUsedThumbnail);
        Assert.Equal(userId, privateImageService.LastActorUserId);
        Assert.Equal(teamId, privateImageService.LastTeamId);
        Assert.Equal(strategyId, privateImageService.LastStrategyId);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task DownloadImage_WhenImageIsAccessible_ReturnsAttachmentWithReadableName()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid strategyId = Guid.NewGuid();
        MemoryStream imageContent = new([1, 2, 3, 4]);
        StubPrivateImageService privateImageService = new()
        {
            StrategyImage = new PrivateImageContent(imageContent, "image/webp", "Split A.webp")
        };
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), userId, privateImageService: privateImageService);

        IActionResult result = await controller.DownloadImage(teamId, strategyId, CancellationToken.None);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);

        Assert.Same(imageContent, file.FileStream);
        Assert.Equal("image/webp", file.ContentType);
        Assert.Equal("Split A.webp", file.FileDownloadName);
        Assert.False(privateImageService.LastRequestUsedThumbnail);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task DownloadImage_WhenImageIsNotAccessible_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid strategyId = Guid.NewGuid();
        StubPrivateImageService privateImageService = new();
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), userId, privateImageService: privateImageService);

        IActionResult result = await controller.DownloadImage(teamId, strategyId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(userId, privateImageService.LastActorUserId);
        Assert.Equal(teamId, privateImageService.LastTeamId);
        Assert.Equal(strategyId, privateImageService.LastStrategyId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Thumbnail_WithEmptyIdentifier_ReturnsNotFoundWithoutReadingStorage(bool emptyTeamId, bool emptyStrategyId)
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = emptyTeamId ? Guid.Empty : Guid.NewGuid();
        Guid strategyId = emptyStrategyId ? Guid.Empty : Guid.NewGuid();
        StubPrivateImageService privateImageService = new();
        StrategiesController controller = CreateController(new StubMapCatalogService(), new StubStrategyListService(), new StubUserTeamService([]), userId, privateImageService: privateImageService);

        IActionResult result = await controller.Thumbnail(teamId, strategyId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(privateImageService.LastActorUserId);
    }

    private static StrategiesController CreateController(IMapCatalogService mapCatalogService, IStrategyListService strategyListService, IUserTeamService userTeamService, Guid? userId, StubStrategyEditingService? strategyEditingService = null, StubPrivateImageService? privateImageService = null)
    {
        ClaimsIdentity identity = userId.HasValue
            ? new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
            ], "Test")
            : new ClaimsIdentity();

        StrategiesController controller = new(mapCatalogService, strategyListService, strategyEditingService ?? new StubStrategyEditingService(), userTeamService, privateImageService ?? new StubPrivateImageService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        controller.TempData = new TempDataDictionary(controller.HttpContext, new StubTempDataProvider());

        return controller;
    }

    private sealed class StubPrivateImageService : IPrivateImageService
    {
        public PrivateImageContent? StrategyImage { get; set; }

        public PrivateImageContent? StrategyThumbnail { get; set; }

        public bool LastRequestUsedThumbnail { get; private set; }

        public Guid? LastActorUserId { get; private set; }

        public Guid? LastTeamId { get; private set; }

        public Guid? LastStrategyId { get; private set; }

        public Task<PrivateImageContent?> GetTeamLogoThumbnailAsync(Guid actorUserId, Guid teamId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<PrivateImageContent?>(null);
        }

        public Task<PrivateImageContent?> GetStrategyImageThumbnailAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastActorUserId = actorUserId;
            LastTeamId = teamId;
            LastStrategyId = strategyId;
            LastRequestUsedThumbnail = true;

            return Task.FromResult(StrategyThumbnail);
        }

        public Task<PrivateImageContent?> GetStrategyImageAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastActorUserId = actorUserId;
            LastTeamId = teamId;
            LastStrategyId = strategyId;
            LastRequestUsedThumbnail = false;

            return Task.FromResult(StrategyImage);
        }

        public Task<StorePrivateImageResult> ReplaceStrategyImageAsync(ReplaceStrategyImageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StorePrivateImageResult> ReplaceTeamLogoAsync(ReplaceTeamLogoRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StorePrivateImageResult> StoreStrategyImageAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StorePrivateImageResult> StoreTeamLogoAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object?>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    private sealed class StubMapCatalogService : IMapCatalogService
    {
        private readonly IReadOnlyCollection<MapOption> _maps;

        public StubMapCatalogService(IReadOnlyCollection<MapOption>? maps = null)
        {
            _maps = maps ?? [];
        }

        public Task<IReadOnlyCollection<MapOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_maps);
        }
    }

    private sealed class StubStrategyListService : IStrategyListService
    {
        public StrategyListFilter? LastFilter { get; private set; }

        public Task<IReadOnlyCollection<StrategySummary>> GetAsync(Guid teamId, StrategyListFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;

            return Task.FromResult<IReadOnlyCollection<StrategySummary>>([]);
        }
    }

    private sealed class StubUserTeamService : IUserTeamService
    {
        private readonly IReadOnlyCollection<UserTeamSummary> _teams;

        public StubUserTeamService(IReadOnlyCollection<UserTeamSummary> teams)
        {
            _teams = teams;
        }

        public Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_teams);
        }

        public Task<CreateTeamResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamManagementDetails?> GetManagementDetailsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<InviteTeamMemberResult> InviteMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamMembershipActionResult> ChangeMemberRoleAsync(ChangeTeamMemberRoleRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamMembershipActionResult> LeaveTeamAsync(LeaveTeamRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamMembershipActionResult> RemoveMemberAsync(RemoveTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OwnershipTransferActionResult> AcceptOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OwnershipTransferActionResult> CancelOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OwnershipTransferActionResult> InitiateOwnershipTransferAsync(InitiateOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OwnershipTransferActionResult> RefuseOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UpdateTeamInformationResult> UpdateInformationAsync(UpdateTeamInformationRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubStrategyEditingService : IStrategyEditingService
    {
        public bool CanManage { get; set; } = true;

        public StrategyEditingDetails? Details { get; set; }

        public SaveStrategyResult CreateResult { get; set; } = SaveStrategyResult.Failure(["Le résultat de création n’est pas configuré."]);

        public SaveStrategyResult UpdateResult { get; set; } = SaveStrategyResult.Failure(["Le résultat de modification n’est pas configuré."]);

        public CreateStrategyRequest? LastCreateRequest { get; private set; }

        public UpdateStrategyRequest? LastUpdateRequest { get; private set; }

        public Task<bool> CanManageAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CanManage);
        }

        public Task<StrategyEditingDetails?> GetAsync(Guid userId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Details);
        }

        public Task<SaveStrategyResult> CreateAsync(CreateStrategyRequest request, CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;

            return Task.FromResult(CreateResult);
        }

        public Task<SaveStrategyResult> UpdateAsync(UpdateStrategyRequest request, CancellationToken cancellationToken = default)
        {
            LastUpdateRequest = request;

            return Task.FromResult(UpdateResult);
        }
    }
}