using System.Net;
using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Application.Emails;
using Microsoft.AspNetCore.Identity;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class AccountPasswordResetService : IAccountPasswordResetService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IPasswordResetLinkFactory _passwordResetLinkFactory;
    private readonly TimeProvider _timeProvider;

    public AccountPasswordResetService(UserManager<ApplicationUser> userManager, IEmailService emailService, IPasswordResetLinkFactory passwordResetLinkFactory, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _emailService = emailService;
        _passwordResetLinkFactory = passwordResetLinkFactory;
        _timeProvider = timeProvider;
    }

    public async Task<AccountPasswordResetResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return AccountPasswordResetResult.Success();
        }

        ApplicationUser? user = await _userManager.FindByEmailAsync(email.Trim());

        if (user is null || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
        {
            return AccountPasswordResetResult.Success();
        }

        DateTimeOffset currentDateUtc = _timeProvider.GetUtcNow();

        if (!user.CanSendPasswordResetEmail(currentDateUtc))
        {
            return AccountPasswordResetResult.Success();
        }

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        string passwordResetLink = _passwordResetLinkFactory.CreatePasswordResetLink(user.Id, token);
        string encodedPasswordResetLink = WebUtility.HtmlEncode(passwordResetLink);
        EmailMessage message = new(user.Email, "Réinitialisez votre mot de passe", $"<p>Une demande de réinitialisation du mot de passe de votre compte Esport Team Manager a été effectuée.</p><p><a href=\"{encodedPasswordResetLink}\">Réinitialiser mon mot de passe</a></p><p>Ce lien est valable pendant une heure et ne peut être utilisé qu’une seule fois.</p><p>Si vous n’êtes pas à l’origine de cette demande, vous pouvez ignorer ce courriel.</p>");

        user.RecordPasswordResetEmailSent(currentDateUtc);

        IdentityResult updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return AccountPasswordResetResult.Failure("La demande de réinitialisation n’a pas pu être enregistrée.");
        }

        await _emailService.SendAsync(message, cancellationToken);

        return AccountPasswordResetResult.Success();
    }

    public async Task<AccountPasswordResetResult> ResetPasswordAsync(Guid userId, string token, string newPassword)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
        {
            return AccountPasswordResetResult.Failure("Le lien de réinitialisation est invalide ou a expiré.");
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.EmailConfirmed)
        {
            return AccountPasswordResetResult.Failure("Le lien de réinitialisation est invalide ou a expiré.");
        }

        IdentityResult resetResult = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (resetResult.Succeeded)
        {
            return AccountPasswordResetResult.Success();
        }

        string[] passwordErrors = resetResult.Errors.Where(error => error.Code.StartsWith("Password", StringComparison.Ordinal)).Select(TranslatePasswordError).ToArray();

        if (passwordErrors.Length > 0)
        {
            return AccountPasswordResetResult.Failure(passwordErrors);
        }

        return AccountPasswordResetResult.Failure("Le lien de réinitialisation est invalide, a expiré ou a déjà été utilisé.");
    }

    private static string TranslatePasswordError(IdentityError error)
    {
        return error.Code switch
        {
            "PasswordTooShort" => "Le mot de passe doit contenir au moins 8 caractères.",
            "PasswordRequiresLower" => "Le mot de passe doit contenir une lettre minuscule.",
            "PasswordRequiresUpper" => "Le mot de passe doit contenir une lettre majuscule.",
            "PasswordRequiresDigit" => "Le mot de passe doit contenir un chiffre.",
            "PasswordRequiresNonAlphanumeric" => "Le mot de passe doit contenir un caractère non alphanumérique.",
            _ => "Le nouveau mot de passe ne respecte pas les contraintes de sécurité."
        };
    }
}