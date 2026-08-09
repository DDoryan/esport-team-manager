using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepriseWeb.Data;
using RepriseWeb.Models;

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
        var activities = await _context.TeamActivities.OrderBy(activity => activity.StartDate).ToListAsync();

        return View(activities);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var activity = await _context.TeamActivities.FindAsync(id);

        if (activity is null)
        {
            return NotFound();
        }

        return View(activity);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeamActivity activity)
    {
        if (!ModelState.IsValid)
        {
            return View(activity);
        }

        _context.TeamActivities.Add(activity);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var activity = await _context.TeamActivities.FindAsync(id);

        if (activity is null)
        {
            return NotFound();
        }

        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TeamActivity editedActivity)
    {
        if (id != editedActivity.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(editedActivity);
        }

        var existingActivity = await _context.TeamActivities.FindAsync(id);

        if (existingActivity is null)
        {
            return NotFound();
        }

        existingActivity.Title = editedActivity.Title;
        existingActivity.Type = editedActivity.Type;
        existingActivity.StartDate = editedActivity.StartDate;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _context.TeamActivities.FindAsync(id);

        if (activity is null)
        {
            return NotFound();
        }

        return View(activity);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await _context.TeamActivities.FindAsync(id);

        if (activity is null)
        {
            return NotFound();
        }

        _context.TeamActivities.Remove(activity);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}