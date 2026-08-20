namespace EsportTeamManager.Application.Accounts;

public interface IAccountAuthenticationService
{
    Task<LoginAccountResult> LoginAsync(LoginAccountRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);
}