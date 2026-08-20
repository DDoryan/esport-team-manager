using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EsportTeamManager.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IAccountRegistrationService _accountRegistrationService;
    private readonly IAccountAuthenticationService _accountAuthenticationService;
    private readonly IAccountEmailConfirmationService _accountEmailConfirmationService;

    public AccountController(IAccountRegistrationService accountRegistrationService, IAccountAuthenticationService accountAuthenticationService, IAccountEmailConfirmationService accountEmailConfirmationService)
    {
        _accountRegistrationService = accountRegistrationService;
        _accountAuthenticationService = accountAuthenticationService;
        _accountEmailConfirmationService = accountEmailConfirmationService;
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

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string token)
    {
        AccountEmailConfirmationResult result = await _accountEmailConfirmationService.ConfirmEmailAsync(userId, token);
        string message = result.Succeeded ? "Votre adresse électronique a été confirmée. Vous pouvez maintenant vous connecter." : result.Errors.FirstOrDefault() ?? "Le lien de confirmation est invalide.";

        return View(new ConfirmEmailViewModel(result.Succeeded, message));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendConfirmation()
    {
        return View(new ResendConfirmationViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AccountEmailConfirmationResult result = await _accountEmailConfirmationService.ResendConfirmationEmailAsync(model.Email);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Le courriel de confirmation n’a pas pu être envoyé. Veuillez réessayer.");

            return View(model);
        }

        return RedirectToAction(nameof(ResendConfirmationSent));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendConfirmationSent()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Activities");
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl
        });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        LoginAccountRequest request = new(model.Email, model.Password, model.RememberMe);
        LoginAccountResult result = await _accountAuthenticationService.LoginAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage);
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Activities");
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _accountAuthenticationService.LogoutAsync(cancellationToken);

        return RedirectToAction(nameof(Login));
    }
}