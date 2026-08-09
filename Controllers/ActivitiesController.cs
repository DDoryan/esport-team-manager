using Microsoft.AspNetCore.Mvc;
using RepriseWeb.Models;

namespace RepriseWeb.Controllers

{
    public class ActivitiesController : Controller
    {
        private static readonly List<TeamActivity> Activities =
        [
            new TeamActivity
            {
                Id = 1,
                Title = "Entraînement collectif",
                Type = "Entraînement",
                StartDate = DateTime.Today.AddHours(18)
            },
            new TeamActivity
            {
                Id = 2,
                Title = "Review du dernier match",
                Type = "Review",
                StartDate = DateTime.Today.AddDays(1).AddHours(20)
            },
            new TeamActivity
            {
                Id = 3,
                Title = "Réunion hebdomadaire",
                Type = "Réunion",
                StartDate = DateTime.Today.AddDays(2).AddHours(19)
            }
        ];

        public IActionResult Index()
        {
            return View(Activities);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(TeamActivity activity)
        {
            if (!ModelState.IsValid)
            {
                return View(activity);
            }
            activity.Id = Activities.Count == 0
                ? 1
                : Activities.Max(existingActivity => existingActivity.Id) + 1;

            Activities.Add(activity);

            return RedirectToAction(nameof(Index));
        }
    }
}
