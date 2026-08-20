using EsportTeamManager.Application.Accounts;
using Microsoft.AspNetCore.Identity;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class AccountAuthenticationService : IAccountAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;

    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountAuthenticationService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<LoginAccountResult> LoginAsync(LoginAccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return LoginAccountResult.Failure();
        }

        string email = request.Email.Trim();
        ApplicationUser? user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return LoginAccountResult.Failure();
        }

        SignInResult signInResult = await _signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return LoginAccountResult.Failure();
        }

        return LoginAccountResult.Success();
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _signInManager.SignOutAsync();
    }
}