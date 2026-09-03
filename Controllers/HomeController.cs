using Microsoft.AspNetCore.Mvc;
using RepriseWeb.Models;

namespace RepriseWeb.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet("/mentions-legales")]
        public IActionResult LegalNotice()
        {
            return View();
        }

        [HttpGet("/conditions-generales-utilisation")]
        public IActionResult TermsOfUse()
        {
            return View();
        }

        [HttpGet("/politique-confidentialite")]
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