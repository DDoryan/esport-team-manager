namespace EsportTeamManager.Application.Accounts;

public interface IAccountRegistrationService
{
    Task<RegisterAccountResult> RegisterAsync(RegisterAccountRequest request, CancellationToken cancellationToken = default);
}