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
    private readonly IUserTeamService _userTeamService;

    public ActivitiesController(IActivityCalendarService activityCalendarService, IUserTeamService userTeamService)
    {
        _activityCalendarService = activityCalendarService;
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

        TeamCalendarViewModel viewModel = new(currentTeam.TeamId, currentTeam.Name, currentTeam.TimeZoneId);

        return View(viewModel);
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
                activity.PlannedStartUtc,
                activity.PlannedEndUtc,
                activity.Subtitle,
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

    private Guid? GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }
}