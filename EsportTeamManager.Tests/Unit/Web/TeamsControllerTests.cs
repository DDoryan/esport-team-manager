using System.Security.Claims;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EsportTeamManager.Tests.Unit.Web;

public sealed class TeamsControllerTests
{
    [Fact]
    public async Task Entry_WhenUserHasNoTeam_RedirectsToTeamsIndex()
    {
        Guid userId = Guid.NewGuid();
        StubUserTeamService service = new(Array.Empty<UserTeamSummary>());
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.Entry(CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(TeamsController.Index), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
    }

    [Fact]
    public async Task Entry_WhenUserHasOneTeam_RedirectsToItsCalendarAndStoresIt()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        UserTeamSummary team = new(teamId, "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", true);
        StubUserTeamService service = new([team]);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.Entry(CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Activities", redirect.ControllerName);
        Assert.NotNull(redirect.RouteValues);
        Assert.Equal(teamId, Assert.IsType<Guid>(redirect.RouteValues["teamId"]));
        Assert.Contains($"EsportTeamManager.LastVisitedTeamId={teamId}", controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task Entry_WhenUserHasSeveralTeamsWithoutStoredTeam_RedirectsToTeamsIndex()
    {
        Guid userId = Guid.NewGuid();
        UserTeamSummary firstTeam = new(Guid.NewGuid(), "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", true);
        UserTeamSummary secondTeam = new(Guid.NewGuid(), "Valorant Academy", "VAL", "Europe/Paris", "Coach", false);
        StubUserTeamService service = new([firstTeam, secondTeam]);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.Entry(CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(TeamsController.Index), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
    }

    [Fact]
    public async Task Entry_WhenStoredTeamIsAccessible_RedirectsToItsCalendar()
    {
        Guid userId = Guid.NewGuid();
        UserTeamSummary firstTeam = new(Guid.NewGuid(), "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", true);
        UserTeamSummary secondTeam = new(Guid.NewGuid(), "Valorant Academy", "VAL", "Europe/Paris", "Coach", false);
        StubUserTeamService service = new([firstTeam, secondTeam]);
        TeamsController controller = CreateController(service, userId, secondTeam.TeamId);

        IActionResult result = await controller.Entry(CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Activities", redirect.ControllerName);
        Assert.NotNull(redirect.RouteValues);
        Assert.Equal(secondTeam.TeamId, Assert.IsType<Guid>(redirect.RouteValues["teamId"]));
    }

    [Fact]
    public async Task Entry_WhenStoredTeamIsInaccessible_DeletesCookieAndRedirectsToTeamsIndex()
    {
        Guid userId = Guid.NewGuid();
        Guid inaccessibleTeamId = Guid.NewGuid();
        UserTeamSummary firstTeam = new(Guid.NewGuid(), "Phoenix Academy", "PHX", "Europe/Paris", "Joueur", true);
        UserTeamSummary secondTeam = new(Guid.NewGuid(), "Valorant Academy", "VAL", "Europe/Paris", "Coach", false);
        StubUserTeamService service = new([firstTeam, secondTeam]);
        TeamsController controller = CreateController(service, userId, inaccessibleTeamId);

        IActionResult result = await controller.Entry(CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        string setCookie = controller.Response.Headers.SetCookie.ToString();

        Assert.Equal(nameof(TeamsController.Index), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Contains("EsportTeamManager.LastVisitedTeamId=;", setCookie);
    }

    private static TeamsController CreateController(IUserTeamService service, Guid userId, Guid? lastVisitedTeamId = null)
    {
        DefaultHttpContext httpContext = CreateHttpContext(userId, lastVisitedTeamId);
        TeamsController controller = new(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
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
    }
}