using System.Security.Claims;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Web.Models.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EsportTeamManager.Web.Navigation;

namespace EsportTeamManager.Web.Controllers;

[Authorize]
public sealed class TeamsController : Controller
{
    private readonly IUserTeamService _userTeamService;

    public TeamsController(IUserTeamService userTeamService)
    {
        _userTeamService = userTeamService;
    }

    [HttpGet]
    public async Task<IActionResult> Entry(CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        IReadOnlyCollection<UserTeamSummary> teams = await _userTeamService.GetTeamsForUserAsync(userId.Value, cancellationToken);

        if (teams.Count == 0)
        {
            LastVisitedTeamCookie.Delete(Response);

            return RedirectToAction(nameof(Index));
        }

        if (teams.Count == 1)
        {
            UserTeamSummary team = teams.Single();

            LastVisitedTeamCookie.Write(Response, team.TeamId);

            return RedirectToAction("Index", "Activities", new { teamId = team.TeamId });
        }

        Guid? lastVisitedTeamId = LastVisitedTeamCookie.Read(Request);

        if (!lastVisitedTeamId.HasValue)
        {
            return RedirectToAction(nameof(Index));
        }

        UserTeamSummary? lastVisitedTeam = teams.SingleOrDefault(team => team.TeamId == lastVisitedTeamId.Value);

        if (lastVisitedTeam is null)
        {
            LastVisitedTeamCookie.Delete(Response);

            return RedirectToAction(nameof(Index));
        }

        LastVisitedTeamCookie.Write(Response, lastVisitedTeam.TeamId);

        return RedirectToAction("Index", "Activities", new { teamId = lastVisitedTeam.TeamId });
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        TeamsIndexViewModel viewModel = await BuildIndexViewModelAsync(userId.Value, new CreateTeamViewModel(), cancellationToken);

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Management(Guid teamId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, teamId, cancellationToken);

        if (details is null)
        {
            return Forbid();
        }

        IReadOnlyCollection<TeamRoleOptionViewModel> availableRoles =
        [
            .. details.AvailableMemberRoles.Select(role => new TeamRoleOptionViewModel(role.TeamRoleId, role.Label))
        ];
        IReadOnlyCollection<TeamMemberViewModel> members =
        [
            .. details.Members.Select(member => new TeamMemberViewModel(member.TeamMembershipId, member.Pseudo, member.Tag, member.TeamRoleId, member.RoleLabel, member.IsOwner, member.CanChangeRole, member.CanRemove, member.JoinedAtUtc))
        ];
        TeamManagementViewModel viewModel = new(details.TeamId, details.Name, details.Tag, details.Description, details.TimeZoneId, details.CurrentUserIsOwner, details.CurrentUserCanInviteMembers, details.CurrentUserCanLeaveTeam, availableRoles, members);

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> ChangeRole(Guid teamId, Guid teamMembershipId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty || teamMembershipId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, teamId, cancellationToken);

        if (details is null)
        {
            return Forbid();
        }

        TeamMemberSummary? member = details.Members.SingleOrDefault(candidate => candidate.TeamMembershipId == teamMembershipId);

        if (member is null || !member.CanChangeRole)
        {
            return Forbid();
        }

        ChangeTeamMemberRoleViewModel viewModel = BuildChangeRoleViewModel(details, member);

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeRole(ChangeTeamMemberRoleViewModel model, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (model.TeamId == Guid.Empty || model.TeamMembershipId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, model.TeamId, cancellationToken);

        if (details is null)
        {
            return Forbid();
        }

        TeamMemberSummary? member = details.Members.SingleOrDefault(candidate => candidate.TeamMembershipId == model.TeamMembershipId);

        if (member is null || !member.CanChangeRole)
        {
            return Forbid();
        }

        ChangeTeamMemberRoleViewModel viewModel = BuildChangeRoleViewModel(details, member, model);

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        ChangeTeamMemberRoleRequest request = new(userId.Value, viewModel.TeamId, viewModel.TeamMembershipId, viewModel.NewTeamRoleId);
        TeamMembershipActionResult result = await _userTeamService.ChangeMemberRoleAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(viewModel);
        }

        TempData["SuccessMessage"] = "Le rôle du membre a été modifié avec succès.";

        return RedirectToAction(nameof(Management), new { teamId = viewModel.TeamId });
    }

