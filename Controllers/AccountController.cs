using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EsportTeamManager.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IAccountRegistrationService _accountRegistrationService;
    private readonly IAccountAuthenticationService _accountAuthenticationService;
    private readonly IAccountEmailConfirmationService _accountEmailConfirmationService;
    private readonly IAccountPasswordResetService _accountPasswordResetService;
    private readonly IAccountProfileService _accountProfileService;

    public AccountController(IAccountRegistrationService accountRegistrationService, IAccountAuthenticationService accountAuthenticationService, IAccountEmailConfirmationService accountEmailConfirmationService, IAccountPasswordResetService accountPasswordResetService, IAccountProfileService accountProfileService)
    {
        _accountRegistrationService = accountRegistrationService;
        _accountAuthenticationService = accountAuthenticationService;
        _accountEmailConfirmationService = accountEmailConfirmationService;
        _accountPasswordResetService = accountPasswordResetService;
        _accountProfileService = accountProfileService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Entry", "Teams");
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
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Entry", "Teams");
        }

        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AccountPasswordResetResult result = await _accountPasswordResetService.RequestPasswordResetAsync(model.Email, cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "La demande de réinitialisation n’a pas pu être traitée. Veuillez réessayer.");

            return View(model);
        }

        return RedirectToAction(nameof(ForgotPasswordSent));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordSent()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(Guid userId, string token)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return View("ResetPasswordInvalid");
        }

        return View(new ResetPasswordViewModel
        {
            UserId = userId,
            Token = token
        });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AccountPasswordResetResult result = await _accountPasswordResetService.ResetPasswordAsync(model.UserId, model.Token, model.Password);

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out Guid userId))
        {
            return Challenge();
        }

        AccountProfile? profile = await _accountProfileService.GetProfileAsync(userId, cancellationToken);

        if (profile is null)
        {
            return Challenge();
        }

        return View(new ProfileViewModel(profile.Pseudo, profile.Tag, profile.Email));
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetCurrentUserId(out Guid userId))
        {
            return Challenge();
        }

        ChangeAccountPasswordRequest request = new(userId, model.CurrentPassword, model.NewPassword);
        ChangeAccountPasswordResult result = await _accountProfileService.ChangePasswordAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        TempData["ProfileSuccessMessage"] = "Votre mot de passe a été modifié. Les autres sessions ont été déconnectées.";

        return RedirectToAction(nameof(Profile));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Entry", "Teams");
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

        return RedirectToAction("Entry", "Teams");
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _accountAuthenticationService.LogoutAsync(cancellationToken);

        return RedirectToAction(nameof(Login));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        string? userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out userId);
    }
}