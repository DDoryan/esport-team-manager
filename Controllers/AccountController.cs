using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EsportTeamManager.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IAccountRegistrationService _accountRegistrationService;

    public AccountController(IAccountRegistrationService accountRegistrationService)
    {
        _accountRegistrationService = accountRegistrationService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        RegisterAccountRequest request = new(model.Email, model.Pseudo, model.Tag, model.Password, model.MinimumAgeConfirmed, model.TermsAccepted);
        RegisterAccountResult result = await _accountRegistrationService.RegisterAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        return RedirectToAction(nameof(RegistrationPending));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult RegistrationPending()
    {
        return View();
    }
}