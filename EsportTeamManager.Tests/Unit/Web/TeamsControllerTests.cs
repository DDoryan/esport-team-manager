using EsportTeamManager.Application.Teams;
using EsportTeamManager.Web.Controllers;
using EsportTeamManager.Web.Models.Teams;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using EsportTeamManager.Application.Images;

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
    public async Task InvitePost_WhenModelIsInvalid_ReturnsManagementWithRehydratedFormWithoutSendingInvitation()
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
        string fieldName = $"{nameof(TeamManagementViewModel.InvitationForm)}.{nameof(InviteTeamMemberViewModel.RecipientIdentity)}";

        controller.ModelState.AddModelError(fieldName, "L’identité du membre est obligatoire.");

        IActionResult result = await controller.Invite(model, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        TeamManagementViewModel viewModel = Assert.IsType<TeamManagementViewModel>(view.Model);

        Assert.Equal(nameof(TeamsController.Management), view.ViewName);
        Assert.Same(model, viewModel.InvitationForm);
        Assert.Equal("Phoenix Academy", viewModel.InvitationForm.TeamName);
        Assert.Single(viewModel.InvitationForm.AvailableRoles);
        Assert.Equal("invitations", controller.ViewData["ActiveManagementSection"]);
        Assert.Null(service.LastInviteRequest);
    }

    [Fact]
    public async Task InvitePost_WhenServiceReturnsFailure_ReturnsManagementWithNeutralError()
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
        TeamManagementViewModel viewModel = Assert.IsType<TeamManagementViewModel>(view.Model);

        Assert.Equal(nameof(TeamsController.Management), view.ViewName);
        Assert.Same(model, viewModel.InvitationForm);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(errorMessage, Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
        Assert.Single(viewModel.InvitationForm.AvailableRoles);
        Assert.Equal("invitations", controller.ViewData["ActiveManagementSection"]);
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
        Assert.Equal(teamId, viewModel.InformationForm.TeamId);
        Assert.Equal("Phoenix Academy", viewModel.InformationForm.TeamName);
        Assert.Equal("Phoenix Academy", viewModel.InformationForm.Name);
        Assert.Equal("PHX", viewModel.InformationForm.Tag);
        Assert.Equal("Europe/Paris", viewModel.InformationForm.TimeZoneId);
        Assert.Contains("Europe/Paris", viewModel.InformationForm.AvailableTimeZoneIds);
    }

    [Fact]
    public async Task TransferOwnershipGet_WhenOwnerCanTransfer_ReturnsFormWithActiveRecipient()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid ownerMembershipId = Guid.NewGuid();
        Guid recipientMembershipId = Guid.NewGuid();
        DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow;
        TeamMemberSummary owner = new(ownerMembershipId, "Owner", "A01", 2, "Coach", true, true, false, joinedAtUtc);
        TeamMemberSummary recipient = new(recipientMembershipId, "Recipient", "B02", 3, "Joueur", false, true, true, joinedAtUtc.AddMinutes(1));
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [], [owner, recipient]);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.TransferOwnership(teamId, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        TransferOwnershipViewModel viewModel = Assert.IsType<TransferOwnershipViewModel>(view.Model);
        OwnershipTransferRecipientOptionViewModel option = Assert.Single(viewModel.AvailableRecipients);
        Assert.Equal(teamId, viewModel.TeamId);
        Assert.Equal("Phoenix Academy", viewModel.TeamName);
        Assert.Equal(recipientMembershipId, option.TeamMembershipId);
        Assert.Equal("Recipient", option.Pseudo);
        Assert.Equal("B02", option.Tag);
        Assert.Equal("Joueur", option.RoleLabel);
    }

    [Fact]
    public async Task TransferOwnershipPost_WhenRequestSucceeds_RedirectsToManagementAndDisplaysConfirmation()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid ownerMembershipId = Guid.NewGuid();
        Guid recipientMembershipId = Guid.NewGuid();
        Guid ownershipTransferId = Guid.NewGuid();
        DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow;
        TeamMemberSummary owner = new(ownerMembershipId, "Owner", "A01", 2, "Coach", true, true, false, joinedAtUtc);
        TeamMemberSummary recipient = new(recipientMembershipId, "Recipient", "B02", 3, "Joueur", false, true, true, joinedAtUtc.AddMinutes(1));
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [], [owner, recipient]);
        StubUserTeamService service = new([], details, initiateOwnershipTransferResult: OwnershipTransferActionResult.Success(ownershipTransferId));
        TeamsController controller = CreateController(service, userId);
        TransferOwnershipViewModel model = new()
        {
            TeamId = teamId,
            RecipientMembershipId = recipientMembershipId
        };

        IActionResult result = await controller.TransferOwnership(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(TeamsController.Management), redirect.ActionName);
        Assert.Equal(teamId, redirect.RouteValues!["teamId"]);
        Assert.Equal("Le transfert de propriété a été proposé avec succès.", controller.TempData["SuccessMessage"]);
        Assert.NotNull(service.LastInitiateOwnershipTransferRequest);
        Assert.Equal(userId, service.LastInitiateOwnershipTransferRequest.InitiatorUserId);
        Assert.Equal(teamId, service.LastInitiateOwnershipTransferRequest.TeamId);
        Assert.Equal(recipientMembershipId, service.LastInitiateOwnershipTransferRequest.RecipientMembershipId);
    }

    [Fact]
    public async Task CancelOwnershipTransferPost_WhenRequestSucceeds_RedirectsToManagementAndDisplaysConfirmation()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid ownershipTransferId = Guid.NewGuid();
        StubUserTeamService service = new([], cancelOwnershipTransferResult: OwnershipTransferActionResult.Success(ownershipTransferId));
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.CancelOwnershipTransfer(teamId, ownershipTransferId, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(TeamsController.Management), redirect.ActionName);
        Assert.Equal(teamId, redirect.RouteValues!["teamId"]);
        Assert.Equal("Le transfert de propriété a été annulé.", controller.TempData["SuccessMessage"]);
        Assert.NotNull(service.LastCancelOwnershipTransferRequest);
        Assert.Equal(userId, service.LastCancelOwnershipTransferRequest.ActorUserId);
        Assert.Equal(teamId, service.LastCancelOwnershipTransferRequest.TeamId);
        Assert.Equal(ownershipTransferId, service.LastCancelOwnershipTransferRequest.OwnershipTransferId);
    }

    [Fact]
    public async Task TransferOwnershipGet_WhenUserIsNotOwner_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", false, true, [], []);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.TransferOwnership(teamId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task TransferOwnershipPost_WhenModelIsInvalid_ReturnsManagementWithRehydratedFormWithoutCreatingTransfer()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid ownerMembershipId = Guid.NewGuid();
        Guid recipientMembershipId = Guid.NewGuid();
        DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow;
        TeamMemberSummary owner = new(ownerMembershipId, "Owner", "A01", 2, "Coach", true, true, false, joinedAtUtc);
        TeamMemberSummary recipient = new(recipientMembershipId, "Recipient", "B02", 3, "Joueur", false, true, true, joinedAtUtc.AddMinutes(1));
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [], [owner, recipient]);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);
        TransferOwnershipViewModel model = new()
        {
            TeamId = teamId
        };
        string fieldName = $"{nameof(TeamManagementViewModel.OwnershipTransferForm)}.{nameof(TransferOwnershipViewModel.RecipientMembershipId)}";

        controller.ModelState.AddModelError(fieldName, "Le nouveau propriétaire est obligatoire.");

        IActionResult result = await controller.TransferOwnership(model, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        TeamManagementViewModel viewModel = Assert.IsType<TeamManagementViewModel>(view.Model);

        Assert.Equal(nameof(TeamsController.Management), view.ViewName);
        Assert.Same(model, viewModel.OwnershipTransferForm);
        Assert.Single(viewModel.OwnershipTransferForm.AvailableRecipients);
        Assert.Equal("Le nouveau propriétaire est obligatoire.", Assert.Single(controller.ModelState[fieldName]!.Errors).ErrorMessage);
        Assert.Equal("ownership", controller.ViewData["ActiveManagementSection"]);
        Assert.Null(service.LastInitiateOwnershipTransferRequest);
    }

    [Fact]
    public async Task ManagementGet_WhenTransferIsPending_ProjectsTransferDetails()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        Guid ownerMembershipId = Guid.NewGuid();
        Guid recipientMembershipId = Guid.NewGuid();
        Guid ownershipTransferId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
        TeamMemberSummary owner = new(ownerMembershipId, "Owner", "A01", 2, "Coach", true, true, false, createdAtUtc.AddDays(-1));
        TeamMemberSummary recipient = new(recipientMembershipId, "Recipient", "B02", 3, "Joueur", false, true, true, createdAtUtc);
        PendingOwnershipTransferSummary pendingTransfer = new(ownershipTransferId, recipientMembershipId, "Recipient", "B02", createdAtUtc);
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [], [owner, recipient], pendingTransfer);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.Management(teamId, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        TeamManagementViewModel viewModel = Assert.IsType<TeamManagementViewModel>(view.Model);
        Assert.NotNull(viewModel.PendingOwnershipTransfer);
        Assert.Equal(ownershipTransferId, viewModel.PendingOwnershipTransfer.OwnershipTransferId);
        Assert.Equal(recipientMembershipId, viewModel.PendingOwnershipTransfer.RecipientMembershipId);
        Assert.Equal("Recipient", viewModel.PendingOwnershipTransfer.RecipientPseudo);
        Assert.Equal("B02", viewModel.PendingOwnershipTransfer.RecipientTag);
        Assert.Equal(createdAtUtc, viewModel.PendingOwnershipTransfer.CreatedAtUtc);
    }

    [Fact]
    public async Task EditInformationGet_WhenCurrentUserIsOwner_RedirectsToManagementInformationSection()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", "Équipe principale.", "Europe/Paris", true, true, [], [], pendingOwnershipTransfer: null, hasLogo: true);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.EditInformation(teamId, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(TeamsController.Management), redirect.ActionName);
        Assert.NotNull(redirect.RouteValues);
        Assert.Equal(teamId, Assert.IsType<Guid>(redirect.RouteValues["teamId"]));
        Assert.Equal("information", Assert.IsType<string>(redirect.RouteValues["section"]));
    }

    [Fact]
    public async Task EditInformationGet_WhenCurrentUserIsNotOwner_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", false, false, [], []);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);

        IActionResult result = await controller.EditInformation(teamId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task EditInformationPost_WhenRequestSucceeds_ForwardsInformationAndLogoThenRedirects()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        byte[] logoBytes = [1, 2, 3, 4];
        await using MemoryStream logoStream = new(logoBytes);
        FormFile logo = new(logoStream, 0, logoBytes.Length, "Logo", "phoenix-logo.png");
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [], []);
        StubUserTeamService service = new([], details, updateInformationResult: UpdateTeamInformationResult.Success());
        TeamsController controller = CreateController(service, userId);
        UpdateTeamInformationViewModel model = new()
        {
            TeamId = teamId,
            Name = "Phoenix Elite",
            Tag = "PHE",
            Description = "Nouvelle description.",
            TimeZoneId = "Europe/London",
            Logo = logo
        };

        IActionResult result = await controller.EditInformation(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);

        Assert.Equal(nameof(TeamsController.Management), redirect.ActionName);
        Assert.NotNull(redirect.RouteValues);
        Assert.Equal(teamId, Assert.IsType<Guid>(redirect.RouteValues["teamId"]));
        Assert.NotNull(service.LastUpdateInformationRequest);
        Assert.Equal(userId, service.LastUpdateInformationRequest.ActorUserId);
        Assert.Equal(teamId, service.LastUpdateInformationRequest.TeamId);
        Assert.Equal("information", Assert.IsType<string>(redirect.RouteValues["section"]));
        Assert.Equal("Phoenix Elite", service.LastUpdateInformationRequest.Name);
        Assert.Equal("PHE", service.LastUpdateInformationRequest.Tag);
        Assert.Equal("Nouvelle description.", service.LastUpdateInformationRequest.Description);
        Assert.Equal("Europe/London", service.LastUpdateInformationRequest.TimeZoneId);
        Assert.Equal("phoenix-logo.png", service.LastUpdateInformationRequest.LogoFileName);
        Assert.True(service.LastUpdateInformationRequest.HasLogo);
        Assert.Equal("Les informations de l’équipe ont été mises à jour.", controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task EditInformationPost_WhenModelIsInvalid_ReturnsManagementWithRehydratedFormWithoutUpdatingTeam()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", "Équipe principale.", "Europe/Paris", true, true, [], [], pendingOwnershipTransfer: null, hasLogo: true);
        StubUserTeamService service = new([], details);
        TeamsController controller = CreateController(service, userId);
        UpdateTeamInformationViewModel model = new()
        {
            TeamId = teamId,
            Name = "Ph",
            Tag = "PHX",
            Description = "Description conservée dans le formulaire.",
            TimeZoneId = "Europe/Paris"
        };
        string fieldName = $"{nameof(TeamManagementViewModel.InformationForm)}.{nameof(UpdateTeamInformationViewModel.Name)}";

        controller.ModelState.AddModelError(fieldName, "Le nom de l’équipe doit contenir entre 3 et 50 caractères.");

        IActionResult result = await controller.EditInformation(model, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        TeamManagementViewModel viewModel = Assert.IsType<TeamManagementViewModel>(view.Model);

        Assert.Equal(nameof(TeamsController.Management), view.ViewName);
        Assert.Same(model, viewModel.InformationForm);
        Assert.Equal("Phoenix Academy", viewModel.InformationForm.TeamName);
        Assert.True(viewModel.InformationForm.HasCurrentLogo);
        Assert.Contains("Europe/Paris", viewModel.InformationForm.AvailableTimeZoneIds);
        Assert.Equal("information", controller.ViewData["ActiveManagementSection"]);
        Assert.Null(service.LastUpdateInformationRequest);
    }

    [Fact]
    public async Task EditInformationPost_WhenServiceReturnsFailure_ReturnsManagementWithNeutralError()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        string errorMessage = "Les informations de l’équipe n’ont pas pu être mises à jour.";
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", "Équipe principale.", "Europe/Paris", true, true, [], []);
        StubUserTeamService service = new([], details, updateInformationResult: UpdateTeamInformationResult.Failure([errorMessage]));
        TeamsController controller = CreateController(service, userId);
        UpdateTeamInformationViewModel model = new()
        {
            TeamId = teamId,
            Name = "Phoenix Elite",
            Tag = "PHE",
            Description = "Nouvelle description.",
            TimeZoneId = "Europe/London"
        };

        IActionResult result = await controller.EditInformation(model, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        TeamManagementViewModel viewModel = Assert.IsType<TeamManagementViewModel>(view.Model);

        Assert.Equal(nameof(TeamsController.Management), view.ViewName);
        Assert.Same(model, viewModel.InformationForm);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(errorMessage, Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage);
        Assert.Contains("Europe/London", viewModel.InformationForm.AvailableTimeZoneIds);
        Assert.Equal("information", controller.ViewData["ActiveManagementSection"]);
        Assert.NotNull(service.LastUpdateInformationRequest);
    }

    [Fact]
    public async Task EditInformationPost_WhenServiceDeniesAccess_ReturnsForbid()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        TeamManagementDetails details = new(teamId, "Phoenix Academy", "PHX", null, "Europe/Paris", true, true, [], []);
        StubUserTeamService service = new([], details, updateInformationResult: UpdateTeamInformationResult.Denied());
        TeamsController controller = CreateController(service, userId);
        UpdateTeamInformationViewModel model = new()
        {
            TeamId = teamId,
            Name = "Phoenix Elite",
            Tag = "PHE",
            TimeZoneId = "Europe/Paris"
        };

        IActionResult result = await controller.EditInformation(model, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Logo_WhenAuthorizedImageExists_ReturnsPrivateWebpStream()
    {
        Guid userId = Guid.NewGuid();
        Guid teamId = Guid.NewGuid();
        MemoryStream contentStream = new([1, 2, 3]);
        PrivateImageContent imageContent = new(contentStream, "image/webp");
        StubPrivateImageService imageService = new(imageContent);
        StubUserTeamService teamService = new([]);
        TeamsController controller = CreateController(teamService, userId, privateImageService: imageService);

        IActionResult result = await controller.Logo(teamId, CancellationToken.None);

        FileStreamResult fileResult = Assert.IsType<FileStreamResult>(result);

        Assert.Same(contentStream, fileResult.FileStream);
        Assert.Equal("image/webp", fileResult.ContentType);
        Assert.Equal("private, no-store", controller.Response.Headers["Cache-Control"].ToString());
    }

    private static TeamsController CreateController(IUserTeamService service, Guid userId, Guid? lastVisitedTeamId = null, IPrivateImageService? privateImageService = null)
    {
        DefaultHttpContext httpContext = CreateHttpContext(userId, lastVisitedTeamId);
        TeamsController controller = new(service, privateImageService ?? new StubPrivateImageService())
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
        private readonly OwnershipTransferActionResult _initiateOwnershipTransferResult;
        private readonly OwnershipTransferActionResult _cancelOwnershipTransferResult;
        private readonly UpdateTeamInformationResult _updateInformationResult;

        public InviteTeamMemberRequest? LastInviteRequest { get; private set; }

        public ChangeTeamMemberRoleRequest? LastChangeMemberRoleRequest { get; private set; }

        public LeaveTeamRequest? LastLeaveTeamRequest { get; private set; }

        public RemoveTeamMemberRequest? LastRemoveMemberRequest { get; private set; }

        public InitiateOwnershipTransferRequest? LastInitiateOwnershipTransferRequest { get; private set; }

        public ResolveOwnershipTransferRequest? LastCancelOwnershipTransferRequest { get; private set; }

        public UpdateTeamInformationRequest? LastUpdateInformationRequest { get; private set; }

        public StubUserTeamService(IReadOnlyCollection<UserTeamSummary> teams, TeamManagementDetails? managementDetails = null, InviteTeamMemberResult? inviteResult = null, TeamMembershipActionResult? changeMemberRoleResult = null, TeamMembershipActionResult? leaveTeamResult = null, TeamMembershipActionResult? removeMemberResult = null, OwnershipTransferActionResult? initiateOwnershipTransferResult = null, OwnershipTransferActionResult? cancelOwnershipTransferResult = null, UpdateTeamInformationResult? updateInformationResult = null)
        {
            _teams = teams;
            _managementDetails = managementDetails;
            _inviteResult = inviteResult ?? InviteTeamMemberResult.Denied();
            _changeMemberRoleResult = changeMemberRoleResult ?? TeamMembershipActionResult.Denied();
            _leaveTeamResult = leaveTeamResult ?? TeamMembershipActionResult.Denied();
            _removeMemberResult = removeMemberResult ?? TeamMembershipActionResult.Denied();
            _initiateOwnershipTransferResult = initiateOwnershipTransferResult ?? OwnershipTransferActionResult.Denied();
            _cancelOwnershipTransferResult = cancelOwnershipTransferResult ?? OwnershipTransferActionResult.Denied();
            _updateInformationResult = updateInformationResult ?? UpdateTeamInformationResult.Denied();
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

        public Task<OwnershipTransferActionResult> AcceptOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(OwnershipTransferActionResult.Denied());
        }

        public Task<OwnershipTransferActionResult> CancelOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastCancelOwnershipTransferRequest = request;

            return Task.FromResult(_cancelOwnershipTransferResult);
        }

        public Task<OwnershipTransferActionResult> InitiateOwnershipTransferAsync(InitiateOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastInitiateOwnershipTransferRequest = request;

            return Task.FromResult(_initiateOwnershipTransferResult);
        }

        public Task<OwnershipTransferActionResult> RefuseOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(OwnershipTransferActionResult.Denied());
        }

        public Task<UpdateTeamInformationResult> UpdateInformationAsync(UpdateTeamInformationRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastUpdateInformationRequest = request;

            return Task.FromResult(_updateInformationResult);
        }
    }

    private sealed class StubPrivateImageService : IPrivateImageService
    {
        private readonly PrivateImageContent? _teamLogo;

        public StubPrivateImageService(PrivateImageContent? teamLogo = null)
        {
            _teamLogo = teamLogo;
        }

        public Task<PrivateImageContent?> GetTeamLogoThumbnailAsync(Guid actorUserId, Guid teamId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_teamLogo);
        }

        public Task<PrivateImageContent?> GetStrategyImageThumbnailAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
        {
            return GetStrategyImageAsync(actorUserId, teamId, strategyId, cancellationToken);
        }

        public Task<PrivateImageContent?> GetStrategyImageAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<PrivateImageContent?>(null);
        }

        public Task<StorePrivateImageResult> ReplaceStrategyImageAsync(ReplaceStrategyImageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StorePrivateImageResult> ReplaceTeamLogoAsync(ReplaceTeamLogoRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(StorePrivateImageResult.Failure(["Non utilisé par ce test."]));
        }

        public Task<StorePrivateImageResult> StoreStrategyImageAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(StorePrivateImageResult.Failure(["Non utilisé par ce test."]));
        }

        public Task<StorePrivateImageResult> StoreTeamLogoAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(StorePrivateImageResult.Failure(["Non utilisé par ce test."]));
        }

        public Task DeleteStrategyImageFilesAsync(Guid strategyId, string optimizedStorageKey, string thumbnailStorageKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
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