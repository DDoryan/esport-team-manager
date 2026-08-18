using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Tests.Integration.Persistence;

public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly string _databasePath;

    public string DatabasePath => _databasePath;

    public string ConnectionString { get; }

    public SqliteTestDatabase()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"esport-team-manager-test-{Guid.NewGuid():N}.db");

        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    public ApplicationDbContext CreateContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using ApplicationDbContext context = CreateContext();

        await context.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        DeleteFileIfExists(_databasePath);
        DeleteFileIfExists($"{_databasePath}-shm");
        DeleteFileIfExists($"{_databasePath}-wal");
        DeleteFileIfExists($"{_databasePath}-journal");

        return ValueTask.CompletedTask;
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}