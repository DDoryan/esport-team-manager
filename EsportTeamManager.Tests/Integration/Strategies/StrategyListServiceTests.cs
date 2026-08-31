using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Infrastructure.Strategies;
using EsportTeamManager.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EsportTeamManager.Tests.Integration.Strategies;

public sealed class StrategyListServiceTests
{
    [Fact]
    public async Task GetAsync_WithUnfilteredRequest_ReturnsOnlyRequestedTeamByLatestUpdate()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IStrategyListService service = scope.ServiceProvider.GetRequiredService<IStrategyListService>();
        ApplicationUser firstOwner = await CreateUserAsync(userManager, "first-owner@example.test", "FirstOwner", "A01");
        ApplicationUser secondOwner = await CreateUserAsync(userManager, "second-owner@example.test", "SecondOwner", "B02");
        TeamSetup firstTeam = await CreateTeamAsync(context, firstOwner, "Phoenix Academy", "PHX");
        TeamSetup secondTeam = await CreateTeamAsync(context, secondOwner, "Nova Academy", "NVA");

        await AddStrategyAsync(context, firstTeam, "Ascent", "Ascent attaque", StrategySide.Attack, true, new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, firstTeam, "Lotus", "Lotus défense", StrategySide.Defense, true, new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, firstTeam, "Haven", "Haven contact", StrategySide.Attack, false, new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, secondTeam, "Bind", "Stratégie étrangère", StrategySide.Defense, true, new DateTimeOffset(2026, 8, 31, 11, 0, 0, TimeSpan.Zero));

        context.ChangeTracker.Clear();

        IReadOnlyCollection<StrategySummary> strategies = await service.GetAsync(firstTeam.TeamId, new StrategyListFilter(null, null, null, null));

