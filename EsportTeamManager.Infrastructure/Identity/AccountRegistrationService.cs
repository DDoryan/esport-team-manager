using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class AccountRegistrationService : IAccountRegistrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountEmailConfirmationService _accountEmailConfirmationService;
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AccountRegistrationService(UserManager<ApplicationUser> userManager, IAccountEmailConfirmationService accountEmailConfirmationService, ApplicationDbContext context, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _accountEmailConfirmationService = accountEmailConfirmationService;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<RegisterAccountResult> RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        List<string> validationErrors = [];

        if (!request.MinimumAgeConfirmed)
        {
            validationErrors.Add("Vous devez attester avoir au moins 15 ans.");
        }

        if (!request.TermsAccepted)
        {
            validationErrors.Add("Vous devez accepter les conditions générales d’utilisation.");
        }

        if (validationErrors.Count > 0)
        {
            return RegisterAccountResult.Failure(validationErrors);
        }

        LegalDocumentVersion? currentTermsVersion = await GetCurrentTermsVersionAsync(cancellationToken);

        if (currentTermsVersion is null)
        {
            return RegisterAccountResult.Failure(["La version actuelle des conditions générales d’utilisation est indisponible."]);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        ApplicationUser user;

        try
        {
            user = new ApplicationUser(Guid.NewGuid(), request.Email, request.Pseudo, request.Tag, utcNow, utcNow);
        }
        catch (DomainException)
        {
            return RegisterAccountResult.Failure(["Les informations fournies ne permettent pas de créer le compte."]);
        }

        IdentityResult identityResult = await _userManager.CreateAsync(user, request.Password);

        if (!identityResult.Succeeded)
        {
            IEnumerable<string> errors = identityResult.Errors.Select(TranslateIdentityError).Distinct();

            return RegisterAccountResult.Failure(errors);
        }

        LegalAcceptance legalAcceptance = new(user.Id, currentTermsVersion.LegalDocumentVersionId, utcNow);

        _context.LegalAcceptances.Add(legalAcceptance);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _context.Entry(legalAcceptance).State = EntityState.Detached;
            await _userManager.DeleteAsync(user);

            return RegisterAccountResult.Failure(["L’acceptation des conditions générales d’utilisation n’a pas pu être enregistrée."]);
        }

        AccountEmailConfirmationResult emailConfirmationResult = await _accountEmailConfirmationService.SendConfirmationEmailAsync(user.Id);

        if (!emailConfirmationResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            return RegisterAccountResult.Failure(["Le courriel de confirmation n’a pas pu être envoyé. Veuillez réessayer."]);
        }

        return RegisterAccountResult.Success();
    }

    private async Task<LegalDocumentVersion?> GetCurrentTermsVersionAsync(CancellationToken cancellationToken)
    {
        List<LegalDocumentVersion> termsVersions = await _context.LegalDocumentVersions
            .Where(version => version.DocumentType == LegalDocumentType.TermsOfService && version.RequiresAcceptance)
            .ToListAsync(cancellationToken);

        return termsVersions
            .OrderByDescending(version => version.PublishedAtUtc)
            .ThenByDescending(version => version.LegalDocumentVersionId)
            .FirstOrDefault();
    }

    private static string TranslateIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateEmail" => "Cette adresse e-mail est déjà utilisée.",
            "DuplicateUserName" => "Cette combinaison de pseudonyme et de tag est déjà utilisée.",
            "InvalidEmail" => "L’adresse e-mail n’est pas valide.",
            "InvalidUserName" => "Le pseudonyme ou le tag n’est pas valide.",
            "PasswordTooShort" => "Le mot de passe doit contenir au moins 8 caractères.",
            "PasswordRequiresLower" => "Le mot de passe doit contenir une lettre minuscule.",
            "PasswordRequiresUpper" => "Le mot de passe doit contenir une lettre majuscule.",
            "PasswordRequiresDigit" => "Le mot de passe doit contenir un chiffre.",
            "PasswordRequiresNonAlphanumeric" => "Le mot de passe doit contenir un caractère non alphanumérique.",
            _ => "Le compte n’a pas pu être créé avec les informations fournies."
        };
    }
}