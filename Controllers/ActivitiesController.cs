using System.Security.Claims;
using EsportTeamManager.Application.Activities;
using EsportTeamManager.Application.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.ViewModels.Activities;
using EsportTeamManager.Domain.Enums;

namespace RepriseWeb.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private const int MaximumPeriodLengthInDays = 29;

    private readonly IActivityCalendarService _activityCalendarService;
    private readonly IActivityCreationService _activityCreationService;
    private readonly IActivityEditingService _activityEditingService;
    private readonly IUserTeamService _userTeamService;

    public ActivitiesController(IActivityCalendarService activityCalendarService, IActivityCreationService activityCreationService, IActivityEditingService activityEditingService, IUserTeamService userTeamService)
    {
        _activityCalendarService = activityCalendarService;
        _activityCreationService = activityCreationService;
        _activityEditingService = activityEditingService;
        _userTeamService = userTeamService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid teamId, CancellationToken cancellationToken)
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

        bool canCreateActivity = await _activityCreationService.CanCreateAsync(currentUserId.Value, teamId, cancellationToken);
        TeamCalendarViewModel viewModel = new(currentTeam.TeamId, currentTeam.Name, currentTeam.TimeZoneId, canCreateActivity);

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

        ActivityCreationOptions? options = await _activityCreationService.GetOptionsAsync(currentUserId.Value, teamId, cancellationToken);

        if (options is null)
        {
            return Forbid();
        }

        CreateActivityViewModel viewModel = BuildCreateViewModel(options, new CreateActivityViewModel());

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateActivityViewModel model, CancellationToken cancellationToken)
    {
        Guid? currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        if (model.TeamId == Guid.Empty)
        {
            return BadRequest();
        }

        ActivityCreationOptions? options = await _activityCreationService.GetOptionsAsync(currentUserId.Value, model.TeamId, cancellationToken);

        if (options is null)
        {
            return Forbid();
        }

        if (model.ParticipantMembershipIds is null || model.ParticipantMembershipIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.ParticipantMembershipIds), "Sélectionnez au moins un participant.");
        }

        if (model.PlannedStartLocal.HasValue && model.PlannedEndLocal.HasValue && model.PlannedEndLocal.Value <= model.PlannedStartLocal.Value)
        {
            ModelState.AddModelError(nameof(model.PlannedEndLocal), "La fin prévue doit être strictement postérieure au début prévu.");
        }

        if (!ModelState.IsValid)
        {
            CreateActivityViewModel invalidViewModel = BuildCreateViewModel(options, model);

            return View(invalidViewModel);
        }

        IReadOnlyCollection<CreateActivityLinkRequest> links = (model.Links ?? [])
        .Select(link => new CreateActivityLinkRequest(link.Name ?? string.Empty, link.Url ?? string.Empty))
        .ToArray();

        CreateActivityRequest request = new(
            currentUserId.Value,
            model.TeamId,
            model.ActivityTypeId!.Value,
            model.PlannedStartLocal!.Value,
            model.PlannedEndLocal!.Value,
            model.ParticipantMembershipIds ?? [],
            model.Subtitle,
            model.Description,
            model.OpponentName,
            links);

        CreateActivityResult result = await _activityCreationService.CreateAsync(request, cancellationToken);

        if (!result.Succeeded || !result.ActivityId.HasValue)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            CreateActivityViewModel failedViewModel = BuildCreateViewModel(options, model);

            return View(failedViewModel);
        }

        TempData["SuccessMessage"] = "L’activité a été créée avec succès.";

        return RedirectToAction(nameof(Index), new { teamId = model.TeamId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid teamId, Guid activityId, CancellationToken cancellationToken)
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

        if (activityId == Guid.Empty)
        {
            return BadRequest();
        }

        UserTeamSummary? currentTeam = await FindCurrentTeamAsync(currentUserId.Value, teamId, cancellationToken);

        if (currentTeam is null)
        {
            return Forbid();
        }

        ActivityEditDetails? details = await _activityEditingService.GetAsync(currentUserId.Value, teamId, activityId, cancellationToken);

        if (details is null)
        {
            return NotFound();
        }

        EditActivityViewModel viewModel = BuildEditViewModel(details);

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EditActivityViewModel model, CancellationToken cancellationToken)
    {
        Guid? currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        if (model.TeamId == Guid.Empty || model.ActivityId == Guid.Empty)
        {
            return BadRequest();
        }

        UserTeamSummary? currentTeam = await FindCurrentTeamAsync(currentUserId.Value, model.TeamId, cancellationToken);

        if (currentTeam is null)
        {
            return Forbid();
        }

        ActivityEditDetails? details = await _activityEditingService.GetAsync(currentUserId.Value, model.TeamId, model.ActivityId, cancellationToken);

        if (details is null)
        {
            return NotFound();
        }

        if (!details.CanEdit)
        {
            return Forbid();
        }

        if (model.PlannedStartLocal.HasValue && model.PlannedEndLocal.HasValue && model.PlannedEndLocal.Value <= model.PlannedStartLocal.Value)
        {
            ModelState.AddModelError(nameof(model.PlannedEndLocal), "La fin prévue doit être strictement postérieure au début prévu.");
        }

        if (!ModelState.IsValid)
        {
            EditActivityViewModel invalidViewModel = BuildEditViewModel(details, model);

            return View(invalidViewModel);
        }

        IReadOnlyCollection<UpdateActivityLinkRequest> links = (model.Links ?? [])
            .Select(link => new UpdateActivityLinkRequest(link.ActivityLinkId, link.Name ?? string.Empty, link.Url ?? string.Empty))
            .ToArray();

        UpdateActivityRequest request = new(
            currentUserId.Value,
            model.TeamId,
            model.ActivityId,
            model.ActivityTypeId!.Value,
            model.PlannedStartLocal!.Value,
            model.PlannedEndLocal!.Value,
            model.Subtitle,
            model.Description,
            model.Report,
            links);
        UpdateActivityResult result = await _activityEditingService.UpdateAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            EditActivityViewModel failedViewModel = BuildEditViewModel(details, model);

            return View(failedViewModel);
        }

        TempData["SuccessMessage"] = "Les modifications de l’activité ont été enregistrées.";

        return RedirectToAction(nameof(Edit), new { teamId = model.TeamId, activityId = model.ActivityId });
    }

    [HttpGet]
    public async Task<IActionResult> Events(Guid teamId, DateTimeOffset? start, DateTimeOffset? end, bool includeCancelled = false, CancellationToken cancellationToken = default)
    {
        Guid? currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        if (teamId == Guid.Empty)
        {
            return BadRequest();
        }

        UserTeamSummary? currentTeam = await FindCurrentTeamAsync(currentUserId.Value, teamId, cancellationToken);

        if (currentTeam is null)
        {
            return Forbid();
        }

        if (!start.HasValue || !end.HasValue)
        {
            return BadRequest();
        }

        DateTimeOffset normalizedStart = start.Value.ToUniversalTime();
        DateTimeOffset normalizedEnd = end.Value.ToUniversalTime();

        if (normalizedEnd <= normalizedStart || normalizedEnd - normalizedStart > TimeSpan.FromDays(MaximumPeriodLengthInDays))
        {
            return BadRequest();
        }

        IReadOnlyCollection<CalendarActivitySummary> activities = await _activityCalendarService.GetForPeriodAsync(teamId, normalizedStart, normalizedEnd, includeCancelled, cancellationToken);
        IReadOnlyCollection<ActivityCalendarEventViewModel> events = activities
            .Select(activity => new ActivityCalendarEventViewModel(
                activity.ActivityId,
                Url.Action(nameof(Edit), "Activities", new { teamId, activityId = activity.ActivityId }) ?? string.Empty,
                activity.TypeLabel,
                activity.TypeCode,
                activity.Subtitle,
                activity.OpponentName,
                activity.PlannedStartUtc,
                activity.PlannedEndUtc,
                activity.TimeZoneId,
                activity.Status.ToString()))
            .ToArray();

        return Json(events);
    }

    private async Task<UserTeamSummary?> FindCurrentTeamAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<UserTeamSummary> userTeams = await _userTeamService.GetTeamsForUserAsync(userId, cancellationToken);

        return userTeams.SingleOrDefault(team => team.TeamId == teamId);
    }

    private static CreateActivityViewModel BuildCreateViewModel(ActivityCreationOptions options, CreateActivityViewModel model)
    {
        model.TeamId = options.TeamId;
        model.TeamName = options.TeamName;
        model.TimeZoneId = options.TimeZoneId;
        model.ActivityTypes = options.ActivityTypes
            .Select(activityType => new ActivityTypeOptionViewModel(activityType.ActivityTypeId, activityType.Code, activityType.Label))
            .ToArray();
        model.Participants = options.Participants
            .Select(participant => new ActivityParticipantOptionViewModel(
                participant.TeamMembershipId,
                participant.Pseudo,
                participant.Tag,
                participant.RoleLabel,
                participant.IsOwner))
            .ToArray();

        return model;
    }

    private static EditActivityViewModel BuildEditViewModel(ActivityEditDetails details, EditActivityViewModel? model = null)
    {
        bool initializeEditableValues = model is null;
        EditActivityViewModel viewModel = model ?? new EditActivityViewModel();

        viewModel.TeamId = details.TeamId;
        viewModel.ActivityId = details.ActivityId;
        viewModel.TeamName = details.TeamName;
        viewModel.TimeZoneId = details.TimeZoneId;
        viewModel.StatusLabel = CreateStatusLabel(details.Status);
        viewModel.CancellationReason = details.CancellationReason;
        viewModel.OpponentName = details.OpponentName;
        viewModel.TeamScore = details.TeamScore;
        viewModel.OpponentScore = details.OpponentScore;
        viewModel.UpdatedAtUtc = details.UpdatedAtUtc;
        viewModel.CanEdit = details.CanEdit;
        viewModel.ActivityTypes = details.ActivityTypes
            .Select(activityType => new ActivityTypeOptionViewModel(activityType.ActivityTypeId, activityType.Code, activityType.Label))
            .ToArray();
        viewModel.Participants = details.Participants
            .Select(participant => new ActivityEditParticipantViewModel(participant.TeamMembershipId, participant.DisplayName, participant.RoleLabel, participant.IsOwner, CreateAttendanceLabel(participant.Attendance)))
            .ToArray();

        if (initializeEditableValues)
        {
            viewModel.ActivityTypeId = details.ActivityTypeId;
            viewModel.Subtitle = details.Subtitle;
            viewModel.PlannedStartLocal = details.PlannedStartLocal;
            viewModel.PlannedEndLocal = details.PlannedEndLocal;
            viewModel.Description = details.Description;
            viewModel.Report = details.Report;
            viewModel.Links = details.Links
                .Select(link => new ActivityEditLinkViewModel(link.ActivityLinkId, link.Name, link.Url))
                .ToList();
        }

        ActivityTypeOptionViewModel? selectedActivityType = viewModel.ActivityTypes.SingleOrDefault(activityType => activityType.ActivityTypeId == viewModel.ActivityTypeId);
        viewModel.TypeCode = selectedActivityType?.Code ?? details.TypeCode;
        viewModel.TypeLabel = selectedActivityType?.Label ?? details.TypeLabel;

        return viewModel;
    }

    private static string CreateStatusLabel(ActivityStatus status)
    {
        return status switch
        {
            ActivityStatus.Planned => "Planifiée",
            ActivityStatus.Completed => "Terminée",
            ActivityStatus.Cancelled => "Annulée",
            _ => status.ToString()
        };
    }

    private static string CreateAttendanceLabel(Attendance? attendance)
    {
        return attendance switch
        {
            Attendance.Present => "Présent",
            Attendance.Absent => "Absent",
            _ => "Non renseignée"
        };
    }

    private Guid? GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }
}