using EsportTeamManager.Application.Accounts;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class UnconfirmedAccountCleanupService : IUnconfirmedAccountCleanupService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public UnconfirmedAccountCleanupService(ApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<int> DeleteExpiredAccountsAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset expirationDateUtc = _timeProvider.GetUtcNow().AddDays(-7);

        List<ApplicationUser> unconfirmedUsers = await _context.Users
            .Where(user => !user.EmailConfirmed && user.AccountStatus == AccountStatus.PendingConfirmation)
            .ToListAsync(cancellationToken);

        List<ApplicationUser> expiredUsers = unconfirmedUsers
            .Where(user => user.CreatedAtUtc <= expirationDateUtc)
            .ToList();

        if (expiredUsers.Count == 0)
        {
            return 0;
        }

        _context.Users.RemoveRange(expiredUsers);

        await _context.SaveChangesAsync(cancellationToken);

        return expiredUsers.Count;
    }
}