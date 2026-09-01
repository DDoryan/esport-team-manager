using EsportTeamManager.Application.Images;
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

public sealed class StrategyEditingServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenOwnerProvidesDescription_CreatesStrategy()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        CreateStrategyRequest request = new(owner.Id, team.TeamId, 4, "  Exécution site A  ", StrategySide.Attack, "  Prise rapide du site A.  ", null, true, null);

        SaveStrategyResult result = await strategyEditingService.CreateAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.StrategyId);

        Strategy strategy = await context.Strategies.AsNoTracking().SingleAsync();

        Assert.Equal(result.StrategyId, strategy.StrategyId);
        Assert.Equal(team.TeamId, strategy.TeamId);
        Assert.Equal(team.OwnerMembershipId, strategy.CreatedByMembershipId);
        Assert.Equal(4, strategy.MapId);
        Assert.Equal("Exécution site A", strategy.Name);
        Assert.Equal(StrategySide.Attack, strategy.Side);
        Assert.Equal("Prise rapide du site A.", strategy.Description);
        Assert.Null(strategy.ExternalUrl);
        Assert.True(strategy.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WhenManagerProvidesExternalUrl_CreatesStrategy()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser manager = await CreateUserAsync(userManager, "manager@example.test", "Manager", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership managerMembership = await AddMembershipAsync(context, team.TeamId, manager, "Manager");
        CreateStrategyRequest request = new(manager.Id, team.TeamId, 1, "Défense Hookah", StrategySide.Defense, null, "https://example.test/strategy", false, null);

        SaveStrategyResult result = await strategyEditingService.CreateAsync(request);

        Assert.True(result.Succeeded);

        Strategy strategy = await context.Strategies.AsNoTracking().SingleAsync();

        Assert.Equal(managerMembership.TeamMembershipId, strategy.CreatedByMembershipId);
        Assert.Equal("https://example.test/strategy", strategy.ExternalUrl);
        Assert.False(strategy.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WhenPlayerIsNotOwner_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        await AddMembershipAsync(context, team.TeamId, player, "Player");
        CreateStrategyRequest request = new(player.Id, team.TeamId, 4, "Exécution site A", StrategySide.Attack, "Prise rapide.", null, true, null);

        SaveStrategyResult result = await strategyEditingService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("pas autorisé", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await context.Strategies.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenNoDescriptionUrlOrImageIsProvided_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        CreateStrategyRequest request = new(owner.Id, team.TeamId, 4, "Exécution site A", StrategySide.Attack, null, null, true, null);

        SaveStrategyResult result = await strategyEditingService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Empty(await context.Strategies.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenImageStorageFails_RollsBackStrategy()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new()
        {
            StoreStrategyImageResult = StorePrivateImageResult.Failure(["L’image de test est invalide."])
        };
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        using MemoryStream imageContent = new([1, 2, 3]);
        StrategyImageUpload image = new("strategy.png", imageContent);
        CreateStrategyRequest request = new(owner.Id, team.TeamId, 4, "Exécution site A", StrategySide.Attack, null, null, true, image);

        SaveStrategyResult result = await strategyEditingService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("L’image de test est invalide.", result.Errors);
        Assert.Empty(await context.Strategies.AsNoTracking().ToListAsync());
        Assert.Empty(await context.ActionTraces.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_WhenOwnerProvidesValidData_UpdatesStrategy()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Strategy strategy = await CreateStrategyAsync(context, team.TeamId, team.OwnerMembershipId);
        UpdateStrategyRequest request = new(owner.Id, team.TeamId, strategy.StrategyId, 1, "Défense Hookah", StrategySide.Defense, null, "https://example.test/updated-strategy", false, null);

        SaveStrategyResult result = await strategyEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(strategy.StrategyId, result.StrategyId);

        context.ChangeTracker.Clear();

        Strategy updatedStrategy = await context.Strategies.AsNoTracking().SingleAsync();

        Assert.Equal(1, updatedStrategy.MapId);
        Assert.Equal("Défense Hookah", updatedStrategy.Name);
        Assert.Equal(StrategySide.Defense, updatedStrategy.Side);
        Assert.Null(updatedStrategy.Description);
        Assert.Equal("https://example.test/updated-strategy", updatedStrategy.ExternalUrl);
        Assert.False(updatedStrategy.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_WhenPlayerIsNotOwner_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        await AddMembershipAsync(context, team.TeamId, player, "Player");
        Strategy strategy = await CreateStrategyAsync(context, team.TeamId, team.OwnerMembershipId);
        UpdateStrategyRequest request = new(player.Id, team.TeamId, strategy.StrategyId, 1, "Modification refusée", StrategySide.Defense, "Contenu modifié.", null, false, null);

        SaveStrategyResult result = await strategyEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("pas autorisé", StringComparison.OrdinalIgnoreCase));

        context.ChangeTracker.Clear();

        Strategy unchangedStrategy = await context.Strategies.AsNoTracking().SingleAsync();

        Assert.Equal("Exécution site A", unchangedStrategy.Name);
        Assert.Equal(4, unchangedStrategy.MapId);
        Assert.True(unchangedStrategy.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_WhenAllContentIsRemovedWithoutStoredImage_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Strategy strategy = await CreateStrategyAsync(context, team.TeamId, team.OwnerMembershipId);
        UpdateStrategyRequest request = new(owner.Id, team.TeamId, strategy.StrategyId, 4, "Exécution site A", StrategySide.Attack, null, null, true, null);

        SaveStrategyResult result = await strategyEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);

        context.ChangeTracker.Clear();

        Strategy unchangedStrategy = await context.Strategies.AsNoTracking().SingleAsync();

        Assert.Equal("Prise rapide du site A.", unchangedStrategy.Description);
    }

    [Fact]
    public async Task UpdateAsync_WhenStoredImageIsOnlyContent_AllowsUpdate()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Strategy strategy = await CreateStrategyAsync(context, team.TeamId, team.OwnerMembershipId);
        DateTimeOffset createdAtUtc = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        ImageFile image = ImageFile.CreateStrategyImage(
            strategy.StrategyId,
            "stored.webp",
            "strategy.png",
            "image/webp",
            100,
            128,
            128,
            $"strategy-images/{strategy.StrategyId:N}/stored.webp",
            $"strategy-images/{strategy.StrategyId:N}/stored-thumbnail.webp",
            createdAtUtc);

        context.ImageFiles.Add(image);
        await context.SaveChangesAsync();

        UpdateStrategyRequest request = new(owner.Id, team.TeamId, strategy.StrategyId, 4, "Exécution avec image", StrategySide.Attack, null, null, true, null);

        SaveStrategyResult result = await strategyEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);

        context.ChangeTracker.Clear();

        Strategy updatedStrategy = await context.Strategies.AsNoTracking().SingleAsync();

        Assert.Equal("Exécution avec image", updatedStrategy.Name);
        Assert.Null(updatedStrategy.Description);
        Assert.Null(updatedStrategy.ExternalUrl);
    }

    [Fact]
    public async Task GetAsync_WhenPlayerIsActive_ReturnsReadOnlyDetails()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        StubPrivateImageService imageService = new();
        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString, imageService);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IStrategyEditingService strategyEditingService = scope.ServiceProvider.GetRequiredService<IStrategyEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        await AddMembershipAsync(context, team.TeamId, player, "Player");
        Strategy strategy = await CreateStrategyAsync(context, team.TeamId, team.OwnerMembershipId);

        StrategyEditingDetails? details = await strategyEditingService.GetAsync(player.Id, team.TeamId, strategy.StrategyId);

        Assert.NotNull(details);
        Assert.Equal(strategy.StrategyId, details.StrategyId);
        Assert.Equal(team.TeamId, details.TeamId);
        Assert.False(details.CanManage);
    }

    private static ServiceProvider CreateServiceProvider(string connectionString, StubPrivateImageService imageService)
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

        services.AddSingleton<IPrivateImageService>(imageService);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 8, 31, 14, 0, 0, TimeSpan.Zero)));
        services.AddScoped<IStrategyEditingService, StrategyEditingService>();

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

    private static async Task<TeamSetup> CreateTeamAsync(ApplicationDbContext context, ApplicationUser owner)
    {
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();

        Guid teamId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = new(2026, 8, 31, 8, 0, 0, TimeSpan.Zero);
        Team team = new(teamId, owner.Id, "Phoenix Academy", createdAtUtc, "PHX", null, "Europe/Paris");
        TeamMembership membership = new(membershipId, teamId, owner.Id, playerRoleId, createdAtUtc);

        context.Teams.Add(team);
        context.TeamMemberships.Add(membership);
        await context.SaveChangesAsync();

        return new TeamSetup(teamId, membershipId);
    }

    private static async Task<Strategy> CreateStrategyAsync(ApplicationDbContext context, Guid teamId, Guid creatorMembershipId)
    {
        DateTimeOffset createdAtUtc = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        Strategy strategy = new(teamId, creatorMembershipId, 4, "Exécution site A", StrategySide.Attack, "Prise rapide du site A.", null, createdAtUtc);

        context.Strategies.Add(strategy);
        await context.SaveChangesAsync();

        return strategy;
    }

    private static async Task<TeamMembership> AddMembershipAsync(ApplicationDbContext context, Guid teamId, ApplicationUser user, string roleCode)
    {
        int roleId = await context.TeamRoles
            .Where(role => role.Code == roleCode)
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership membership = new(Guid.NewGuid(), teamId, user.Id, roleId, new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

        context.TeamMemberships.Add(membership);
        await context.SaveChangesAsync();

        return membership;
    }

    private sealed class StubPrivateImageService : IPrivateImageService
    {
        public StorePrivateImageResult StoreStrategyImageResult { get; set; } = StorePrivateImageResult.Failure(["Le stockage d’image ne devait pas être appelé."]);

        public Task<PrivateImageContent?> GetTeamLogoThumbnailAsync(Guid actorUserId, Guid teamId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PrivateImageContent?> GetStrategyImageAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<PrivateImageContent?>(null);
        }

        public Task<StorePrivateImageResult> ReplaceStrategyImageAsync(ReplaceStrategyImageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StorePrivateImageResult> ReplaceTeamLogoAsync(ReplaceTeamLogoRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StorePrivateImageResult> StoreStrategyImageAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StoreStrategyImageResult);
        }

        public Task<StorePrivateImageResult> StoreTeamLogoAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class TeamSetup
    {
        public Guid TeamId { get; }

        public Guid OwnerMembershipId { get; }

        public TeamSetup(Guid teamId, Guid ownerMembershipId)
        {
            TeamId = teamId;
            OwnerMembershipId = ownerMembershipId;
        }
    }
}