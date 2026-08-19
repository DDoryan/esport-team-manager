namespace EsportTeamManager.Application.Accounts;

public interface IAccountEmailConfirmationService
{
    Task<AccountEmailConfirmationResult> SendConfirmationEmailAsync(Guid userId);

    Task<AccountEmailConfirmationResult> ConfirmEmailAsync(Guid userId, string token);

    Task<AccountEmailConfirmationResult> ResendConfirmationEmailAsync(string email);
}