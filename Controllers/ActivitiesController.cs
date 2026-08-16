using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepriseWeb.ViewModels.Activities;

namespace RepriseWeb.Controllers;

public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ActivitiesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        List<ActivityListItemViewModel> activities = await _context.TeamActivities
            .AsNoTracking()
            .Where(activity => activity.Status != ActivityStatus.Cancelled)
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
            .ToListAsync();

        activities = activities.OrderBy(activity => activity.PlannedStartUtc).ToList();

        return View(activities);
    }
}