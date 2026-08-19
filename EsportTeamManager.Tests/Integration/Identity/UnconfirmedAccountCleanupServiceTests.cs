using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Tests.Integration.Persistence;
using EsportTeamManager.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Tests.Integration.Identity;

public sealed class UnconfirmedAccountCleanupServiceTests
{
    [Fact]
    public async Task DeleteExpiredAccountsAsync_WhenAccountsHaveDifferentStates_DeletesOnlyExpiredUnconfirmedAccount()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ApplicationDbContext context = database.CreateContext();

        ApplicationUser expiredUser = new(Guid.NewGuid(), "expired@example.test", "ExpiredPlayer", "E01", currentDateUtc.AddDays(-8), currentDateUtc.AddDays(-8));
        ApplicationUser recentUser = new(Guid.NewGuid(), "recent@example.test", "RecentPlayer", "R01", currentDateUtc.AddDays(-6), currentDateUtc.AddDays(-6));
        ApplicationUser confirmedUser = new(Guid.NewGuid(), "confirmed@example.test", "ConfirmedPlayer", "C01", currentDateUtc.AddDays(-10), currentDateUtc.AddDays(-10));

        confirmedUser.EmailConfirmed = true;
        confirmedUser.MarkAsConfirmed(currentDateUtc.AddDays(-9));

        SetNormalizedIdentityValues(expiredUser);
        SetNormalizedIdentityValues(recentUser);
        SetNormalizedIdentityValues(confirmedUser);
        context.Users.AddRange(expiredUser, recentUser, confirmedUser);
        await context.SaveChangesAsync();

        UnconfirmedAccountCleanupService cleanupService = new(context, timeProvider);

        int deletedAccountCount = await cleanupService.DeleteExpiredAccountsAsync();

        Assert.Equal(1, deletedAccountCount);
        Assert.Null(await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == expiredUser.Id));
        Assert.NotNull(await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == recentUser.Id));
        Assert.NotNull(await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == confirmedUser.Id));
    }

    [Fact]
    public async Task DeleteExpiredAccountsAsync_WhenNoAccountHasExpired_ReturnsZero()
    {
        DateTimeOffset currentDateUtc = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        AdjustableTimeProvider timeProvider = new(currentDateUtc);

        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ApplicationDbContext context = database.CreateContext();

        ApplicationUser recentUser = new(Guid.NewGuid(), "recent@example.test", "RecentPlayer", "R01", currentDateUtc.AddDays(-6), currentDateUtc.AddDays(-6));

        SetNormalizedIdentityValues(recentUser);
        context.Users.Add(recentUser);
        await context.SaveChangesAsync();

        UnconfirmedAccountCleanupService cleanupService = new(context, timeProvider);

        int deletedAccountCount = await cleanupService.DeleteExpiredAccountsAsync();

        Assert.Equal(0, deletedAccountCount);
        Assert.NotNull(await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == recentUser.Id));
    }

    private static void SetNormalizedIdentityValues(ApplicationUser user)
    {
        user.NormalizedEmail = user.Email?.ToUpperInvariant();
        user.NormalizedUserName = user.UserName?.ToUpperInvariant();
    }
}