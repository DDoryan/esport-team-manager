using EsportTeamManager.Application.Accounts;
using Microsoft.AspNetCore.Identity;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class AccountProfileService : IAccountProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountProfileService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
        {
            return null;
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        return new AccountProfile(user.Pseudo, user.Tag, user.Email);
    }

    public async Task<ChangeAccountPasswordResult> ChangePasswordAsync(ChangeAccountPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.UserId == Guid.Empty || string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
        {
            return ChangeAccountPasswordResult.Failure("Le mot de passe n’a pas pu être modifié.");
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId.ToString());

        if (user is null)
        {
            return ChangeAccountPasswordResult.Failure("Le compte est introuvable.");
        }

        IdentityResult changeResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!changeResult.Succeeded)
        {
            string[] errors = changeResult.Errors.Select(TranslatePasswordError).Distinct().ToArray();

            return ChangeAccountPasswordResult.Failure(errors);
        }

        await _signInManager.RefreshSignInAsync(user);

        return ChangeAccountPasswordResult.Success();
    }

    private static string TranslatePasswordError(IdentityError error)
    {
        return error.Code switch
        {
            "PasswordMismatch" => "Le mot de passe actuel est incorrect.",
            "PasswordTooShort" => "Le nouveau mot de passe doit contenir au moins 8 caractères.",
            "PasswordRequiresLower" => "Le nouveau mot de passe doit contenir une lettre minuscule.",
            "PasswordRequiresUpper" => "Le nouveau mot de passe doit contenir une lettre majuscule.",
            "PasswordRequiresDigit" => "Le nouveau mot de passe doit contenir un chiffre.",
            "PasswordRequiresNonAlphanumeric" => "Le nouveau mot de passe doit contenir un caractère non alphanumérique.",
            _ => "Le mot de passe n’a pas pu être modifié."
        };
    }
}