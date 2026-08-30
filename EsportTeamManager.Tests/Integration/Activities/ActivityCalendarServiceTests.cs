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

public sealed class ActivityCalendarServiceTests
{
    [Fact]
    public async Task GetForPeriodAsync_WhenFiltersAreCombined_ReturnsOnlyMatchingActivity()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCalendarService calendarService = scope.ServiceProvider.GetRequiredService<IActivityCalendarService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid expectedActivityId = await CreateCompletedMatchAsync(context, team, team.OwnerMembershipId, "Préparation tournoi", "Travail de l’attaque", "Débrief complet", "Navi", 13, 8, new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero));
        await CreateCompletedMatchAsync(context, team, team.OwnerMembershipId, "Autre rencontre", "Travail défensif", "Compte rendu secondaire", "Liquid", 8, 13, new DateTimeOffset(2026, 8, 25, 16, 0, 0, TimeSpan.Zero));
        ActivityCalendarFilter filter = new(["Pracc"], [ActivityStatus.Completed], owner.Id, null, MatchResult.Victory, "navi");

        IReadOnlyCollection<CalendarActivitySummary> activities = await calendarService.GetForPeriodAsync(
            team.TeamId,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            filter);

        CalendarActivitySummary activity = Assert.Single(activities);

        Assert.Equal(expectedActivityId, activity.ActivityId);
        Assert.Equal("Navi", activity.OpponentName);
        Assert.Equal(ActivityStatus.Completed, activity.Status);
    }

    [Theory]
    [InlineData("tournoi")]
    [InlineData("attaque")]
    [InlineData("débrief")]
    [InlineData("navi")]
    public async Task GetForPeriodAsync_WhenSearchTextMatchesSupportedField_ReturnsActivity(string searchText)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCalendarService calendarService = scope.ServiceProvider.GetRequiredService<IActivityCalendarService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid expectedActivityId = await CreateCompletedMatchAsync(context, team, team.OwnerMembershipId, "Préparation tournoi", "Travail de l’attaque", "Débrief complet", "Navi", 13, 8, new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero));
        ActivityCalendarFilter filter = new(["Pracc"], [ActivityStatus.Completed], null, null, null, searchText);

        IReadOnlyCollection<CalendarActivitySummary> activities = await calendarService.GetForPeriodAsync(
            team.TeamId,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            filter);

        CalendarActivitySummary activity = Assert.Single(activities);

        Assert.Equal(expectedActivityId, activity.ActivityId);
    }

    [Theory]
    [InlineData(MatchResult.Victory, 13, 8)]
    [InlineData(MatchResult.Defeat, 8, 13)]
    [InlineData(MatchResult.Draw, 10, 10)]
    public async Task GetForPeriodAsync_WhenResultMatches_ReturnsActivity(MatchResult result, int teamScore, int opponentScore)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCalendarService calendarService = scope.ServiceProvider.GetRequiredService<IActivityCalendarService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid expectedActivityId = await CreateCompletedMatchAsync(context, team, team.OwnerMembershipId, "Match classé", "Description", "Compte rendu", "Navi", teamScore, opponentScore, new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero));
        ActivityCalendarFilter filter = new(["Pracc"], [ActivityStatus.Completed], null, null, result, null);

        IReadOnlyCollection<CalendarActivitySummary> activities = await calendarService.GetForPeriodAsync(
            team.TeamId,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            filter);

        CalendarActivitySummary activity = Assert.Single(activities);

        Assert.Equal(expectedActivityId, activity.ActivityId);
    }

    [Fact]
    public async Task GetForPeriodAsync_WhenUserHasSeveralMemberships_MatchesActivityFromPreviousMembership()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCalendarService calendarService = scope.ServiceProvider.GetRequiredService<IActivityCalendarService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership previousMembership = await AddMembershipAsync(context, team.TeamId, player, new DateTimeOffset(2026, 8, 23, 8, 30, 0, TimeSpan.Zero));
        Guid expectedActivityId = await CreateMeetingAsync(context, team, previousMembership.TeamMembershipId, new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero));

        previousMembership.Leave(new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero));
        await context.SaveChangesAsync();
        await AddMembershipAsync(context, team.TeamId, player, new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));

        ActivityCalendarFilter filter = new(["Meeting"], [ActivityStatus.Planned], player.Id, null, null, null);

        IReadOnlyCollection<CalendarActivitySummary> activities = await calendarService.GetForPeriodAsync(
            team.TeamId,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            filter);

        CalendarActivitySummary activity = Assert.Single(activities);

        Assert.Equal(expectedActivityId, activity.ActivityId);
    }

    [Fact]
    public async Task GetParticipantOptionsAsync_WhenUserHasSeveralMemberships_ReturnsSingleLogicalUser()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCalendarService calendarService = scope.ServiceProvider.GetRequiredService<IActivityCalendarService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership previousMembership = await AddMembershipAsync(context, team.TeamId, player, new DateTimeOffset(2026, 8, 23, 8, 30, 0, TimeSpan.Zero));

        await CreateMeetingAsync(context, team, previousMembership.TeamMembershipId, new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero));
        previousMembership.Leave(new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero));
        await context.SaveChangesAsync();

        TeamMembership currentMembership = await AddMembershipAsync(context, team.TeamId, player, new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));

        await CreateMeetingAsync(context, team, currentMembership.TeamMembershipId, new DateTimeOffset(2026, 8, 25, 16, 0, 0, TimeSpan.Zero));

        IReadOnlyCollection<ActivityCalendarParticipantOption> options = await calendarService.GetParticipantOptionsAsync(team.TeamId);

        ActivityCalendarParticipantOption option = Assert.Single(options);

        Assert.Equal(player.Id, option.UserId);
        Assert.Null(option.FormerMemberId);
        Assert.Equal("Player#B02", option.DisplayName);
    }

    [Fact]
    public async Task GetParticipantOptionsAsync_WhenParticipantIsPseudonymized_ReturnsFormerMemberLabel()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityCalendarService calendarService = scope.ServiceProvider.GetRequiredService<IActivityCalendarService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership membership = await AddMembershipAsync(context, team.TeamId, player, new DateTimeOffset(2026, 8, 23, 8, 30, 0, TimeSpan.Zero));

        await CreateMeetingAsync(context, team, membership.TeamMembershipId, new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero));

        Guid formerMemberId = Guid.NewGuid();
        DateTimeOffset pseudonymizedAtUtc = new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
        FormerMember formerMember = new(formerMemberId, team.TeamId, 1, pseudonymizedAtUtc);

        context.FormerMembers.Add(formerMember);
        membership.Pseudonymize(formerMemberId, pseudonymizedAtUtc);
        await context.SaveChangesAsync();

        IReadOnlyCollection<ActivityCalendarParticipantOption> options = await calendarService.GetParticipantOptionsAsync(team.TeamId);

        ActivityCalendarParticipantOption option = Assert.Single(options);

        Assert.Null(option.UserId);
        Assert.Equal(formerMemberId, option.FormerMemberId);
        Assert.Equal("Utilisateur supprimé 1", option.DisplayName);
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

        services.AddScoped<IActivityCalendarService, ActivityCalendarService>();

        return services.BuildServiceProvider();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag)
    {
        DateTimeOffset utcNow = new(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);
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
        DateTimeOffset createdAtUtc = new(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);
        Team team = new(teamId, owner.Id, "Phoenix Academy", createdAtUtc, "PHX", null, "Europe/Paris");
        TeamMembership membership = new(membershipId, teamId, owner.Id, playerRoleId, createdAtUtc);

        context.Teams.Add(team);
        context.TeamMemberships.Add(membership);
        await context.SaveChangesAsync();

        return new TeamSetup(teamId, membershipId);
    }

    private static async Task<TeamMembership> AddMembershipAsync(ApplicationDbContext context, Guid teamId, ApplicationUser user, DateTimeOffset joinedAtUtc)
    {
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership membership = new(Guid.NewGuid(), teamId, user.Id, playerRoleId, joinedAtUtc);

        context.TeamMemberships.Add(membership);
        await context.SaveChangesAsync();

        return membership;
    }

    private static async Task<Guid> CreateMeetingAsync(ApplicationDbContext context, TeamSetup team, Guid participantMembershipId, DateTimeOffset plannedStartUtc)
    {
        ActivityType activityType = await context.ActivityTypes.SingleAsync(item => item.Code == "Meeting");
        Guid activityId = Guid.NewGuid();
        TeamActivity activity = new(
            activityId,
            team.TeamId,
            activityType,
            team.OwnerMembershipId,
            plannedStartUtc,
            plannedStartUtc.AddHours(2),
            "Europe/Paris",
            [participantMembershipId],
            new DateTimeOffset(2026, 8, 23, 8, 0, 0, TimeSpan.Zero),
            "Réunion historique",
            "Préparation de la semaine",
            "Compte rendu");

        context.TeamActivities.Add(activity);
        await context.SaveChangesAsync();

        return activityId;
    }

    private static async Task<Guid> CreateCompletedMatchAsync(ApplicationDbContext context, TeamSetup team, Guid participantMembershipId, string subtitle, string description, string report, string opponentName, int teamScore, int opponentScore, DateTimeOffset plannedStartUtc)
    {
        ActivityType activityType = await context.ActivityTypes.SingleAsync(item => item.Code == "Pracc");
        Guid activityId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = new(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset plannedEndUtc = plannedStartUtc.AddHours(2);
        TeamActivity activity = new(activityId, team.TeamId, activityType, team.OwnerMembershipId, plannedStartUtc, plannedEndUtc, "Europe/Paris", [participantMembershipId], createdAtUtc, subtitle, description, report);

        activity.UpdateOpponent(opponentName, createdAtUtc.AddMinutes(5));
        activity.SetScores(teamScore, opponentScore, createdAtUtc.AddMinutes(10));
        activity.Complete(
            new Dictionary<Guid, Attendance>
            {
                [participantMembershipId] = Attendance.Present
            },
            plannedEndUtc);

        context.TeamActivities.Add(activity);
        await context.SaveChangesAsync();

        return activityId;
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
