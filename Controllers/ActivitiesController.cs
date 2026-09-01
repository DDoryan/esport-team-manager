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
    private const int MaximumSearchTextLength = 100;

    private static readonly string[] DefaultActivityTypeCodes =
    [
        "Pracc",
    "OfficialMatch",
    "Meeting",
    "VodReview"
    ];

    private static readonly ActivityStatus[] DefaultActivityStatuses =
    [
        ActivityStatus.Planned,
    ActivityStatus.Completed
    ];

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
        IReadOnlyCollection<ActivityCalendarParticipantOption> participantOptions = await _activityCalendarService.GetParticipantOptionsAsync(teamId, cancellationToken);

        IReadOnlyCollection<ActivityCalendarParticipantOptionViewModel> participantOptionViewModels = participantOptions
            .Select(option => new ActivityCalendarParticipantOptionViewModel(
                option.UserId.HasValue
                    ? $"user:{option.UserId.Value:D}"
                    : $"former:{option.FormerMemberId.GetValueOrDefault():D}",
                option.DisplayName))
            .ToArray();

        TeamCalendarViewModel viewModel = new(currentTeam.TeamId, currentTeam.Name, currentTeam.TimeZoneId, canCreateActivity, participantOptionViewModels);

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

        ActivityTypeOption? selectedActivityType = options.ActivityTypes.SingleOrDefault(activityType => activityType.ActivityTypeId == model.ActivityTypeId);
        bool selectedActivityTypeRequiresOpponent = selectedActivityType?.Code is "Pracc" or "OfficialMatch";

        if (selectedActivityTypeRequiresOpponent && string.IsNullOrWhiteSpace(model.OpponentName))
        {
            ModelState.AddModelError(nameof(model.OpponentName), "L’équipe adverse est obligatoire pour une pracc ou un match officiel.");
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

        ActivityTypeOption? selectedActivityType = details.ActivityTypes.SingleOrDefault(activityType => activityType.ActivityTypeId == model.ActivityTypeId);
        bool selectedActivityTypeRequiresScores = selectedActivityType?.Code is "Pracc" or "OfficialMatch";
        bool teamScoreIsProvided = model.TeamScore.HasValue;
        bool opponentScoreIsProvided = model.OpponentScore.HasValue;

        if (!Enum.IsDefined(typeof(ActivityStatus), model.Status))
        {
            ModelState.AddModelError(nameof(model.Status), "L’état demandé n’est pas valide.");
        }

        if (selectedActivityTypeRequiresScores && string.IsNullOrWhiteSpace(model.OpponentName))
        {
            ModelState.AddModelError(nameof(model.OpponentName), "L’équipe adverse est obligatoire pour une pracc ou un match officiel.");
        }

        if (selectedActivityTypeRequiresScores && teamScoreIsProvided != opponentScoreIsProvided)
        {
            ModelState.AddModelError(nameof(model.TeamScore), "Les deux scores doivent être renseignés ensemble.");
            ModelState.AddModelError(nameof(model.OpponentScore), "Les deux scores doivent être renseignés ensemble.");
        }

        if (selectedActivityTypeRequiresScores && model.Status == ActivityStatus.Completed && !teamScoreIsProvided && !opponentScoreIsProvided)
        {
            ModelState.AddModelError(nameof(model.TeamScore), "Les deux scores sont obligatoires pour une activité terminée.");
            ModelState.AddModelError(nameof(model.OpponentScore), "Les deux scores sont obligatoires pour une activité terminée.");
        }

        if (model.PlannedStartLocal.HasValue && model.PlannedEndLocal.HasValue && model.PlannedEndLocal.Value <= model.PlannedStartLocal.Value)
        {
            ModelState.AddModelError(nameof(model.PlannedEndLocal), "La fin prévue doit être strictement postérieure au début prévu.");
        }

        if (model.Participants is null || !model.Participants.Any(participant => participant.IsSelected))
        {
            ModelState.AddModelError(nameof(model.Participants), "Sélectionnez au moins un participant.");
        }

        if (!ModelState.IsValid)
        {
            EditActivityViewModel invalidViewModel = BuildEditViewModel(details, model);

            return View(invalidViewModel);
        }

        IReadOnlyCollection<UpdateActivityParticipantRequest> participants = (model.Participants ?? [])
            .Select(participant =>
            {
                Attendance? attendance = model.Status == ActivityStatus.Completed && participant.IsSelected
                    ? participant.IsPresent
                        ? Attendance.Present
                        : Attendance.Absent
                    : null;

                return new UpdateActivityParticipantRequest(participant.TeamMembershipId, participant.IsSelected, attendance);
            })
            .ToArray();

        IReadOnlyCollection<UpdateActivityLinkRequest> links = (model.Links ?? [])
            .Select(link => new UpdateActivityLinkRequest(link.ActivityLinkId, link.Name ?? string.Empty, link.Url ?? string.Empty))
            .ToArray();

        IReadOnlyCollection<Guid> strategyIds = (model.Strategies ?? [])
            .Where(strategy => strategy.IsSelected)
            .Select(strategy => strategy.StrategyId)
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
            participants,
            links,
            model.OpponentName,
            model.TeamScore,
            model.OpponentScore,
            model.Status,
            model.CancellationReason,
            model.StatusChangeConfirmed,
            strategyIds);
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
    public async Task<IActionResult> Events(Guid teamId, DateTimeOffset? start, DateTimeOffset? end, string? types = null, string? statuses = null, string? participant = null, MatchResult? result = null, string? searchText = null, CancellationToken cancellationToken = default)
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

        if (!start.HasValue || !end.HasValue || !ModelState.IsValid)
        {
            return BadRequest();
        }

        if (searchText is not null && searchText.Trim().Length > MaximumSearchTextLength)
        {
            return BadRequest();
        }

        DateTimeOffset normalizedStart = start.Value.ToUniversalTime();
        DateTimeOffset normalizedEnd = end.Value.ToUniversalTime();

        if (normalizedEnd <= normalizedStart || normalizedEnd - normalizedStart > TimeSpan.FromDays(MaximumPeriodLengthInDays))
        {
            return BadRequest();
        }

        IReadOnlyCollection<string> selectedTypeCodes = ParseActivityTypeCodes(types);

        if (!TryParseActivityStatuses(statuses, out IReadOnlyCollection<ActivityStatus> selectedStatuses))
        {
            return BadRequest();
        }

        if (!TryParseParticipantFilter(participant, out Guid? participantUserId, out Guid? formerMemberId))
        {
            return BadRequest();
        }

        ActivityCalendarFilter filter = new(selectedTypeCodes, selectedStatuses, participantUserId, formerMemberId, result, searchText);

        IReadOnlyCollection<CalendarActivitySummary> activities = await _activityCalendarService.GetForPeriodAsync(teamId, normalizedStart, normalizedEnd, filter, cancellationToken);
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

    private static bool TryParseParticipantFilter(string? participant, out Guid? userId, out Guid? formerMemberId)
{
    userId = null;
    formerMemberId = null;

    if (string.IsNullOrWhiteSpace(participant))
    {
        return true;
    }

    string[] parts = participant.Split(':', 2, StringSplitOptions.TrimEntries);

    if (parts.Length != 2 || !Guid.TryParse(parts[1], out Guid identifier) || identifier == Guid.Empty)
    {
        return false;
    }

    if (string.Equals(parts[0], "user", StringComparison.OrdinalIgnoreCase))
    {
        userId = identifier;

        return true;
    }

    if (string.Equals(parts[0], "former", StringComparison.OrdinalIgnoreCase))
    {
        formerMemberId = identifier;

        return true;
    }

    return false;
}

    private static IReadOnlyCollection<string> ParseActivityTypeCodes(string? types)
    {
        if (types is null)
        {
            return DefaultActivityTypeCodes.ToArray();
        }

        return types
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryParseActivityStatuses(string? statuses, out IReadOnlyCollection<ActivityStatus> parsedStatuses)
    {
        if (statuses is null)
        {
            parsedStatuses = DefaultActivityStatuses.ToArray();

            return true;
        }

        string[] statusValues = statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<ActivityStatus> results = [];

        foreach (string statusValue in statusValues)
        {
            if (!Enum.TryParse(statusValue, true, out ActivityStatus status) || !Enum.IsDefined(typeof(ActivityStatus), status))
            {
                parsedStatuses = Array.Empty<ActivityStatus>();

                return false;
            }

            results.Add(status);
        }

        parsedStatuses = results
            .Distinct()
            .ToArray();

        return true;
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
        viewModel.OriginalStatus = details.Status;
        viewModel.UpdatedAtUtc = details.UpdatedAtUtc;
        viewModel.CanEdit = details.CanEdit;
        viewModel.ActivityTypes = details.ActivityTypes
            .Select(activityType => new ActivityTypeOptionViewModel(activityType.ActivityTypeId, activityType.Code, activityType.Label))
            .ToArray();
        Dictionary<Guid, ActivityEditParticipantViewModel> postedParticipants = (model?.Participants ?? [])
            .Where(participant => participant.TeamMembershipId != Guid.Empty)
            .GroupBy(participant => participant.TeamMembershipId)
            .ToDictionary(group => group.Key, group => group.First());

        Dictionary<Guid, ActivityEditStrategyViewModel> postedStrategies = (model?.Strategies ?? [])
            .Where(strategy => strategy.StrategyId != Guid.Empty)
            .GroupBy(strategy => strategy.StrategyId)
            .ToDictionary(group => group.Key, group => group.First());

        viewModel.Participants = details.Participants
            .Select(participant =>
            {
                postedParticipants.TryGetValue(participant.TeamMembershipId, out ActivityEditParticipantViewModel? postedParticipant);

                bool isSelected = postedParticipant?.IsSelected ?? participant.IsSelected;
                bool postedAttendanceIsApplicable = model is not null && viewModel.Status == ActivityStatus.Completed;
                bool isPresent = postedAttendanceIsApplicable
                    ? postedParticipant?.IsPresent ?? participant.Attendance == Attendance.Present
                    : participant.Attendance == Attendance.Present;

                return new ActivityEditParticipantViewModel(
                    participant.TeamMembershipId,
                    participant.DisplayName,
                    participant.RoleLabel,
                    participant.IsOwner,
                    isSelected,
                    participant.IsFormerMember,
                    isPresent);
            })
            .ToList();

        viewModel.Strategies = details.Strategies
            .Select(strategy =>
            {
                postedStrategies.TryGetValue(strategy.StrategyId, out ActivityEditStrategyViewModel? postedStrategy);

                return new ActivityEditStrategyViewModel
                {
                    StrategyId = strategy.StrategyId,
                    Name = strategy.Name,
                    MapName = strategy.MapName,
                    Side = strategy.Side,
                    IsActive = strategy.IsActive,
                    IsSelected = postedStrategy?.IsSelected ?? strategy.IsSelected,
                    HasImage = strategy.HasImage,
                };
            })
            .ToList();

        if (initializeEditableValues)
        {
            viewModel.Status = details.Status;
            viewModel.CancellationReason = details.CancellationReason;
            viewModel.StatusChangeConfirmed = false;
            viewModel.ActivityTypeId = details.ActivityTypeId;
            viewModel.Subtitle = details.Subtitle;
            viewModel.PlannedStartLocal = details.PlannedStartLocal;
            viewModel.PlannedEndLocal = details.PlannedEndLocal;
            viewModel.Description = details.Description;
            viewModel.Report = details.Report;
            viewModel.OpponentName = details.OpponentName;
            viewModel.TeamScore = details.TeamScore;
            viewModel.OpponentScore = details.OpponentScore;
            viewModel.Links = details.Links
                .Select(link => new ActivityEditLinkViewModel(link.ActivityLinkId, link.Name, link.Url))
                .ToList();
        }

        viewModel.Result = initializeEditableValues
            ? details.Result
            : CalculateMatchResult(viewModel.TeamScore, viewModel.OpponentScore);

        ActivityTypeOptionViewModel? selectedActivityType = viewModel.ActivityTypes.SingleOrDefault(activityType => activityType.ActivityTypeId == viewModel.ActivityTypeId);
        viewModel.TypeCode = selectedActivityType?.Code ?? details.TypeCode;
        viewModel.TypeLabel = selectedActivityType?.Label ?? details.TypeLabel;

        return viewModel;
    }

    private static MatchResult? CalculateMatchResult(int? teamScore, int? opponentScore)
    {
        if (!teamScore.HasValue || !opponentScore.HasValue)
        {
            return null;
        }

        if (teamScore.Value > opponentScore.Value)
        {
            return MatchResult.Victory;
        }

        return teamScore.Value < opponentScore.Value ? MatchResult.Defeat : MatchResult.Draw;
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

    private Guid? GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }
}