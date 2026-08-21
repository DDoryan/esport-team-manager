using System.Security.Claims;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepriseWeb.ViewModels.Activities;

namespace RepriseWeb.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IUserTeamService _userTeamService;

    public ActivitiesController(ApplicationDbContext context, IUserTeamService userTeamService)
    {
        _context = context;
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

        IReadOnlyCollection<UserTeamSummary> userTeams = await _userTeamService.GetTeamsForUserAsync(currentUserId.Value, cancellationToken);
        UserTeamSummary? currentTeam = userTeams.SingleOrDefault(team => team.TeamId == teamId);

        if (currentTeam is null)
        {
            return Forbid();
        }

        List<ActivityListItemViewModel> activities = await _context.TeamActivities
            .AsNoTracking()
            .Where(activity => activity.TeamId == teamId && activity.Status != ActivityStatus.Cancelled)
            .Select(activity => new ActivityListItemViewModel
            {
                ActivityId = activity.ActivityId,
                TypeLabel = activity.ActivityType.Label,
                Subtitle = activity.Subtitle,
                PlannedStartUtc = activity.PlannedStartUtc,
                PlannedEndUtc = activity.PlannedEndUtc,
                TimeZoneId = activity.TimeZoneId,
                StatusLabel = activity.Status == ActivityStatus.Planned ? "Planifiée" : activity.Status == ActivityStatus.Completed ? "Terminée" : "Annulée"
            })
            .ToListAsync(cancellationToken);

        activities = activities.OrderBy(activity => activity.PlannedStartUtc).ToList();

        ViewData["TeamName"] = currentTeam.Name;

        return View(activities);
    }

    private Guid? GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out Guid parsedUserId) ? parsedUserId : null;
    }
}