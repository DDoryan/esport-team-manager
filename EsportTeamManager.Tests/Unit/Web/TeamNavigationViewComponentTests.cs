using System.Security.Claims;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Web.Models.Navigation;
using EsportTeamManager.Web.ViewComponents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace EsportTeamManager.Tests.Unit.Web;

public sealed class TeamNavigationViewComponentTests
{
    [Fact]
    public async Task InvokeAsync_WhenRequestedTeamIsAccessible_SelectsAndStoresIt()
    {
        Guid userId = Guid.NewGuid();
        UserTeamSummary team = new(Guid.NewGuid(), "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", true);
        StubUserTeamService service = new([team]);
        TeamNavigationViewComponent component = CreateComponent(service, userId, team.TeamId, null, "Activities", "Index");

        IViewComponentResult result = await component.InvokeAsync();

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        TeamNavigationViewModel viewModel = Assert.IsType<TeamNavigationViewModel>(viewResult.ViewData?.Model);
        string setCookie = component.HttpContext.Response.Headers.SetCookie.ToString();

        Assert.NotNull(viewModel.ActiveTeam);
        Assert.Equal(team.TeamId, viewModel.ActiveTeam.TeamId);
        Assert.True(viewModel.CalendarIsActive);
        Assert.False(viewModel.ManagementIsActive);
        Assert.False(viewModel.StrategiesIsActive);
        Assert.Contains($"EsportTeamManager.LastVisitedTeamId={team.TeamId}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoTeamIsRequested_UsesAccessibleStoredTeam()
    {
        Guid userId = Guid.NewGuid();
        UserTeamSummary firstTeam = new(Guid.NewGuid(), "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", true);
        UserTeamSummary secondTeam = new(Guid.NewGuid(), "Valorant Academy", "VAL", "Europe/Paris", "Coach", false);
        StubUserTeamService service = new([firstTeam, secondTeam]);
        TeamNavigationViewComponent component = CreateComponent(service, userId, null, secondTeam.TeamId, "Teams", "Index");

        IViewComponentResult result = await component.InvokeAsync();

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        TeamNavigationViewModel viewModel = Assert.IsType<TeamNavigationViewModel>(viewResult.ViewData?.Model);

        Assert.NotNull(viewModel.ActiveTeam);
        Assert.Equal(secondTeam.TeamId, viewModel.ActiveTeam.TeamId);
        Assert.Equal(2, viewModel.Teams.Count);
        Assert.False(viewModel.CalendarIsActive);
        Assert.False(viewModel.ManagementIsActive);
        Assert.False(viewModel.StrategiesIsActive);
    }

    [Fact]
    public async Task InvokeAsync_WhenStoredTeamIsInaccessible_ClearsSelectionAndDeletesCookie()
    {
        Guid userId = Guid.NewGuid();
        Guid inaccessibleTeamId = Guid.NewGuid();
        UserTeamSummary team = new(Guid.NewGuid(), "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", true);
        StubUserTeamService service = new([team]);
        TeamNavigationViewComponent component = CreateComponent(service, userId, null, inaccessibleTeamId, "Teams", "Index");

        IViewComponentResult result = await component.InvokeAsync();

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        TeamNavigationViewModel viewModel = Assert.IsType<TeamNavigationViewModel>(viewResult.ViewData?.Model);
        string setCookie = component.HttpContext.Response.Headers.SetCookie.ToString();

        Assert.Null(viewModel.ActiveTeam);
        Assert.False(viewModel.CalendarIsActive);
        Assert.False(viewModel.ManagementIsActive);
        Assert.False(viewModel.StrategiesIsActive);
        Assert.Contains("EsportTeamManager.LastVisitedTeamId=;", setCookie);
    }

    [Fact]
    public async Task InvokeAsync_WhenStrategiesPageIsDisplayed_MarksStrategiesAsActive()
    {
        Guid userId = Guid.NewGuid();
        UserTeamSummary team = new(Guid.NewGuid(), "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", true);
        StubUserTeamService service = new([team]);
        TeamNavigationViewComponent component = CreateComponent(service, userId, team.TeamId, null, "Strategies", "Index");

        IViewComponentResult result = await component.InvokeAsync();

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        TeamNavigationViewModel viewModel = Assert.IsType<TeamNavigationViewModel>(viewResult.ViewData?.Model);

        Assert.True(viewModel.StrategiesIsActive);
        Assert.False(viewModel.CalendarIsActive);
        Assert.False(viewModel.ManagementIsActive);
    }

    private static TeamNavigationViewComponent CreateComponent(IUserTeamService service, Guid userId, Guid? requestedTeamId, Guid? lastVisitedTeamId, string controllerName, string actionName)
    {
        DefaultHttpContext httpContext = CreateHttpContext(userId, lastVisitedTeamId);
        RouteData routeData = new();

        routeData.Values["controller"] = controllerName;
        routeData.Values["action"] = actionName;

        if (requestedTeamId.HasValue)
        {
            routeData.Values["teamId"] = requestedTeamId.Value;
        }

        ViewDataDictionary viewData = new(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        ViewContext viewContext = new()
        {
            HttpContext = httpContext,
            RouteData = routeData,
            ViewData = viewData
        };
        TeamNavigationViewComponent component = new(service)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = viewContext
            }
        };

        return component;
    }

    private static DefaultHttpContext CreateHttpContext(Guid userId, Guid? lastVisitedTeamId)
    {
        Claim[] claims = [new Claim(ClaimTypes.NameIdentifier, userId.ToString())];
        ClaimsIdentity identity = new(claims, "Test");
        DefaultHttpContext httpContext = new();

        httpContext.User = new ClaimsPrincipal(identity);

        if (lastVisitedTeamId.HasValue)
        {
            httpContext.Request.Headers.Cookie = $"EsportTeamManager.LastVisitedTeamId={lastVisitedTeamId.Value}";
        }

        return httpContext;
    }

    private sealed class StubUserTeamService : IUserTeamService
    {
        private readonly IReadOnlyCollection<UserTeamSummary> _teams;

        public StubUserTeamService(IReadOnlyCollection<UserTeamSummary> teams)
        {
            _teams = teams;
        }

        public Task<CreateTeamResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamManagementDetails?> GetManagementDetailsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_teams);
        }

        public Task<InviteTeamMemberResult> InviteMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(InviteTeamMemberResult.Denied());
        }

        public Task<TeamMembershipActionResult> ChangeMemberRoleAsync(ChangeTeamMemberRoleRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TeamMembershipActionResult.Denied());
        }

        public Task<TeamMembershipActionResult> LeaveTeamAsync(LeaveTeamRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TeamMembershipActionResult.Denied());
        }

        public Task<TeamMembershipActionResult> RemoveMemberAsync(RemoveTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TeamMembershipActionResult.Denied());
        }

        public Task<OwnershipTransferActionResult> AcceptOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(OwnershipTransferActionResult.Denied());
        }

        public Task<OwnershipTransferActionResult> CancelOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(OwnershipTransferActionResult.Denied());
        }

        public Task<OwnershipTransferActionResult> InitiateOwnershipTransferAsync(InitiateOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(OwnershipTransferActionResult.Denied());
        }

        public Task<OwnershipTransferActionResult> RefuseOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(OwnershipTransferActionResult.Denied());
        }

        public Task<UpdateTeamInformationResult> UpdateInformationAsync(UpdateTeamInformationRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(UpdateTeamInformationResult.Denied());
        }

        public Task<DeleteTeamResult> DeleteTeamAsync(DeleteTeamRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(DeleteTeamResult.Denied());
        }
    }
}