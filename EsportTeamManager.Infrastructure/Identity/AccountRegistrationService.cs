using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class AccountRegistrationService : IAccountRegistrationService
{
    private readonly UserManager<ApplicationUser> _userManager;

    private readonly TimeProvider _timeProvider;

    public AccountRegistrationService(UserManager<ApplicationUser> userManager, TimeProvider timeProvider)
    {
        _userManager = userManager;
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

        if (identityResult.Succeeded)
        {
            return RegisterAccountResult.Success();
        }

        IEnumerable<string> errors = identityResult.Errors.Select(TranslateIdentityError).Distinct();

        return RegisterAccountResult.Failure(errors);
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