    [HttpGet]
    public async Task<IActionResult> RemoveMember(Guid teamId, Guid teamMembershipId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty || teamMembershipId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, teamId, cancellationToken);

        if (details is null)
        {
            return Forbid();
        }

        TeamMemberSummary? member = details.Members.SingleOrDefault(candidate => candidate.TeamMembershipId == teamMembershipId);

        if (member is null || !member.CanRemove)
        {
            return Forbid();
        }

        RemoveTeamMemberViewModel viewModel = BuildRemoveMemberViewModel(details, member);

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveMember(RemoveTeamMemberViewModel model, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (model.TeamId == Guid.Empty || model.TeamMembershipId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, model.TeamId, cancellationToken);

        if (details is null)
        {
            return Forbid();
        }

        TeamMemberSummary? member = details.Members.SingleOrDefault(candidate => candidate.TeamMembershipId == model.TeamMembershipId);

        if (member is null || !member.CanRemove)
        {
            return Forbid();
        }

        RemoveTeamMemberViewModel viewModel = BuildRemoveMemberViewModel(details, member);

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        RemoveTeamMemberRequest request = new(userId.Value, viewModel.TeamId, viewModel.TeamMembershipId);
        TeamMembershipActionResult result = await _userTeamService.RemoveMemberAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(viewModel);
        }

        TempData["SuccessMessage"] = "Le membre a été exclu de l’équipe.";

