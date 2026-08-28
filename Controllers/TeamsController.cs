using System.Security.Claims;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Web.Models.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EsportTeamManager.Web.Navigation;
using EsportTeamManager.Application.Images;

namespace EsportTeamManager.Web.Controllers;

[Authorize]
public sealed class TeamsController : Controller
{
    private readonly IUserTeamService _userTeamService;

    private readonly IPrivateImageService _privateImageService;

    public TeamsController(IUserTeamService userTeamService, IPrivateImageService privateImageService)
    {
        _userTeamService = userTeamService;
        _privateImageService = privateImageService;
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

        TeamManagementViewModel viewModel = BuildManagementViewModel(details);

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Logo(Guid teamId, CancellationToken cancellationToken)
    {
        Guid? userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty)
        {
            return NotFound();
        }

        PrivateImageContent? image = await _privateImageService.GetTeamLogoThumbnailAsync(userId.Value, teamId, cancellationToken);

        if (image is null)
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "private, no-store";

        return File(image.Content, image.MediaType);
    }

    [HttpGet]
    public async Task<IActionResult> EditInformation(Guid teamId, CancellationToken cancellationToken)
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

        if (details is null || !details.CurrentUserIsOwner)
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Management), new { teamId, section = "information" });
    }

    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = 3_145_728)]
    public async Task<IActionResult> EditInformation([Bind(Prefix = nameof(TeamManagementViewModel.InformationForm))] UpdateTeamInformationViewModel model, CancellationToken cancellationToken)
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

        if (details is null || !details.CurrentUserIsOwner)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            ViewData["ActiveManagementSection"] = "information";
            TeamManagementViewModel managementViewModel = BuildManagementViewModel(details, informationForm: model);

            return View(nameof(Management), managementViewModel);
        }

        Stream? logoContent = null;
        UpdateTeamInformationResult result;

        try
        {
            if (model.Logo is not null)
            {
                logoContent = model.Logo.OpenReadStream();
            }

            UpdateTeamInformationRequest request = new(userId.Value, model.TeamId, model.Name, model.Tag, model.Description, model.TimeZoneId, model.Logo?.FileName, logoContent);
            result = await _userTeamService.UpdateInformationAsync(request, cancellationToken);
        }
        finally
        {
            if (logoContent is not null)
            {
                await logoContent.DisposeAsync();
            }
        }

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

            ViewData["ActiveManagementSection"] = "information";
            TeamManagementViewModel managementViewModel = BuildManagementViewModel(details, informationForm: model);

            return View(nameof(Management), managementViewModel);
        }

        TempData["SuccessMessage"] = "Les informations de l’équipe ont été mises à jour.";

        return RedirectToAction(nameof(Management), new { teamId = model.TeamId, section = "information" });
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
    public async Task<IActionResult> TransferOwnership(Guid teamId, CancellationToken cancellationToken)
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

        if (details is null || !details.CurrentUserIsOwner)
        {
            return Forbid();
        }

        if (details.PendingOwnershipTransfer is not null || details.Members.All(member => member.IsOwner))
        {
            return RedirectToAction(nameof(Management), new { teamId });
        }

        TransferOwnershipViewModel viewModel = BuildTransferOwnershipViewModel(details);

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> TransferOwnership([Bind(Prefix = nameof(TeamManagementViewModel.OwnershipTransferForm))] TransferOwnershipViewModel model, CancellationToken cancellationToken)
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

        if (details is null || !details.CurrentUserIsOwner)
        {
            return Forbid();
        }

        TransferOwnershipViewModel viewModel = BuildTransferOwnershipViewModel(details, model);

        if (model.RecipientMembershipId == Guid.Empty)
        {
            string fieldName = $"{nameof(TeamManagementViewModel.OwnershipTransferForm)}.{nameof(TransferOwnershipViewModel.RecipientMembershipId)}";

            ModelState.AddModelError(fieldName, "Le nouveau propriétaire est obligatoire.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["ActiveManagementSection"] = "ownership";
            TeamManagementViewModel managementViewModel = BuildManagementViewModel(details, ownershipTransferForm: viewModel);

            return View(nameof(Management), managementViewModel);
        }

        InitiateOwnershipTransferRequest request = new(userId.Value, viewModel.TeamId, viewModel.RecipientMembershipId!.Value);
        OwnershipTransferActionResult result = await _userTeamService.InitiateOwnershipTransferAsync(request, cancellationToken);

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

            ViewData["ActiveManagementSection"] = "ownership";
            TeamManagementViewModel managementViewModel = BuildManagementViewModel(details, ownershipTransferForm: viewModel);

            return View(nameof(Management), managementViewModel);
        }

        TempData["SuccessMessage"] = "Le transfert de propriété a été proposé avec succès.";

        return RedirectToAction(nameof(Management), new { teamId = viewModel.TeamId, section = "ownership" });
    }

    [HttpPost]
    public async Task<IActionResult> CancelOwnershipTransfer(Guid teamId, Guid ownershipTransferId, CancellationToken cancellationToken)
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

        if (ownershipTransferId == Guid.Empty)
        {
            return RedirectToAction(nameof(Management), new { teamId, section = "ownership" });
        }

        ResolveOwnershipTransferRequest request = new(userId.Value, teamId, ownershipTransferId);
        OwnershipTransferActionResult result = await _userTeamService.CancelOwnershipTransferAsync(request, cancellationToken);

        if (result.AccessDenied)
        {
            return Forbid();
        }

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault() ?? "Le transfert de propriété n’a pas pu être annulé.";

            return RedirectToAction(nameof(Management), new { teamId, section = "ownership" });
        }

        TempData["SuccessMessage"] = "Le transfert de propriété a été annulé.";

        return RedirectToAction(nameof(Management), new { teamId, section = "ownership" });
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
    public async Task<IActionResult> Invite([Bind(Prefix = nameof(TeamManagementViewModel.InvitationForm))] InviteTeamMemberViewModel model, CancellationToken cancellationToken)
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
            ViewData["ActiveManagementSection"] = "invitations";
            TeamManagementViewModel managementViewModel = BuildManagementViewModel(details, invitationForm: viewModel);

            return View(nameof(Management), managementViewModel);
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

            ViewData["ActiveManagementSection"] = "invitations";
            TeamManagementViewModel managementViewModel = BuildManagementViewModel(details, invitationForm: viewModel);

            return View(nameof(Management), managementViewModel);
        }

        TempData["SuccessMessage"] = "L’invitation a été envoyée avec succès.";

        return RedirectToAction(nameof(Management), new { teamId = viewModel.TeamId, section = "invitations" });
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

    private static TeamManagementViewModel BuildManagementViewModel(TeamManagementDetails details, InviteTeamMemberViewModel? invitationForm = null, TransferOwnershipViewModel? ownershipTransferForm = null, UpdateTeamInformationViewModel? informationForm = null)
    {
        IReadOnlyCollection<TeamRoleOptionViewModel> availableRoles =
        [
            .. details.AvailableMemberRoles.Select(role => new TeamRoleOptionViewModel(role.TeamRoleId, role.Label))
        ];
        IReadOnlyCollection<TeamMemberViewModel> members =
        [
            .. details.Members.Select(member => new TeamMemberViewModel(member.TeamMembershipId, member.Pseudo, member.Tag, member.TeamRoleId, member.RoleLabel, member.IsOwner, member.CanChangeRole, member.CanRemove, member.JoinedAtUtc))
        ];

        PendingOwnershipTransferViewModel? pendingOwnershipTransfer = null;

        if (details.PendingOwnershipTransfer is not null)
        {
            PendingOwnershipTransferSummary transfer = details.PendingOwnershipTransfer;

            pendingOwnershipTransfer = new PendingOwnershipTransferViewModel(transfer.OwnershipTransferId, transfer.RecipientMembershipId, transfer.RecipientPseudo, transfer.RecipientTag, transfer.CreatedAtUtc);
        }

        UpdateTeamInformationViewModel preparedInformationForm = BuildUpdateInformationViewModel(details, informationForm);
        InviteTeamMemberViewModel preparedInvitationForm = BuildInviteViewModel(details, invitationForm);
        TransferOwnershipViewModel preparedOwnershipTransferForm = BuildTransferOwnershipViewModel(details, ownershipTransferForm);

        return new TeamManagementViewModel(details.TeamId, details.Name, details.Tag, details.Description, details.TimeZoneId, details.CurrentUserIsOwner, details.CurrentUserCanInviteMembers, details.CurrentUserCanLeaveTeam, availableRoles, members, pendingOwnershipTransfer, details.HasLogo, preparedInvitationForm, preparedOwnershipTransferForm, preparedInformationForm);
    }

    private static TransferOwnershipViewModel BuildTransferOwnershipViewModel(TeamManagementDetails details, TransferOwnershipViewModel? model = null)
    {
        TransferOwnershipViewModel viewModel = model ?? new TransferOwnershipViewModel();

        viewModel.TeamId = details.TeamId;
        viewModel.TeamName = details.Name;
        viewModel.AvailableRecipients =
        [
            .. details.Members
            .Where(member => !member.IsOwner)
            .Select(member => new OwnershipTransferRecipientOptionViewModel(member.TeamMembershipId, member.Pseudo, member.Tag, member.RoleLabel))
        ];

        return viewModel;
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

    private static UpdateTeamInformationViewModel BuildUpdateInformationViewModel(TeamManagementDetails details, UpdateTeamInformationViewModel? model = null)
    {
        UpdateTeamInformationViewModel viewModel = model ?? new UpdateTeamInformationViewModel
        {
            Name = details.Name,
            Tag = details.Tag,
            Description = details.Description,
            TimeZoneId = details.TimeZoneId
        };

        viewModel.TeamId = details.TeamId;
        viewModel.TeamName = details.Name;
        viewModel.HasCurrentLogo = details.HasLogo;
        viewModel.AvailableTimeZoneIds = GetAvailableIanaTimeZoneIds(details.TimeZoneId);

        return viewModel;
    }

    private static IReadOnlyCollection<string> GetAvailableIanaTimeZoneIds(string currentTimeZoneId)
    {
        HashSet<string> timeZoneIds = new(StringComparer.Ordinal)
    {
        "Europe/Paris",
        currentTimeZoneId
    };

        foreach (TimeZoneInfo timeZone in TimeZoneInfo.GetSystemTimeZones())
        {
            if (timeZone.HasIanaId)
            {
                timeZoneIds.Add(timeZone.Id);

                continue;
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out string? ianaTimeZoneId))
            {
                timeZoneIds.Add(ianaTimeZoneId);
            }
        }

        return [.. timeZoneIds.OrderBy(timeZoneId => timeZoneId)];
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