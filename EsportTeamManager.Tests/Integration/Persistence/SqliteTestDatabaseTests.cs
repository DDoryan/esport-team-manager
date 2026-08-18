using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Tests.Integration.Persistence;

public sealed class SqliteTestDatabaseTests
{
    [Fact]
    public async Task InitializeAsync_WithEmptyDatabase_AppliesMigrationsAndSeedsReferenceData()
    {
        await using SqliteTestDatabase database = new();

        await database.InitializeAsync();

        await using ApplicationDbContext context = database.CreateContext();

        List<string> appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.True(await context.Database.CanConnectAsync());
        Assert.Contains(appliedMigrations, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.Contains(appliedMigrations, migration => migration.EndsWith("_SeedReferenceData", StringComparison.Ordinal));
        Assert.Equal(3, await context.TeamRoles.CountAsync());
        Assert.Equal(4, await context.ActivityTypes.CountAsync());
        Assert.Equal(13, await context.Maps.CountAsync());
    }

    [Fact]
    public async Task TwoDatabases_WhenInitialized_AreIsolated()
    {
        await using SqliteTestDatabase firstDatabase = new();
        await using SqliteTestDatabase secondDatabase = new();

        await firstDatabase.InitializeAsync();
        await secondDatabase.InitializeAsync();

        await using ApplicationDbContext firstContext = firstDatabase.CreateContext();
        await using ApplicationDbContext secondContext = secondDatabase.CreateContext();

        firstContext.Maps.Add(new Map(999, "Integration Test Map"));
        await firstContext.SaveChangesAsync();

        Assert.NotEqual(firstDatabase.DatabasePath, secondDatabase.DatabasePath);
        Assert.True(await firstContext.Maps.AnyAsync(map => map.MapId == 999));
        Assert.False(await secondContext.Maps.AnyAsync(map => map.MapId == 999));
    }

    [Fact]
    public async Task DisposeAsync_AfterInitialization_DeletesDatabaseFile()
    {
        SqliteTestDatabase database = new();
        string databasePath = database.DatabasePath;

        try
        {
            await database.InitializeAsync();

            Assert.True(File.Exists(databasePath));
        }
        finally
        {
            await database.DisposeAsync();
        }

        Assert.False(File.Exists(databasePath));
    }
}