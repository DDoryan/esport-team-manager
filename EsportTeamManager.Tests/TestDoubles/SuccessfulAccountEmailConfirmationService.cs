using EsportTeamManager.Application.Accounts;

namespace EsportTeamManager.Tests.TestDoubles;

public sealed class SuccessfulAccountEmailConfirmationService : IAccountEmailConfirmationService
{
    public Task<AccountEmailConfirmationResult> SendConfirmationEmailAsync(Guid userId)
    {
        return Task.FromResult(AccountEmailConfirmationResult.Success());
    }

    public Task<AccountEmailConfirmationResult> ConfirmEmailAsync(Guid userId, string token)
    {
        return Task.FromResult(AccountEmailConfirmationResult.Success());
    }

    public Task<AccountEmailConfirmationResult> ResendConfirmationEmailAsync(string email)
    {
        return Task.FromResult(AccountEmailConfirmationResult.Success());
    }
}