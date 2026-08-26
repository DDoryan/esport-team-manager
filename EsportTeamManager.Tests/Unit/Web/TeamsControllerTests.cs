using EsportTeamManager.Application.Teams;
using EsportTeamManager.Web.Controllers;
using EsportTeamManager.Web.Models.Teams;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace EsportTeamManager.Tests.Unit.Web;

public sealed class TeamsControllerTests
{
    [Fact]
    public async Task Entry_WhenUserHasNoTeam_RedirectsToTeamsIndex()
    {
        Guid userId = Guid.NewGuid();
        StubUserTeamService service = new([]);
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

    [Fact]
    public async Task InviteGet_WhenUserCanInvite_ReturnsFormWithAvailableRoles()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(1, "Manager"), new TeamRoleOption(2, "Coach"), new TeamRoleOption(3, "Joueur")], []);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.Invite(teamId, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        InviteTeamMemberViewModel viewModel = Assert.IsType<InviteTeamMemberViewModel>(view.Model);

        Assert.Equal(teamId, viewModel.TeamId);
        Assert.Equal("Phoenix Academy", viewModel.TeamName);
        Assert.Equal(3, viewModel.AvailableRoles.Count);
        Assert.Contains(viewModel.AvailableRoles, role => role.Label == "Manager");
        Assert.Contains(viewModel.AvailableRoles, role => role.Label == "Coach");
        Assert.Contains(viewModel.AvailableRoles, role => role.Label == "Joueur");
    }

    [Fact]
    public async Task InviteGet_WhenUserCannotInvite_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", false, false, [], []);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.Invite(teamId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task InvitePost_WhenRequestSucceeds_RedirectsToManagementAndDisplaysConfirmation()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(3, "Joueur")], []);
        StubUserTeamService service = new([], details, InviteTeamMemberResult.Success(invitationId));
        TeamsController controller = CreateController(service, userId);
        InviteTeamMemberViewModel model = new()
        {
            TeamId = teamId,
            RecipientIdentity = "Recipient#B02",
            ProposedTeamRoleId = 3
        };

        IActionResult result = await controller.Invite(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(TeamsController.Management), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.NotNull(redirect.RouteValues);
        Assert.Equal(teamId, Assert.IsType<Guid>(redirect.RouteValues["teamId"]));
        Assert.Equal("L’invitation a été envoyée avec succès.", controller.TempData["SuccessMessage"]);

        Assert.NotNull(service.LastInviteRequest);
        Assert.Equal(userId, service.LastInviteRequest.SenderUserId);
        Assert.Equal(teamId, service.LastInviteRequest.TeamId);
        Assert.Equal("Recipient#B02", service.LastInviteRequest.RecipientIdentity);
        Assert.Equal(3, service.LastInviteRequest.ProposedTeamRoleId);
    }

    [Fact]
    public async Task InvitePost_WhenModelIsInvalid_ReturnsRehydratedFormWithoutSendingInvitation()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(3, "Joueur")], []);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);
        InviteTeamMemberViewModel model = new()
        {
            TeamId = teamId,
            RecipientIdentity = string.Empty,
            ProposedTeamRoleId = 0
        };

        controller.ModelState.AddModelError(nameof(InviteTeamMemberViewModel.RecipientIdentity), "L’identité du membre est obligatoire.");

        IActionResult result = await controller.Invite(model, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        InviteTeamMemberViewModel viewModel = Assert.IsType<InviteTeamMemberViewModel>(view.Model);

        Assert.Same(model, viewModel);
        Assert.Equal("Phoenix Academy", viewModel.TeamName);
        Assert.Single(viewModel.AvailableRoles);
        Assert.Null(service.LastInviteRequest);
    }

    [Fact]
    public async Task InvitePost_WhenServiceReturnsFailure_RedisplaysFormWithNeutralError()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        string errorMessage = "L’invitation n’a pas pu être envoyée. Vérifiez l’identité saisie et le rôle proposé.";
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(3, "Joueur")], []);
        StubUserTeamService service = new([], details, InviteTeamMemberResult.Failure([errorMessage]));
        TeamsController controller = CreateController(service, userId);
        InviteTeamMemberViewModel model = new()
        {
            TeamId = teamId,
            RecipientIdentity = "Unknown#B02",
            ProposedTeamRoleId = 3
        };

        IActionResult result = await controller.Invite(model, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        InviteTeamMemberViewModel viewModel = Assert.IsType<InviteTeamMemberViewModel>(view.Model);

        Assert.Same(model, viewModel);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(errorMessage, Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
        Assert.Single(viewModel.AvailableRoles);
        Assert.NotNull(service.LastInviteRequest);
    }

    [Fact]
    public async Task InvitePost_WhenServiceReturnsDenied_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", false, true, [new TeamRoleOption(2, "Coach")], []);
        StubUserTeamService service = new([], details, InviteTeamMemberResult.Denied());
        TeamsController controller = CreateController(service, userId);
        InviteTeamMemberViewModel model = new()
        {
            TeamId = teamId,
            RecipientIdentity = "Recipient#B02",
            ProposedTeamRoleId = 2
        };

        IActionResult result = await controller.Invite(model, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.NotNull(service.LastInviteRequest);
    }

    private static TeamsController CreateController(IUserTeamService service, Guid userId, Guid? lastVisitedTeamId = null)
    {
        DefaultHttpContext httpContext = CreateHttpContext(userId, lastVisitedTeamId);
        TeamsController controller = new(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new StubTempDataProvider())
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
        private readonly TeamManagementDetails? _managementDetails;
        private readonly InviteTeamMemberResult _inviteResult;

        public InviteTeamMemberRequest? LastInviteRequest { get; private set; }

        public StubUserTeamService(IReadOnlyCollection<UserTeamSummary> teams, TeamManagementDetails? managementDetails = null, InviteTeamMemberResult? inviteResult = null)
        {
            _teams = teams;
            _managementDetails = managementDetails;
            _inviteResult = inviteResult ?? InviteTeamMemberResult.Denied();
        }

        public Task<CreateTeamResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TeamManagementDetails?> GetManagementDetailsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_managementDetails);
        }

        public Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_teams);
        }

        public Task<InviteTeamMemberResult> InviteMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastInviteRequest = request;

            return Task.FromResult(_inviteResult);
        }
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}