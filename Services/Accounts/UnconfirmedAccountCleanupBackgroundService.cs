using EsportTeamManager.Application.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EsportTeamManager.Web.Services.Accounts;

public sealed class UnconfirmedAccountCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<UnconfirmedAccountCleanupBackgroundService> _logger;

    public UnconfirmedAccountCleanupBackgroundService(IServiceScopeFactory serviceScopeFactory, ILogger<UnconfirmedAccountCleanupBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCleanupAsync(stoppingToken);
                await Task.Delay(CleanupInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
            IUnconfirmedAccountCleanupService cleanupService = scope.ServiceProvider.GetRequiredService<IUnconfirmedAccountCleanupService>();
            int deletedAccountCount = await cleanupService.DeleteExpiredAccountsAsync(cancellationToken);

            if (deletedAccountCount > 0)
            {
                _logger.LogInformation("{DeletedAccountCount} compte(s) non confirmé(s) arrivé(s) à expiration ont été supprimé(s).", deletedAccountCount);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Le nettoyage des comptes non confirmés a échoué.");
        }
    }
}