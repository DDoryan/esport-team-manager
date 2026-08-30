using EsportTeamManager.Application.Activities;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Activities;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EsportTeamManager.Tests.Integration.Activities;

public sealed class ActivityCreationServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenOwnerProvidesValidData_CreatesPlannedActivityInUtc()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCreationService activityCreationService = scope.ServiceProvider.GetRequiredService<IActivityCreationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);

        CreateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            3,
            new DateTime(2026, 8, 22, 18, 0, 0),
            new DateTime(2026, 8, 22, 20, 0, 0),
            [team.OwnerMembershipId],
            "  Préparation tactique  ",
            "  Travail des reprises de site.  ");

        CreateActivityResult result = await activityCreationService.CreateAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ActivityId);
        Assert.Empty(result.Errors);

        TeamActivity activity = await context.TeamActivities
            .AsNoTracking()
            .Include(item => item.ActivityType)
            .Include(item => item.Participants)
            .SingleAsync(item => item.ActivityId == result.ActivityId);

        Assert.Equal(team.TeamId, activity.TeamId);
        Assert.Equal(team.OwnerMembershipId, activity.CreatedByMembershipId);
        Assert.Equal("Meeting", activity.ActivityType.Code);
        Assert.Equal("Préparation tactique", activity.Subtitle);
        Assert.Equal("Travail des reprises de site.", activity.Description);
        Assert.Equal(new DateTimeOffset(2026, 8, 22, 16, 0, 0, TimeSpan.Zero), activity.PlannedStartUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero), activity.PlannedEndUtc);
        Assert.Equal("Europe/Paris", activity.TimeZoneId);
        Assert.Equal(ActivityStatus.Planned, activity.Status);

        ActivityParticipant participant = Assert.Single(activity.Participants);

        Assert.Equal(team.OwnerMembershipId, participant.TeamMembershipId);
        Assert.Null(participant.Attendance);
    }

    [Fact]
    public async Task CreateAsync_WhenPraccContainsOpponentAndLinks_PersistsAllRelatedData()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCreationService activityCreationService = scope.ServiceProvider.GetRequiredService<IActivityCreationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);

        CreateActivityRequest request = new(owner.Id, team.TeamId, 1, new DateTime(2026, 8, 23, 20, 0, 0), new DateTime(2026, 8, 23, 22, 0, 0), [team.OwnerMembershipId], "  Préparation tournoi  ", "  Préparation du prochain match officiel.  ", "  Navi  ", [new CreateActivityLinkRequest("  Discord  ", "  https://discord.com  "), new CreateActivityLinkRequest("  Plan de jeu  ", "  https://example.com/plan  ")]);

        CreateActivityResult result = await activityCreationService.CreateAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ActivityId);
        Assert.Empty(result.Errors);

        TeamActivity activity = await context.TeamActivities.AsNoTracking().Include(item => item.ActivityType).Include(item => item.Participants).SingleAsync(item => item.ActivityId == result.ActivityId);
        MatchDetail matchDetail = await context.MatchDetails.AsNoTracking().SingleAsync(item => item.ActivityId == result.ActivityId);
        List<ActivityLink> links = await context.ActivityLinks.AsNoTracking().Where(item => item.ActivityId == result.ActivityId).OrderBy(item => item.Name).ToListAsync();

        Assert.Equal("Pracc", activity.ActivityType.Code);
        Assert.Equal("Préparation tournoi", activity.Subtitle);
        Assert.Equal("Préparation du prochain match officiel.", activity.Description);
        Assert.Equal("Navi", matchDetail.OpponentName);
        Assert.Equal(2, links.Count);
        Assert.Equal("Discord", links[0].Name);
        Assert.Equal("https://discord.com", links[0].Url);
        Assert.Equal("Plan de jeu", links[1].Name);
        Assert.Equal("https://example.com/plan", links[1].Url);
        Assert.Single(activity.Participants);
    }

    [Fact]
    public async Task CreateAsync_WhenPraccHasNoOpponent_ReturnsFailureWithoutPersistence()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCreationService activityCreationService = scope.ServiceProvider.GetRequiredService<IActivityCreationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);

        CreateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            1,
            new DateTime(2026, 8, 23, 20, 0, 0),
            new DateTime(2026, 8, 23, 22, 0, 0),
            [team.OwnerMembershipId],
            "Préparation tournoi",
            "Préparation du prochain match officiel.");

        CreateActivityResult result = await activityCreationService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.ActivityId);
        Assert.Contains("L’équipe adverse est obligatoire pour une pracc ou un match officiel.", result.Errors);
        Assert.Empty(await context.TeamActivities.AsNoTracking().ToListAsync());
        Assert.Empty(await context.MatchDetails.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenLinkIsInvalid_DoesNotPersistPartialActivity()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCreationService activityCreationService = scope.ServiceProvider.GetRequiredService<IActivityCreationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);

        CreateActivityRequest request = new(owner.Id, team.TeamId, 1, new DateTime(2026, 8, 23, 20, 0, 0), new DateTime(2026, 8, 23, 22, 0, 0), [team.OwnerMembershipId], "Préparation tournoi", null, "Navi", [new CreateActivityLinkRequest("Lien invalide", "ftp://example.com")]);

        CreateActivityResult result = await activityCreationService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.ActivityId);
        Assert.Contains("Les informations fournies ne permettent pas de créer l’activité.", result.Errors);
        Assert.Empty(await context.TeamActivities.AsNoTracking().ToListAsync());
        Assert.Empty(await context.MatchDetails.AsNoTracking().ToListAsync());
        Assert.Empty(await context.ActivityLinks.AsNoTracking().ToListAsync());
        Assert.Empty(await context.ActivityParticipants.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Coach")]
    public async Task CreateAsync_WhenMemberHasAuthorizedRole_CreatesActivity(string roleCode)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCreationService activityCreationService = scope.ServiceProvider.GetRequiredService<IActivityCreationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser member = await CreateUserAsync(userManager, $"{roleCode.ToLowerInvariant()}@example.test", roleCode, "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership memberMembership = await AddMembershipAsync(context, team.TeamId, member, roleCode);

        CreateActivityRequest request = CreateValidRequest(member.Id, team.TeamId, memberMembership.TeamMembershipId);

        CreateActivityResult result = await activityCreationService.CreateAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ActivityId);
        Assert.Single(await context.TeamActivities.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenNonOwnerPlayerCreatesActivity_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCreationService activityCreationService = scope.ServiceProvider.GetRequiredService<IActivityCreationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership playerMembership = await AddMembershipAsync(context, team.TeamId, player, "Player");

        CreateActivityRequest request = CreateValidRequest(player.Id, team.TeamId, playerMembership.TeamMembershipId);

        CreateActivityResult result = await activityCreationService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.ActivityId);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(await context.TeamActivities.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenNoParticipantIsSelected_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCreationService activityCreationService = scope.ServiceProvider.GetRequiredService<IActivityCreationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);

        CreateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            1,
            new DateTime(2026, 8, 22, 18, 0, 0),
            new DateTime(2026, 8, 22, 20, 0, 0),
            [],
            null,
            null);

        CreateActivityResult result = await activityCreationService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Sélectionnez au moins un participant.", result.Errors);
        Assert.Empty(await context.TeamActivities.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenParticipantIsNotActive_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCreationService activityCreationService = scope.ServiceProvider.GetRequiredService<IActivityCreationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser formerMember = await CreateUserAsync(userManager, "former@example.test", "Former", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership formerMembership = await AddMembershipAsync(context, team.TeamId, formerMember, "Player");

        formerMembership.Leave(DateTimeOffset.UtcNow.AddMinutes(1));
        await context.SaveChangesAsync();

        CreateActivityRequest request = CreateValidRequest(owner.Id, team.TeamId, formerMembership.TeamMembershipId);

        CreateActivityResult result = await activityCreationService.CreateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.ActivityId);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(await context.TeamActivities.AsNoTracking().ToListAsync());
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
        services.AddScoped<IActivityCreationService, ActivityCreationService>();

        return services.BuildServiceProvider();
    }

    private static CreateActivityRequest CreateValidRequest(Guid userId, Guid teamId, Guid participantMembershipId)
    {
        return new CreateActivityRequest(
            userId,
            teamId,
            1,
            new DateTime(2026, 8, 22, 18, 0, 0),
            new DateTime(2026, 8, 22, 20, 0, 0),
            [participantMembershipId],
            null,
            null,
            "Opponent");
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
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
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        Team team = new(teamId, owner.Id, "Phoenix Academy", utcNow, "PHX", null, "Europe/Paris");
        TeamMembership membership = new(membershipId, teamId, owner.Id, playerRoleId, utcNow);

        context.Teams.Add(team);
        context.TeamMemberships.Add(membership);

        await context.SaveChangesAsync();

        return new TeamSetup(teamId, membershipId);
    }

    private static async Task<TeamMembership> AddMembershipAsync(ApplicationDbContext context, Guid teamId, ApplicationUser user, string roleCode)
    {
        int roleId = await context.TeamRoles
            .Where(role => role.Code == roleCode)
            .Select(role => role.TeamRoleId)
            .SingleAsync();

        TeamMembership membership = new(Guid.NewGuid(), teamId, user.Id, roleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(membership);
        await context.SaveChangesAsync();

        return membership;
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