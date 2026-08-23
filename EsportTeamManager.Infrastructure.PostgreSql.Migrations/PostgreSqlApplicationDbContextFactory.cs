using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EsportTeamManager.Infrastructure.PostgreSql.Migrations;

public sealed class PostgreSqlApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        string connectionString = args.FirstOrDefault() ?? "Host=localhost;Port=5432;Database=esport_team_manager;Username=postgres";
        string migrationsAssemblyName = typeof(PostgreSqlMigrationsAssemblyMarker).Assembly.GetName().Name ?? throw new InvalidOperationException("L’assembly des migrations PostgreSQL est introuvable.");

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, postgreSqlOptions =>
            {
                postgreSqlOptions.MigrationsAssembly(migrationsAssemblyName);
            })
            .Options;

        return new ApplicationDbContext(options);
    }
}