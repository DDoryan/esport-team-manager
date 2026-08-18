using Microsoft.AspNetCore.Mvc;
using RepriseWeb.Models;

namespace RepriseWeb.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;

            return View(new ErrorViewModel(HttpContext.TraceIdentifier));
        }
    }
}