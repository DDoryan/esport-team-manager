namespace EsportTeamManager.Application.Accounts;

public interface IUnconfirmedAccountCleanupService
{
    Task<int> DeleteExpiredAccountsAsync(CancellationToken cancellationToken = default);
}