        return RedirectToAction(nameof(Management), new { teamId = viewModel.TeamId });
    }

    [HttpGet]
    public async Task<IActionResult> LeaveTeam(Guid teamId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, teamId, cancellationToken);

        if (details is null || !details.CurrentUserCanLeaveTeam)
        {
            return Forbid();
        }

        LeaveTeamViewModel viewModel = new()
        {
            TeamId = details.TeamId,
            TeamName = details.Name
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> LeaveTeam(LeaveTeamViewModel model, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (model.TeamId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, model.TeamId, cancellationToken);

        if (details is null || !details.CurrentUserCanLeaveTeam)
        {
            return Forbid();
        }

        LeaveTeamViewModel viewModel = new()
        {
            TeamId = details.TeamId,
            TeamName = details.Name
        };

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        LeaveTeamRequest request = new(userId.Value, viewModel.TeamId);
        TeamMembershipActionResult result = await _userTeamService.LeaveTeamAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(viewModel);
        }

        LastVisitedTeamCookie.Delete(Response);
        TempData["SuccessMessage"] = "Vous avez quitté l’équipe.";

        return RedirectToAction(nameof(Entry));
    }

    [HttpGet]
    public async Task<IActionResult> Invite(Guid teamId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, teamId, cancellationToken);

        if (details is null || !details.CurrentUserCanInviteMembers)
        {
            return Forbid();
        }

        InviteTeamMemberViewModel viewModel = BuildInviteViewModel(details);

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Invite(InviteTeamMemberViewModel model, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (model.TeamId == Guid.Empty)
        {
            return RedirectToAction(nameof(Entry));
        }

        TeamManagementDetails? details = await _userTeamService.GetManagementDetailsAsync(userId.Value, model.TeamId, cancellationToken);

        if (details is null || !details.CurrentUserCanInviteMembers)
        {
            return Forbid();
        }

        InviteTeamMemberViewModel viewModel = BuildInviteViewModel(details, model);

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        InviteTeamMemberRequest request = new(userId.Value, viewModel.TeamId, viewModel.RecipientIdentity, viewModel.ProposedTeamRoleId);
        InviteTeamMemberResult result = await _userTeamService.InviteMemberAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(viewModel);
        }

        TempData["SuccessMessage"] = "L’invitation a été envoyée avec succès.";

        return RedirectToAction(nameof(Management), new { teamId = viewModel.TeamId });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTeamViewModel model, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TeamsIndexViewModel invalidViewModel = await BuildIndexViewModelAsync(userId.Value, model, cancellationToken);

            return View(nameof(Index), invalidViewModel);
        }

        CreateTeamRequest request = new(userId.Value, model.Name, model.Tag, model.TimeZoneId);
        CreateTeamResult result = await _userTeamService.CreateAsync(request, cancellationToken);

        if (!result.Succeeded || !result.TeamId.HasValue)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            TeamsIndexViewModel failedViewModel = await BuildIndexViewModelAsync(userId.Value, model, cancellationToken);

            return View(nameof(Index), failedViewModel);
        }

        TempData["SuccessMessage"] = "L’équipe a été créée avec succès.";

        return RedirectToAction("Index", "Activities", new { teamId = result.TeamId.Value });
    }

    private static ChangeTeamMemberRoleViewModel BuildChangeRoleViewModel(TeamManagementDetails details, TeamMemberSummary member, ChangeTeamMemberRoleViewModel? model = null)
    {
        ChangeTeamMemberRoleViewModel viewModel = model ?? new ChangeTeamMemberRoleViewModel
        {
            NewTeamRoleId = member.TeamRoleId
        };

        viewModel.TeamId = details.TeamId;
        viewModel.TeamName = details.Name;
        viewModel.TeamMembershipId = member.TeamMembershipId;
        viewModel.MemberIdentity = $"{member.Pseudo}#{member.Tag}";
        viewModel.AvailableRoles =
        [
            .. details.AvailableMemberRoles.Select(role => new TeamRoleOptionViewModel(role.TeamRoleId, role.Label))
        ];

        return viewModel;
    }

    private static RemoveTeamMemberViewModel BuildRemoveMemberViewModel(TeamManagementDetails details, TeamMemberSummary member)
    {
        return new RemoveTeamMemberViewModel
        {
            TeamId = details.TeamId,
            TeamName = details.Name,
            TeamMembershipId = member.TeamMembershipId,
            MemberIdentity = $"{member.Pseudo}#{member.Tag}"
        };
    }

    private static InviteTeamMemberViewModel BuildInviteViewModel(TeamManagementDetails details, InviteTeamMemberViewModel? model = null)
    {
        InviteTeamMemberViewModel viewModel = model ?? new InviteTeamMemberViewModel();

        viewModel.TeamId = details.TeamId;
        viewModel.TeamName = details.Name;
        viewModel.AvailableRoles =
        [
            .. details.AvailableInvitationRoles.Select(role => new TeamRoleOptionViewModel(role.TeamRoleId, role.Label))
        ];

        return viewModel;
    }

    private async Task<TeamsIndexViewModel> BuildIndexViewModelAsync(Guid userId, CreateTeamViewModel createTeam, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<UserTeamSummary> teams = await _userTeamService.GetTeamsForUserAsync(userId, cancellationToken);
        IReadOnlyCollection<TeamCardViewModel> teamCards =
        [
            .. teams.Select(team => new TeamCardViewModel(team.TeamId, team.Name, team.Tag, team.RoleLabel, team.IsOwner, CreateInitials(team.Name, team.Tag)))
        ];

        return new TeamsIndexViewModel(teamCards, createTeam);
    }

    private Guid? GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }

    private static string CreateInitials(string name, string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            string normalizedTag = tag.Trim();

            return normalizedTag[..Math.Min(2, normalizedTag.Length)].ToUpperInvariant();
        }

        string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length >= 2)
        {
            return $"{words[0][0]}{words[1][0]}".ToUpperInvariant();
        }

        string normalizedName = name.Trim();

        return normalizedName[..Math.Min(2, normalizedName.Length)].ToUpperInvariant();
    }
}