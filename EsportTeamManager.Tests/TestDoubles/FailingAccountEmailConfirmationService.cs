using EsportTeamManager.Application.Accounts;

namespace EsportTeamManager.Tests.TestDoubles;

public sealed class FailingAccountEmailConfirmationService : IAccountEmailConfirmationService
{
    public Task<AccountEmailConfirmationResult> SendConfirmationEmailAsync(Guid userId)
    {
        return Task.FromResult(AccountEmailConfirmationResult.Failure("Échec simulé de l’envoi du courriel."));
    }

    public Task<AccountEmailConfirmationResult> ConfirmEmailAsync(Guid userId, string token)
    {
        return Task.FromResult(AccountEmailConfirmationResult.Failure("Échec simulé de la confirmation."));
    }

    public Task<AccountEmailConfirmationResult> ResendConfirmationEmailAsync(string email)
    {
        return Task.FromResult(AccountEmailConfirmationResult.Failure("Échec simulé du renvoi du courriel."));
    }
}