using System.Net;
using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Application.Emails;
using Microsoft.AspNetCore.Identity;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class AccountEmailConfirmationService : IAccountEmailConfirmationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IEmailConfirmationLinkFactory _emailConfirmationLinkFactory;
    private readonly TimeProvider _timeProvider;

    public AccountEmailConfirmationService(UserManager<ApplicationUser> userManager, IEmailService emailService, IEmailConfirmationLinkFactory emailConfirmationLinkFactory, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _emailService = emailService;
        _emailConfirmationLinkFactory = emailConfirmationLinkFactory;
        _timeProvider = timeProvider;
    }

    public async Task<AccountEmailConfirmationResult> SendConfirmationEmailAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return AccountEmailConfirmationResult.Failure("Le compte est introuvable.");
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return AccountEmailConfirmationResult.Failure("Le compte est introuvable.");
        }

        return await SendConfirmationEmailAsync(user, false);
    }

    public async Task<AccountEmailConfirmationResult> ConfirmEmailAsync(Guid userId, string token)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return AccountEmailConfirmationResult.Failure("Le lien de confirmation est invalide.");
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return AccountEmailConfirmationResult.Failure("Le lien de confirmation est invalide.");
        }

        if (user.EmailConfirmed)
        {
            return AccountEmailConfirmationResult.Failure("Ce lien de confirmation a déjà été utilisé.");
        }

        IdentityResult confirmationResult = await _userManager.ConfirmEmailAsync(user, token);

        if (!confirmationResult.Succeeded)
        {
            return AccountEmailConfirmationResult.Failure("Le lien de confirmation est invalide ou a expiré.");
        }

        user.MarkAsConfirmed(_timeProvider.GetUtcNow());

        IdentityResult updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return AccountEmailConfirmationResult.Failure(updateResult.Errors.Select(error => error.Description).ToArray());
        }

        return AccountEmailConfirmationResult.Success();
    }

    public async Task<AccountEmailConfirmationResult> ResendConfirmationEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return AccountEmailConfirmationResult.Success();
        }

        ApplicationUser? user = await _userManager.FindByEmailAsync(email.Trim());

        if (user is null || user.EmailConfirmed)
        {
            return AccountEmailConfirmationResult.Success();
        }

        return await SendConfirmationEmailAsync(user, true);
    }

    private async Task<AccountEmailConfirmationResult> SendConfirmationEmailAsync(ApplicationUser user, bool concealExpectedRejection)
    {
        if (user.EmailConfirmed)
        {
            return concealExpectedRejection ? AccountEmailConfirmationResult.Success() : AccountEmailConfirmationResult.Failure("L’adresse électronique est déjà confirmée.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return AccountEmailConfirmationResult.Failure("Aucune adresse électronique n’est associée au compte.");
        }

        DateTimeOffset currentDateUtc = _timeProvider.GetUtcNow();

        if (!user.CanSendConfirmationEmail(currentDateUtc))
        {
            return concealExpectedRejection ? AccountEmailConfirmationResult.Success() : AccountEmailConfirmationResult.Failure("Un courriel de confirmation a déjà été envoyé récemment.");
        }

        string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        string confirmationLink = _emailConfirmationLinkFactory.CreateEmailConfirmationLink(user.Id, token);
        string encodedConfirmationLink = WebUtility.HtmlEncode(confirmationLink);
        EmailMessage message = new(user.Email, "Confirmez votre adresse électronique", $"<p>Confirmez votre adresse électronique pour activer votre compte Esport Team Manager.</p><p><a href=\"{encodedConfirmationLink}\">Confirmer mon adresse électronique</a></p><p>Ce lien est valable pendant 24 heures.</p>");

        user.RecordConfirmationEmailSent(currentDateUtc);

        IdentityResult updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return AccountEmailConfirmationResult.Failure(updateResult.Errors.Select(error => error.Description).ToArray());
        }

        await _emailService.SendAsync(message);

        return AccountEmailConfirmationResult.Success();
    }
}