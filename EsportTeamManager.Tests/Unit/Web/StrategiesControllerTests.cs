using System.Security.Claims;
using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.Controllers;
using RepriseWeb.ViewModels.Strategies;

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

    private static StrategiesController CreateController(IMapCatalogService mapCatalogService, IStrategyListService strategyListService, IUserTeamService userTeamService, Guid? userId)
    {
        ClaimsIdentity identity = userId.HasValue
            ? new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
            ], "Test")
            : new ClaimsIdentity();

        StrategiesController controller = new(mapCatalogService, strategyListService, userTeamService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        return controller;
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
}