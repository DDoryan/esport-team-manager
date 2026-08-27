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

    [Fact]
    public async Task ChangeRoleGet_WhenMemberCanBeManaged_ReturnsPrefilledForm()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        TeamMemberSummary member = new(membershipId, "Member", "B02", 3, "Joueur", false, true, true, DateTimeOffset.UtcNow);
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(1, "Manager"), new TeamRoleOption(2, "Coach"), new TeamRoleOption(3, "Joueur")], [member]);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.ChangeRole(teamId, membershipId, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        ChangeTeamMemberRoleViewModel viewModel = Assert.IsType<ChangeTeamMemberRoleViewModel>(view.Model);

        Assert.Equal(teamId, viewModel.TeamId);
        Assert.Equal("Phoenix Academy", viewModel.TeamName);
        Assert.Equal(membershipId, viewModel.TeamMembershipId);
        Assert.Equal("Member#B02", viewModel.MemberIdentity);
        Assert.Equal(3, viewModel.NewTeamRoleId);
        Assert.Equal(3, viewModel.AvailableRoles.Count);
    }

    [Fact]
    public async Task ChangeRolePost_WhenRequestSucceeds_RedirectsToManagementAndDisplaysConfirmation()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        TeamMemberSummary member = new(membershipId, "Member", "B02", 3, "Joueur", false, true, true, DateTimeOffset.UtcNow);
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(1, "Manager"), new TeamRoleOption(2, "Coach"), new TeamRoleOption(3, "Joueur")], [member]);
        StubUserTeamService service = new([], details, changeMemberRoleResult: TeamMembershipActionResult.Success());
        TeamsController controller = CreateController(service, userId);
        ChangeTeamMemberRoleViewModel model = new()
        {
            TeamId = teamId,
            TeamMembershipId = membershipId,
            NewTeamRoleId = 2
        };

        IActionResult result = await controller.ChangeRole(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(TeamsController.Management), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.NotNull(redirect.RouteValues);
        Assert.Equal(teamId, Assert.IsType<Guid>(redirect.RouteValues["teamId"]));
        Assert.Equal("Le rôle du membre a été modifié avec succès.", controller.TempData["SuccessMessage"]);

        Assert.NotNull(service.LastChangeMemberRoleRequest);
        Assert.Equal(userId, service.LastChangeMemberRoleRequest.ActorUserId);
        Assert.Equal(teamId, service.LastChangeMemberRoleRequest.TeamId);
        Assert.Equal(membershipId, service.LastChangeMemberRoleRequest.TeamMembershipId);
        Assert.Equal(2, service.LastChangeMemberRoleRequest.NewTeamRoleId);
    }

    [Fact]
    public async Task ChangeRoleGet_WhenMemberCannotBeManaged_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        TeamMemberSummary member = new(membershipId, "Owner", "A01", 3, "Joueur", true, false, false, DateTimeOffset.UtcNow);
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", false, true, [new TeamRoleOption(2, "Coach"), new TeamRoleOption(3, "Joueur")], [member]);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.ChangeRole(teamId, membershipId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(service.LastChangeMemberRoleRequest);
    }

    [Fact]
    public async Task RemoveMemberPost_WhenRequestSucceeds_RedirectsToManagementAndDisplaysConfirmation()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        TeamMemberSummary member = new(membershipId, "Member", "B02", 3, "Joueur", false, true, true, DateTimeOffset.UtcNow);
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(1, "Manager"), new TeamRoleOption(2, "Coach"), new TeamRoleOption(3, "Joueur")], [member]);
        StubUserTeamService service = new([], details, removeMemberResult: TeamMembershipActionResult.Success());
        TeamsController controller = CreateController(service, userId);
        RemoveTeamMemberViewModel model = new()
        {
            TeamId = teamId,
            TeamMembershipId = membershipId
        };

        IActionResult result = await controller.RemoveMember(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(TeamsController.Management), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.NotNull(redirect.RouteValues);
        Assert.Equal(teamId, Assert.IsType<Guid>(redirect.RouteValues["teamId"]));
        Assert.Equal("Le membre a été exclu de l’équipe.", controller.TempData["SuccessMessage"]);

        Assert.NotNull(service.LastRemoveMemberRequest);
        Assert.Equal(userId, service.LastRemoveMemberRequest.ActorUserId);
        Assert.Equal(teamId, service.LastRemoveMemberRequest.TeamId);
        Assert.Equal(membershipId, service.LastRemoveMemberRequest.TeamMembershipId);
    }

    [Fact]
    public async Task RemoveMemberGet_WhenMemberCannotBeRemoved_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        TeamMemberSummary member = new(membershipId, "Owner", "A01", 3, "Joueur", true, true, false, DateTimeOffset.UtcNow);
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(1, "Manager"), new TeamRoleOption(2, "Coach"), new TeamRoleOption(3, "Joueur")], [member]);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.RemoveMember(teamId, membershipId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(service.LastRemoveMemberRequest);
    }

    [Fact]
    public async Task LeaveTeamPost_WhenRequestSucceeds_RedirectsToEntryDeletesCookieAndDisplaysConfirmation()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", false, false, [], []);
        StubUserTeamService service = new([], details, leaveTeamResult: TeamMembershipActionResult.Success());
        TeamsController controller = CreateController(service, userId, teamId);
        LeaveTeamViewModel model = new()
        {
            TeamId = teamId
        };

        IActionResult result = await controller.LeaveTeam(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        string setCookie = controller.Response.Headers.SetCookie.ToString();

        Assert.Equal(nameof(TeamsController.Entry), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
        Assert.Equal("Vous avez quitté l’équipe.", controller.TempData["SuccessMessage"]);
        Assert.Contains("EsportTeamManager.LastVisitedTeamId=;", setCookie);

        Assert.NotNull(service.LastLeaveTeamRequest);
        Assert.Equal(userId, service.LastLeaveTeamRequest.UserId);
        Assert.Equal(teamId, service.LastLeaveTeamRequest.TeamId);
    }

    [Fact]
    public async Task LeaveTeamGet_WhenCurrentUserIsOwner_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(1, "Manager"), new TeamRoleOption(2, "Coach"), new TeamRoleOption(3, "Joueur")], []);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.LeaveTeam(teamId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(service.LastLeaveTeamRequest);
    }

    [Fact]
    public async Task Management_WhenUserHasAccess_ReturnsPermissionAwareViewModel()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        TeamMemberSummary member = new(membershipId, "Member", "B02", 3, "Joueur", false, true, true, DateTimeOffset.UtcNow);
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [new TeamRoleOption(1, "Manager"), new TeamRoleOption(2, "Coach"), new TeamRoleOption(3, "Joueur")], [member]);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.Management(teamId, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        TeamManagementViewModel viewModel = Assert.IsType<TeamManagementViewModel>(view.Model);
        TeamMemberViewModel memberViewModel = Assert.Single(viewModel.Members);

        Assert.Equal(teamId, viewModel.TeamId);
        Assert.True(viewModel.CurrentUserIsOwner);
        Assert.True(viewModel.CurrentUserCanInviteMembers);
        Assert.False(viewModel.CurrentUserCanLeaveTeam);
        Assert.Equal(3, viewModel.AvailableRoles.Count);
        Assert.Equal(membershipId, memberViewModel.TeamMembershipId);
        Assert.Equal(3, memberViewModel.TeamRoleId);
        Assert.True(memberViewModel.CanChangeRole);
        Assert.True(memberViewModel.CanRemove);
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
        private readonly TeamMembershipActionResult _changeMemberRoleResult;
        private readonly TeamMembershipActionResult _leaveTeamResult;
        private readonly TeamMembershipActionResult _removeMemberResult;

        public InviteTeamMemberRequest? LastInviteRequest { get; private set; }

        public ChangeTeamMemberRoleRequest? LastChangeMemberRoleRequest { get; private set; }

        public LeaveTeamRequest? LastLeaveTeamRequest { get; private set; }

        public RemoveTeamMemberRequest? LastRemoveMemberRequest { get; private set; }

        public StubUserTeamService(IReadOnlyCollection<UserTeamSummary> teams, TeamManagementDetails? managementDetails = null, InviteTeamMemberResult? inviteResult = null, TeamMembershipActionResult? changeMemberRoleResult = null, TeamMembershipActionResult? leaveTeamResult = null, TeamMembershipActionResult? removeMemberResult = null)
        {
            _teams = teams;
            _managementDetails = managementDetails;
            _inviteResult = inviteResult ?? InviteTeamMemberResult.Denied();
            _changeMemberRoleResult = changeMemberRoleResult ?? TeamMembershipActionResult.Denied();
            _leaveTeamResult = leaveTeamResult ?? TeamMembershipActionResult.Denied();
            _removeMemberResult = removeMemberResult ?? TeamMembershipActionResult.Denied();
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

        public Task<TeamMembershipActionResult> ChangeMemberRoleAsync(ChangeTeamMemberRoleRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastChangeMemberRoleRequest = request;

            return Task.FromResult(_changeMemberRoleResult);
        }

        public Task<TeamMembershipActionResult> LeaveTeamAsync(LeaveTeamRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastLeaveTeamRequest = request;

            return Task.FromResult(_leaveTeamResult);
        }

        public Task<TeamMembershipActionResult> RemoveMemberAsync(RemoveTeamMemberRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastRemoveMemberRequest = request;

            return Task.FromResult(_removeMemberResult);
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