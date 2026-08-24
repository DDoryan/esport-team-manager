using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Infrastructure.Teams;
using EsportTeamManager.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EsportTeamManager.Tests.Integration.Teams;

public sealed class UserTeamServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_CreatesTeamWithActivePlayerOwner()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamRequest request = new(owner.Id, "  Phoenix Academy  ", "  PHX  ", "Europe/Paris");

        CreateTeamResult result = await teamService.CreateAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.TeamId);
        Assert.Empty(result.Errors);

        Team team = await context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == result.TeamId);
        TeamMembership membership = await context.TeamMemberships.AsNoTracking().SingleAsync(item => item.TeamId == result.TeamId);
        TeamRole role = await context.TeamRoles.AsNoTracking().SingleAsync(item => item.TeamRoleId == membership.TeamRoleId);

        Assert.Equal(owner.Id, team.OwnerUserId);
        Assert.Equal("Phoenix Academy", team.Name);
        Assert.Equal("PHX", team.Tag);
        Assert.Null(team.Description);
        Assert.Equal("Europe/Paris", team.TimeZoneId);
        Assert.Equal(owner.Id, membership.UserId);
        Assert.Null(membership.FormerMemberId);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Null(membership.LeftAtUtc);
        Assert.Equal("Player", role.Code);
        Assert.Equal("Joueur", role.Label);
    }

    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_CreatesSensitiveActionTrace()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamRequest request = new(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris");
        DateTimeOffset beforeCreationUtc = DateTimeOffset.UtcNow;

        CreateTeamResult result = await teamService.CreateAsync(request);

        DateTimeOffset afterCreationUtc = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded);
        Assert.NotNull(result.TeamId);

        Guid teamId = result.TeamId.Value;
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync();

        Assert.Equal(owner.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal("TEAM_CREATED", trace.ActionCode);
        Assert.Equal(nameof(Team), trace.ObjectType);
        Assert.Equal(teamId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
        Assert.InRange(trace.OccurredAtUtc, beforeCreationUtc, afterCreationUtc);
        Assert.Equal(trace.OccurredAtUtc.AddMonths(6), trace.ExpiresAtUtc);
    }

    [Theory]
    [InlineData("AB", "TAG", "Europe/Paris")]
    [InlineData("Valid team", "T", "Europe/Paris")]
    [InlineData("Valid team", "TAG", "Invalid/Zone")]
    public async Task CreateAsync_WhenRequestIsInvalid_DoesNotCreateTeamOrMembership(string name, string? tag, string timeZoneId)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamRequest request = new(owner.Id, name, tag, timeZoneId);

        CreateTeamResult result = await teamService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(await context.Teams.AsNoTracking().ToListAsync());
        Assert.Empty(await context.TeamMemberships.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetTeamsForUserAsync_WhenUserOwnsSeveralTeams_ReturnsOnlyTheirActiveTeams()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser otherUser = await CreateUserAsync(userManager, "other@example.test", "Other", "B02");

        CreateTeamResult firstResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Shared name", "TAG", "Europe/Paris"));
        CreateTeamResult secondResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Shared name", "TAG", "Europe/Paris"));
        CreateTeamResult otherResult = await teamService.CreateAsync(new CreateTeamRequest(otherUser.Id, "Other team", "OTH", "Europe/Paris"));

        IReadOnlyCollection<UserTeamSummary> teams = await teamService.GetTeamsForUserAsync(owner.Id);

        Assert.True(firstResult.Succeeded);
        Assert.True(secondResult.Succeeded);
        Assert.True(otherResult.Succeeded);
        Assert.Equal(2, teams.Count);
        Assert.All(teams, team => Assert.Equal("Shared name", team.Name));
        Assert.All(teams, team => Assert.Equal("TAG", team.Tag));
        Assert.All(teams, team => Assert.Equal("Joueur", team.RoleLabel));
        Assert.All(teams, team => Assert.True(team.IsOwner));
        Assert.DoesNotContain(teams, team => team.TeamId == otherResult.TeamId);
    }

    private static ServiceProvider CreateServiceProvider(string connectionString)
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = null!;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IUserTeamService, UserTeamService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        ApplicationUser user = new(Guid.NewGuid(), email, pseudo, tag, utcNow, utcNow);
        IdentityResult result = await userManager.CreateAsync(user);

        Assert.True(result.Succeeded, string.Join(" | ", result.Errors.Select(error => error.Description)));

        return user;
    }
}