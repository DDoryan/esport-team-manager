using System.Security.Claims;
using EsportTeamManager.Application.Activities;
using EsportTeamManager.Application.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepriseWeb.ViewModels.Activities;

namespace RepriseWeb.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private const int MaximumPeriodLengthInDays = 29;

    private readonly IActivityCalendarService _activityCalendarService;
    private readonly IActivityCreationService _activityCreationService;
    private readonly IUserTeamService _userTeamService;

    public ActivitiesController(IActivityCalendarService activityCalendarService, IActivityCreationService activityCreationService, IUserTeamService userTeamService)
    {
        _activityCalendarService = activityCalendarService;
        _activityCreationService = activityCreationService;
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

    private Guid? GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }
}