        Assert.Equal(["Lotus défense", "Haven contact", "Ascent attaque"], strategies.Select(strategy => strategy.Name));
        Assert.DoesNotContain(strategies, strategy => strategy.Name == "Stratégie étrangère");
        Assert.Empty(context.ChangeTracker.Entries<Strategy>());
    }

    [Fact]
    public async Task GetAsync_WithMapSelected_ReturnsOnlySelectedMap()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IStrategyListService service = scope.ServiceProvider.GetRequiredService<IStrategyListService>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner, "Phoenix Academy", "PHX");

        await AddStrategyAsync(context, team, "Ascent", "Ascent attaque", StrategySide.Attack, true, new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, team, "Lotus", "Lotus défense", StrategySide.Defense, true, new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

        int lotusMapId = await context.Maps
            .Where(map => map.Name == "Lotus")
            .Select(map => map.MapId)
            .SingleAsync();

        IReadOnlyCollection<StrategySummary> strategies = await service.GetAsync(team.TeamId, new StrategyListFilter(lotusMapId, null, null, null));

        StrategySummary strategy = Assert.Single(strategies);

        Assert.Equal("Lotus", strategy.MapName);
        Assert.Equal("Lotus défense", strategy.Name);
    }

    [Theory]
    [InlineData(true, "Stratégie active")]
    [InlineData(false, "Stratégie inactive")]
    public async Task GetAsync_WithActivityState_ReturnsOnlySelectedState(bool isActive, string expectedName)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IStrategyListService service = scope.ServiceProvider.GetRequiredService<IStrategyListService>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner, "Phoenix Academy", "PHX");

        await AddStrategyAsync(context, team, "Ascent", "Stratégie active", StrategySide.Attack, true, new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, team, "Lotus", "Stratégie inactive", StrategySide.Defense, false, new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

        IReadOnlyCollection<StrategySummary> strategies = await service.GetAsync(team.TeamId, new StrategyListFilter(null, null, isActive, null));

        StrategySummary strategy = Assert.Single(strategies);

        Assert.Equal(expectedName, strategy.Name);
        Assert.Equal(isActive, strategy.IsActive);
    }

    [Theory]
    [InlineData(StrategySide.Attack, "Stratégie attaque")]
    [InlineData(StrategySide.Defense, "Stratégie défense")]
    public async Task GetAsync_WithSideSelected_ReturnsOnlySelectedSide(StrategySide side, string expectedName)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IStrategyListService service = scope.ServiceProvider.GetRequiredService<IStrategyListService>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner, "Phoenix Academy", "PHX");

        await AddStrategyAsync(context, team, "Ascent", "Stratégie attaque", StrategySide.Attack, true, new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, team, "Lotus", "Stratégie défense", StrategySide.Defense, true, new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

        IReadOnlyCollection<StrategySummary> strategies = await service.GetAsync(team.TeamId, new StrategyListFilter(null, side, null, null));

        StrategySummary strategy = Assert.Single(strategies);

        Assert.Equal(expectedName, strategy.Name);
        Assert.Equal(side, strategy.Side);
    }

    [Fact]
    public async Task GetAsync_WhenFiltersAreCombined_ReturnsOnlyMatchingStrategy()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IStrategyListService service = scope.ServiceProvider.GetRequiredService<IStrategyListService>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner, "Phoenix Academy", "PHX");

        await AddStrategyAsync(context, team, "Ascent", "  Retake Site A  ", StrategySide.Attack, true, new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, team, "Ascent", "Défense Site A", StrategySide.Defense, true, new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, team, "Lotus", "Retake Site C", StrategySide.Attack, true, new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero));
        await AddStrategyAsync(context, team, "Ascent", "Retake inactif", StrategySide.Attack, false, new DateTimeOffset(2026, 8, 31, 7, 0, 0, TimeSpan.Zero));

        int ascentMapId = await context.Maps
            .Where(map => map.Name == "Ascent")
            .Select(map => map.MapId)
            .SingleAsync();

        StrategyListFilter filter = new(ascentMapId, StrategySide.Attack, true, "  site a  ");

        IReadOnlyCollection<StrategySummary> strategies = await service.GetAsync(team.TeamId, filter);

        StrategySummary strategy = Assert.Single(strategies);

        Assert.Equal("Retake Site A", strategy.Name);
        Assert.Equal("Ascent", strategy.MapName);
        Assert.Equal(StrategySide.Attack, strategy.Side);
        Assert.True(strategy.IsActive);
    }

    [Fact]
    public async Task GetAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        await using SqliteTestDatabase database = new();
        await using ApplicationDbContext context = database.CreateContext();
        StrategyListService service = new(context);
        using CancellationTokenSource cancellationTokenSource = new();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetAsync(Guid.NewGuid(), new StrategyListFilter(null, null, null, null), cancellationTokenSource.Token));
    }

    [Fact]
    public async Task GetAsync_WithEmptyTeamIdentifier_ThrowsArgumentException()
    {
        await using SqliteTestDatabase database = new();
        await using ApplicationDbContext context = database.CreateContext();
        StrategyListService service = new(context);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(Guid.Empty, new StrategyListFilter(null, null, null, null)));

        Assert.Equal("teamId", exception.ParamName);
    }

    [Fact]
    public void Contract_ExposesOnlyReadOperation()
    {
        var methods = typeof(IStrategyListService).GetMethods();

        var method = Assert.Single(methods);

        Assert.Equal(nameof(IStrategyListService.GetAsync), method.Name);
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

        services.AddScoped<IStrategyListService, StrategyListService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag)
    {
        DateTimeOffset utcNow = new(2026, 8, 31, 8, 0, 0, TimeSpan.Zero);
        ApplicationUser user = new(Guid.NewGuid(), email, pseudo, tag, utcNow, utcNow);
        IdentityResult result = await userManager.CreateAsync(user);

        Assert.True(result.Succeeded, string.Join(" | ", result.Errors.Select(error => error.Description)));

        return user;
    }

    private static async Task<TeamSetup> CreateTeamAsync(ApplicationDbContext context, ApplicationUser owner, string teamName, string teamTag)
    {
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();

        Guid teamId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = new(2026, 8, 31, 8, 0, 0, TimeSpan.Zero);
        Team team = new(teamId, owner.Id, teamName, createdAtUtc, teamTag);
        TeamMembership membership = new(membershipId, teamId, owner.Id, playerRoleId, createdAtUtc);

        context.Teams.Add(team);
        context.TeamMemberships.Add(membership);
        await context.SaveChangesAsync();

        return new TeamSetup(teamId, membershipId);
    }

    private static async Task<Strategy> AddStrategyAsync(ApplicationDbContext context, TeamSetup team, string mapName, string name, StrategySide side, bool isActive, DateTimeOffset updatedAtUtc)
    {
        int mapId = await context.Maps
            .Where(map => map.Name == mapName)
            .Select(map => map.MapId)
            .SingleAsync();

        DateTimeOffset createdAtUtc = updatedAtUtc.AddHours(-1);
        Strategy strategy = new(team.TeamId, team.OwnerMembershipId, mapId, name, side, "Description", null, createdAtUtc);

        if (!isActive)
        {
            strategy.SetActive(false, updatedAtUtc);
        }
        else
        {
            strategy.Rename(strategy.Name, updatedAtUtc);
        }

        context.Strategies.Add(strategy);
        await context.SaveChangesAsync();

        return strategy;
    }

    private sealed record TeamSetup(Guid TeamId, Guid OwnerMembershipId);
}