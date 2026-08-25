using System.ComponentModel.DataAnnotations;
using System.Net;
using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Application.Emails;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class AccountEmailChangeService : IAccountEmailChangeService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IEmailChangeLinkFactory _emailChangeLinkFactory;
    private readonly TimeProvider _timeProvider;

    public AccountEmailChangeService(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IEmailService emailService, IEmailChangeLinkFactory emailChangeLinkFactory, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _context = context;
        _emailService = emailService;
        _emailChangeLinkFactory = emailChangeLinkFactory;
        _timeProvider = timeProvider;
    }

    public async Task<ChangeAccountEmailResult> RequestEmailChangeAsync(ChangeAccountEmailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.UserId == Guid.Empty || string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewEmail))
        {
            return ChangeAccountEmailResult.Failure("La demande de changement d’adresse e-mail n’a pas pu être traitée.");
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId.ToString());

        if (user is null || !user.EmailConfirmed || user.AccountStatus != AccountStatus.Active)
        {
            return ChangeAccountEmailResult.Failure("Le compte est introuvable.");
        }

        bool passwordIsValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);

        if (!passwordIsValid)
        {
            return ChangeAccountEmailResult.Failure("Le mot de passe actuel est incorrect.");
        }

        string newEmail = request.NewEmail.Trim();

        if (newEmail.Length > 254 || !new EmailAddressAttribute().IsValid(newEmail))
        {
            return ChangeAccountEmailResult.Failure("La nouvelle adresse e-mail n’est pas valide.");
        }

        string? normalizedNewEmail = _userManager.NormalizeEmail(newEmail);

        if (string.IsNullOrWhiteSpace(normalizedNewEmail))
        {
            return ChangeAccountEmailResult.Failure("La nouvelle adresse e-mail n’est pas valide.");
        }

        if (string.Equals(user.NormalizedEmail, normalizedNewEmail, StringComparison.Ordinal))
        {
            return ChangeAccountEmailResult.Failure("La nouvelle adresse e-mail doit être différente de l’adresse actuelle.");
        }

        DateTimeOffset currentDateUtc = _timeProvider.GetUtcNow();

        await ClearExpiredPendingEmailsAsync(currentDateUtc, cancellationToken);

        bool emailIsUnavailable = await _context.Users.AsNoTracking().AnyAsync(candidate => candidate.Id != user.Id && (candidate.NormalizedEmail == normalizedNewEmail || candidate.NormalizedPendingEmail == normalizedNewEmail), cancellationToken);

        if (emailIsUnavailable)
        {
            return ChangeAccountEmailResult.Failure("Cette adresse e-mail est déjà utilisée ou réservée.");
        }

        user.ReservePendingEmail(newEmail, normalizedNewEmail, currentDateUtc);

        IdentityResult reservationResult;

        try
        {
            reservationResult = await _userManager.UpdateAsync(user);
        }
        catch (DbUpdateException)
        {
            return ChangeAccountEmailResult.Failure("Cette adresse e-mail est déjà utilisée ou réservée.");
        }

        if (!reservationResult.Succeeded)
        {
            return ChangeAccountEmailResult.Failure("La nouvelle adresse e-mail n’a pas pu être réservée.");
        }

        string token = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        string emailChangeLink = _emailChangeLinkFactory.CreateEmailChangeLink(user.Id, token);
        string encodedEmailChangeLink = WebUtility.HtmlEncode(emailChangeLink);
        EmailMessage message = new(newEmail, "Confirmez votre nouvelle adresse électronique", $"<p>Une demande de changement d’adresse électronique a été effectuée pour votre compte Esport Team Manager.</p><p><a href=\"{encodedEmailChangeLink}\">Confirmer ma nouvelle adresse électronique</a></p><p>Ce lien est valable pendant une heure et ne peut être utilisé qu’une seule fois.</p><p>Votre ancienne adresse reste active tant que la nouvelle adresse n’est pas confirmée.</p>");

        await _emailService.SendAsync(message, cancellationToken);

        return ChangeAccountEmailResult.Success();
    }

    public async Task<ChangeAccountEmailResult> ConfirmEmailChangeAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return ChangeAccountEmailResult.Failure("Le lien de changement d’adresse est invalide ou a expiré.");
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.EmailConfirmed || user.AccountStatus != AccountStatus.Active)
        {
            return ChangeAccountEmailResult.Failure("Le lien de changement d’adresse est invalide ou a expiré.");
        }

        DateTimeOffset currentDateUtc = _timeProvider.GetUtcNow();

        if (!user.HasValidPendingEmail(currentDateUtc))
        {
            user.ClearPendingEmail();
            await _userManager.UpdateAsync(user);

            return ChangeAccountEmailResult.Failure("Le lien de changement d’adresse est invalide ou a expiré.");
        }

        string pendingEmail = user.PendingEmail!;
        string normalizedPendingEmail = user.NormalizedPendingEmail!;

        bool emailIsUnavailable = await _context.Users.AsNoTracking().AnyAsync(candidate => candidate.Id != user.Id && (candidate.NormalizedEmail == normalizedPendingEmail || candidate.NormalizedPendingEmail == normalizedPendingEmail), cancellationToken);

        if (emailIsUnavailable)
        {
            user.ClearPendingEmail();
            await _userManager.UpdateAsync(user);

            return ChangeAccountEmailResult.Failure("Cette adresse e-mail est désormais utilisée par un autre compte.");
        }

        user.ClearPendingEmail();

        IdentityResult changeResult;

        try
        {
            changeResult = await _userManager.ChangeEmailAsync(user, pendingEmail, token);
        }
        catch (DbUpdateException)
        {
            return ChangeAccountEmailResult.Failure("Cette adresse e-mail est désormais utilisée par un autre compte.");
        }

        if (!changeResult.Succeeded)
        {
            return ChangeAccountEmailResult.Failure("Le lien de changement d’adresse est invalide, a expiré ou a déjà été utilisé.");
        }

        return ChangeAccountEmailResult.Success();
    }

    private async Task ClearExpiredPendingEmailsAsync(DateTimeOffset currentDateUtc, CancellationToken cancellationToken)
    {
        List<ApplicationUser> usersWithPendingEmail = await _context.Users.Where(user => user.PendingEmailExpiresAtUtc != null).ToListAsync(cancellationToken);
        bool changeWasMade = false;

        foreach (ApplicationUser user in usersWithPendingEmail)
        {
            if (!user.HasValidPendingEmail(currentDateUtc))
            {
                user.ClearPendingEmail();
                changeWasMade = true;
            }
        }

        if (changeWasMade)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}