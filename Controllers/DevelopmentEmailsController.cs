using EsportTeamManager.Application.Emails;
using EsportTeamManager.Infrastructure.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EsportTeamManager.Web.Controllers;

[AllowAnonymous]
[Route("_development/emails")]
public sealed class DevelopmentEmailsController : Controller
{
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _environment;

    public DevelopmentEmailsController(IEmailService emailService, IWebHostEnvironment environment)
    {
        _emailService = emailService;
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!_environment.IsDevelopment() || _emailService is not DevelopmentEmailService developmentEmailService)
        {
            return NotFound();
        }

        IReadOnlyCollection<EmailMessage> emails = developmentEmailService.SentEmails.Reverse().ToArray();

        return View(emails);
    }
}