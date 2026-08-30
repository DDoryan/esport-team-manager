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

public sealed class ActivityEditingServiceTests
{
    [Fact]
    public async Task GetAsync_WhenOwnerCanAccessActivity_ReturnsCompleteDetails()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        context.ActivityLinks.Add(new ActivityLink(activityId, "Discord", "https://discord.com"));
        await context.SaveChangesAsync();

        ActivityEditDetails details = Assert.IsType<ActivityEditDetails>(await activityEditingService.GetAsync(owner.Id, team.TeamId, activityId));

        Assert.Equal(activityId, details.ActivityId);
        Assert.Equal(team.TeamId, details.TeamId);
        Assert.Equal("Phoenix Academy", details.TeamName);
        Assert.Equal("Europe/Paris", details.TimeZoneId);
        Assert.Equal("Meeting", details.TypeCode);
        Assert.Equal("Réunion", details.TypeLabel);
        Assert.Equal("Sous-titre initial", details.Subtitle);
        Assert.Equal("Description initiale", details.Description);
        Assert.Equal("Compte rendu initial", details.Report);
        Assert.Equal(new DateTime(2026, 8, 24, 18, 0, 0), details.PlannedStartLocal);
        Assert.Equal(new DateTime(2026, 8, 24, 20, 0, 0), details.PlannedEndLocal);
        Assert.Equal(ActivityStatus.Planned, details.Status);
        Assert.True(details.CanEdit);
        Assert.Contains(details.ActivityTypes, activityType => activityType.Code == "Pracc");
        Assert.Contains(details.ActivityTypes, activityType => activityType.Code == "OfficialMatch");

        ActivityEditParticipantSummary participant = Assert.Single(details.Participants);

        Assert.Equal(team.OwnerMembershipId, participant.TeamMembershipId);
        Assert.Equal("Owner#A01", participant.DisplayName);
        Assert.Equal("Joueur", participant.RoleLabel);
        Assert.True(participant.IsOwner);
        Assert.True(participant.IsSelected);
        Assert.False(participant.IsFormerMember);
        Assert.Null(participant.Attendance);

        ActivityEditLinkSummary link = Assert.Single(details.Links);

        Assert.Equal("Discord", link.Name);
        Assert.Equal("https://discord.com", link.Url);
    }

    [Fact]
    public async Task GetAsync_WhenActivityIsPlanned_ReturnsActiveMembersAndExistingFormerParticipant()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser activeUser = await CreateUserAsync(userManager, "active@example.test", "Active", "B02");
        ApplicationUser formerUser = await CreateUserAsync(userManager, "former@example.test", "Former", "C03");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership activeMembership = await AddMembershipAsync(context, team.TeamId, activeUser, "Player");
        TeamMembership formerMembership = await AddMembershipAsync(context, team.TeamId, formerUser, "Player");
        Guid activityId = await CreatePlannedActivityAsync(context, team);
        TeamActivity activity = await context.TeamActivities
            .Include(item => item.Participants)
            .SingleAsync(item => item.ActivityId == activityId);

        activity.ReplaceParticipants(
            [team.OwnerMembershipId, formerMembership.TeamMembershipId],
            new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero));

        formerMembership.Leave(new DateTimeOffset(2026, 8, 23, 9, 30, 0, TimeSpan.Zero));

        await context.SaveChangesAsync();

        ActivityEditDetails details = Assert.IsType<ActivityEditDetails>(await activityEditingService.GetAsync(owner.Id, team.TeamId, activityId));

        Assert.Equal(3, details.Participants.Count);

        ActivityEditParticipantSummary ownerParticipant = Assert.Single(details.Participants, participant => participant.TeamMembershipId == team.OwnerMembershipId);
        ActivityEditParticipantSummary activeParticipant = Assert.Single(details.Participants, participant => participant.TeamMembershipId == activeMembership.TeamMembershipId);
        ActivityEditParticipantSummary formerParticipant = Assert.Single(details.Participants, participant => participant.TeamMembershipId == formerMembership.TeamMembershipId);

        Assert.True(ownerParticipant.IsSelected);
        Assert.False(ownerParticipant.IsFormerMember);
        Assert.False(activeParticipant.IsSelected);
        Assert.False(activeParticipant.IsFormerMember);
        Assert.True(formerParticipant.IsSelected);
        Assert.True(formerParticipant.IsFormerMember);
        Assert.Null(ownerParticipant.Attendance);
        Assert.Null(activeParticipant.Attendance);
        Assert.Null(formerParticipant.Attendance);
    }

    [Fact]
    public async Task UpdateAsync_WhenPlannedParticipantSelectionChanges_SynchronizesParticipants()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership playerMembership = await AddMembershipAsync(context, team.TeamId, player, "Player");
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Participants mis à jour",
            "Description initiale",
            "Compte rendu initial",
            [
                new UpdateActivityParticipantRequest(team.OwnerMembershipId, false, null),
            new UpdateActivityParticipantRequest(playerMembership.TeamMembershipId, true, null)
            ],
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        context.ChangeTracker.Clear();

        ActivityParticipant participant = await context.ActivityParticipants
            .AsNoTracking()
            .SingleAsync(item => item.ActivityId == activityId);

        Assert.Equal(playerMembership.TeamMembershipId, participant.TeamMembershipId);
        Assert.Null(participant.Attendance);

        TeamActivity updatedActivity = await context.TeamActivities
            .AsNoTracking()
            .SingleAsync(item => item.ActivityId == activityId);

        Assert.Equal("Participants mis à jour", updatedActivity.Subtitle);
    }

    [Fact]
    public async Task UpdateAsync_WhenNoPlannedParticipantIsSelected_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Modification interdite",
            "Description modifiée",
            "Compte rendu modifié",
            [
                new UpdateActivityParticipantRequest(team.OwnerMembershipId, false, null)
            ],
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Sélectionnez au moins un participant.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .Include(item => item.Participants)
            .SingleAsync(item => item.ActivityId == activityId);

        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
        Assert.Equal("Description initiale", unchangedActivity.Description);
        Assert.Equal("Compte rendu initial", unchangedActivity.Report);
        Assert.Equal(team.OwnerMembershipId, Assert.Single(unchangedActivity.Participants).TeamMembershipId);
    }

    [Fact]
    public async Task UpdateAsync_WhenOwnerProvidesValidData_UpdatesActivityAndCreatesMatchDetail()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            1,
            new DateTime(2026, 8, 25, 20, 0, 0),
            new DateTime(2026, 8, 25, 22, 0, 0),
            "  Préparation tournoi  ",
            "  Nouvelle description.  ",
            "  Nouveau compte rendu.  ",
            CreateParticipantRequests(team),
            [],
            "  Navi  ",
            13,
            8);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity updatedActivity = await context.TeamActivities
            .AsNoTracking()
            .Include(activity => activity.ActivityType)
            .Include(activity => activity.MatchDetail)
            .SingleAsync(activity => activity.ActivityId == activityId);

        Assert.Equal("Pracc", updatedActivity.ActivityType.Code);
        Assert.Equal("Préparation tournoi", updatedActivity.Subtitle);
        Assert.Equal("Nouvelle description.", updatedActivity.Description);
        Assert.Equal("Nouveau compte rendu.", updatedActivity.Report);
        Assert.Equal(new DateTimeOffset(2026, 8, 25, 18, 0, 0, TimeSpan.Zero), updatedActivity.PlannedStartUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 25, 20, 0, 0, TimeSpan.Zero), updatedActivity.PlannedEndUtc);
        Assert.Equal("Europe/Paris", updatedActivity.TimeZoneId);
        Assert.Equal(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero), updatedActivity.UpdatedAtUtc);
        MatchDetail matchDetail = Assert.IsType<MatchDetail>(updatedActivity.MatchDetail);

        Assert.Equal("Navi", matchDetail.OpponentName);
        Assert.Equal(13, matchDetail.TeamScore);
        Assert.Equal(8, matchDetail.OpponentScore);
        Assert.Equal(MatchResult.Victory, matchDetail.Result);

        ActivityEditDetails details = Assert.IsType<ActivityEditDetails>(await activityEditingService.GetAsync(owner.Id, team.TeamId, activityId));

        Assert.Equal(MatchResult.Victory, details.Result);
    }

    [Fact]
    public async Task UpdateAsync_WhenPraccHasNoOpponent_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            1,
            new DateTime(2026, 8, 25, 20, 0, 0),
            new DateTime(2026, 8, 25, 22, 0, 0),
            "Préparation tournoi",
            "Nouvelle description.",
            "Nouveau compte rendu.",
            CreateParticipantRequests(team),
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("L’équipe adverse est obligatoire pour une pracc ou un match officiel.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .Include(activity => activity.ActivityType)
            .Include(activity => activity.MatchDetail)
            .SingleAsync(activity => activity.ActivityId == activityId);

        Assert.Equal("Meeting", unchangedActivity.ActivityType.Code);
        Assert.Null(unchangedActivity.MatchDetail);
        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
    }

    [Theory]
    [InlineData(13, null)]
    [InlineData(null, 8)]
    public async Task UpdateAsync_WhenOnlyOneScoreIsProvided_ReturnsFailureWithoutModification(int? teamScore, int? opponentScore)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            1,
            new DateTime(2026, 8, 25, 20, 0, 0),
            new DateTime(2026, 8, 25, 22, 0, 0),
            "Préparation tournoi",
            "Nouvelle description.",
            "Nouveau compte rendu.",
            CreateParticipantRequests(team),
            [],
            "Navi",
            teamScore,
            opponentScore);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Les deux scores doivent être renseignés ensemble.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .Include(activity => activity.ActivityType)
            .Include(activity => activity.MatchDetail)
            .SingleAsync(activity => activity.ActivityId == activityId);

        Assert.Equal("Meeting", unchangedActivity.ActivityType.Code);
        Assert.Null(unchangedActivity.MatchDetail);
        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
    }

    [Fact]
    public async Task UpdateAsync_WhenCompletedMatchScoresChange_UpdatesScoresAndResult()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreateCompletedMatchActivityAsync(context, team);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            1,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Match terminé",
            "Description modifiée.",
            "Compte rendu modifié.",
            [
                new UpdateActivityParticipantRequest(team.OwnerMembershipId, true, Attendance.Present)
            ],
            [],
            "  Fnatic  ",
            10,
            10);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity updatedActivity = await context.TeamActivities
            .AsNoTracking()
            .Include(activity => activity.MatchDetail)
            .SingleAsync(activity => activity.ActivityId == activityId);

        Assert.Equal(ActivityStatus.Completed, updatedActivity.Status);

        MatchDetail matchDetail = Assert.IsType<MatchDetail>(updatedActivity.MatchDetail);

        Assert.Equal("Fnatic", matchDetail.OpponentName);
        Assert.Equal(10, matchDetail.TeamScore);
        Assert.Equal(10, matchDetail.OpponentScore);
        Assert.Equal(MatchResult.Draw, matchDetail.Result);
    }

    [Fact]
    public async Task UpdateAsync_WhenCompletedMatchHasNoScores_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreateCompletedMatchActivityAsync(context, team);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            1,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Match terminé",
            "Description modifiée.",
            "Compte rendu modifié.",
            [
                new UpdateActivityParticipantRequest(team.OwnerMembershipId, true, Attendance.Present)
            ],
            [],
            "Navi");

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Les deux scores sont obligatoires pour une activité terminée.", result.Errors);

        context.ChangeTracker.Clear();

        MatchDetail unchangedMatchDetail = await context.MatchDetails
            .AsNoTracking()
            .SingleAsync(matchDetail => matchDetail.ActivityId == activityId);

        Assert.Equal("Navi", unchangedMatchDetail.OpponentName);
        Assert.Equal(13, unchangedMatchDetail.TeamScore);
        Assert.Equal(8, unchangedMatchDetail.OpponentScore);
        Assert.Equal(MatchResult.Victory, unchangedMatchDetail.Result);
    }



    [Theory]
    [InlineData("Manager")]
    [InlineData("Coach")]
    public async Task UpdateAsync_WhenMemberHasAuthorizedRole_UpdatesActivity(string roleCode)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser member = await CreateUserAsync(userManager, $"{roleCode.ToLowerInvariant()}@example.test", roleCode, "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        await AddMembershipAsync(context, team.TeamId, member, roleCode);
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        UpdateActivityRequest request = new(
            member.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 25, 18, 0, 0),
            new DateTime(2026, 8, 25, 20, 0, 0),
            $"Modification par {roleCode}",
            "Description modifiée.",
            null,
            CreateParticipantRequests(team),
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity updatedActivity = await context.TeamActivities
            .AsNoTracking()
            .SingleAsync(activity => activity.ActivityId == activityId);

        Assert.Equal($"Modification par {roleCode}", updatedActivity.Subtitle);
        Assert.Equal("Description modifiée.", updatedActivity.Description);
    }

    [Fact]
    public async Task UpdateAsync_WhenNonOwnerPlayerAttemptsUpdate_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        await AddMembershipAsync(context, team.TeamId, player, "Player");
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        ActivityEditDetails details = Assert.IsType<ActivityEditDetails>(await activityEditingService.GetAsync(player.Id, team.TeamId, activityId));

        Assert.False(details.CanEdit);

        UpdateActivityRequest request = new(
            player.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 25, 18, 0, 0),
            new DateTime(2026, 8, 25, 20, 0, 0),
            "Modification interdite",
            null,
            null,
            CreateParticipantRequests(team),
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Vous n’êtes pas autorisé à modifier cette activité.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .SingleAsync(activity => activity.ActivityId == activityId);

        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
        Assert.Equal("Description initiale", unchangedActivity.Description);
        Assert.Equal("Compte rendu initial", unchangedActivity.Report);
    }

    [Fact]
    public async Task UpdateAsync_WhenPeriodIsInvalid_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 25, 22, 0, 0),
            new DateTime(2026, 8, 25, 20, 0, 0),
            "Modification invalide",
            "Description invalide",
            null,
            CreateParticipantRequests(team),
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("La fin prévue doit être strictement postérieure au début prévu.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .SingleAsync(activity => activity.ActivityId == activityId);

        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero), unchangedActivity.PlannedStartUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.Zero), unchangedActivity.PlannedEndUtc);
    }

    [Fact]
    public async Task UpdateAsync_WhenLinksAreAddedUpdatedAndRemoved_SynchronizesLinks()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);
        ActivityLink updatedLink = new(activityId, "Discord", "https://discord.com/ancien");
        ActivityLink removedLink = new(activityId, "Ancien document", "https://example.test/ancien");

        context.ActivityLinks.AddRange(updatedLink, removedLink);
        await context.SaveChangesAsync();

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Sous-titre initial",
            "Description initiale",
            "Compte rendu initial",
            CreateParticipantRequests(team),
            [
                new UpdateActivityLinkRequest(updatedLink.ActivityLinkId, "  Discord équipe  ", "  https://discord.gg/phoenix  "),
            new UpdateActivityLinkRequest(Guid.Empty, "  VOD  ", "  https://example.test/vod  ")
            ]);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        context.ChangeTracker.Clear();

        List<ActivityLink> links = await context.ActivityLinks
            .AsNoTracking()
            .Where(link => link.ActivityId == activityId)
            .OrderBy(link => link.Name)
            .ToListAsync();

        Assert.Equal(2, links.Count);

        ActivityLink persistedUpdatedLink = Assert.Single(links, link => link.ActivityLinkId == updatedLink.ActivityLinkId);

        Assert.Equal("Discord équipe", persistedUpdatedLink.Name);
        Assert.Equal("https://discord.gg/phoenix", persistedUpdatedLink.Url);
        Assert.DoesNotContain(links, link => link.ActivityLinkId == removedLink.ActivityLinkId);

        ActivityLink addedLink = Assert.Single(links, link => link.ActivityLinkId != updatedLink.ActivityLinkId);

        Assert.NotEqual(Guid.Empty, addedLink.ActivityLinkId);
        Assert.Equal("VOD", addedLink.Name);
        Assert.Equal("https://example.test/vod", addedLink.Url);
    }

    [Fact]
    public async Task UpdateAsync_WhenLinkIsInvalid_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);
        ActivityLink existingLink = new(activityId, "Discord", "https://discord.com");

        context.ActivityLinks.Add(existingLink);
        await context.SaveChangesAsync();

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 25, 18, 0, 0),
            new DateTime(2026, 8, 25, 20, 0, 0),
            "Modification invalide",
            "Description modifiée",
            "Compte rendu modifié",
            CreateParticipantRequests(team),
            [
                new UpdateActivityLinkRequest(existingLink.ActivityLinkId, "Discord modifié", "ftp://example.test")
            ]);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Les informations fournies ne permettent pas de modifier les liens de l’activité.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .SingleAsync(activity => activity.ActivityId == activityId);

        ActivityLink unchangedLink = await context.ActivityLinks
            .AsNoTracking()
            .SingleAsync(link => link.ActivityLinkId == existingLink.ActivityLinkId);

        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
        Assert.Equal("Description initiale", unchangedActivity.Description);
        Assert.Equal("Discord", unchangedLink.Name);
        Assert.Equal("https://discord.com", unchangedLink.Url);
    }

    [Fact]
    public async Task UpdateAsync_WhenLinkBelongsToAnotherActivity_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);
        Guid otherActivityId = await CreatePlannedActivityAsync(context, team);
        ActivityLink otherActivityLink = new(otherActivityId, "Lien externe", "https://example.test/externe");

        context.ActivityLinks.Add(otherActivityLink);
        await context.SaveChangesAsync();

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Modification interdite",
            "Description modifiée",
            null,
            CreateParticipantRequests(team),
            [
                new UpdateActivityLinkRequest(otherActivityLink.ActivityLinkId, "Lien détourné", "https://example.test/modifie")
            ]);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Un lien fourni n’appartient pas à cette activité.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .SingleAsync(activity => activity.ActivityId == activityId);

        ActivityLink unchangedOtherLink = await context.ActivityLinks
            .AsNoTracking()
            .SingleAsync(link => link.ActivityLinkId == otherActivityLink.ActivityLinkId);

        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
        Assert.Equal("Lien externe", unchangedOtherLink.Name);
        Assert.Equal("https://example.test/externe", unchangedOtherLink.Url);
        Assert.Empty(await context.ActivityLinks.AsNoTracking().Where(link => link.ActivityId == activityId).ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_WhenExistingLinkIdentifierIsRepeated_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);
        ActivityLink existingLink = new(activityId, "Discord", "https://discord.com");

        context.ActivityLinks.Add(existingLink);
        await context.SaveChangesAsync();

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Modification invalide",
            "Description modifiée",
            null,
            CreateParticipantRequests(team),
            [
                new UpdateActivityLinkRequest(existingLink.ActivityLinkId, "Discord principal", "https://discord.gg/phoenix"),
            new UpdateActivityLinkRequest(existingLink.ActivityLinkId, "Discord secondaire", "https://discord.gg/secondaire")
            ]);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("La demande contient plusieurs fois le même lien.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .SingleAsync(activity => activity.ActivityId == activityId);

        ActivityLink unchangedLink = await context.ActivityLinks
            .AsNoTracking()
            .SingleAsync(link => link.ActivityLinkId == existingLink.ActivityLinkId);

        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
        Assert.Equal("Discord", unchangedLink.Name);
        Assert.Equal("https://discord.com", unchangedLink.Url);
    }

    [Fact]
    public async Task UpdateAsync_WhenInactiveMemberIsAddedToPlannedActivity_ReturnsFailureWithoutModification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser formerUser = await CreateUserAsync(userManager, "former@example.test", "Former", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership formerMembership = await AddMembershipAsync(context, team.TeamId, formerUser, "Player");
        Guid activityId = await CreatePlannedActivityAsync(context, team);

        formerMembership.Leave(new DateTimeOffset(2026, 8, 23, 9, 30, 0, TimeSpan.Zero));
        await context.SaveChangesAsync();

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Modification interdite",
            "Description modifiée",
            "Compte rendu modifié",
            [
                new UpdateActivityParticipantRequest(team.OwnerMembershipId, true, null),
            new UpdateActivityParticipantRequest(formerMembership.TeamMembershipId, true, null)
            ],
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("Un ou plusieurs participants ne sont pas membres actifs de cette équipe et ne sont pas déjà associés à l’activité.", result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity unchangedActivity = await context.TeamActivities
            .AsNoTracking()
            .Include(item => item.Participants)
            .SingleAsync(item => item.ActivityId == activityId);

        Assert.Equal("Sous-titre initial", unchangedActivity.Subtitle);
        Assert.Equal(team.OwnerMembershipId, Assert.Single(unchangedActivity.Participants).TeamMembershipId);
    }

    [Fact]
    public async Task UpdateAsync_WhenCompletedAttendancesChange_UpdatesEveryAttendance()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateUserAsync(userManager, "player@example.test", "Player", "B02");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership playerMembership = await AddMembershipAsync(context, team.TeamId, player, "Player");
        Guid activityId = await CreateCompletedActivityAsync(context, team, playerMembership.TeamMembershipId);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Sous-titre initial",
            "Description initiale",
            "Compte rendu initial",
            [
                new UpdateActivityParticipantRequest(team.OwnerMembershipId, true, Attendance.Absent),
            new UpdateActivityParticipantRequest(playerMembership.TeamMembershipId, true, Attendance.Present)
            ],
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        context.ChangeTracker.Clear();

        List<ActivityParticipant> participants = await context.ActivityParticipants
            .AsNoTracking()
            .Where(participant => participant.ActivityId == activityId)
            .ToListAsync();

        Assert.Equal(Attendance.Absent, Assert.Single(participants, participant => participant.TeamMembershipId == team.OwnerMembershipId).Attendance);
        Assert.Equal(Attendance.Present, Assert.Single(participants, participant => participant.TeamMembershipId == playerMembership.TeamMembershipId).Attendance);
    }

    [Fact]
    public async Task UpdateAsync_WhenCompletedParticipantSelectionChanges_SynchronizesParticipantsAndAttendances()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser removedPlayer = await CreateUserAsync(userManager, "removed@example.test", "Removed", "B02");
        ApplicationUser addedPlayer = await CreateUserAsync(userManager, "added@example.test", "Added", "C03");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership removedMembership = await AddMembershipAsync(context, team.TeamId, removedPlayer, "Player");
        TeamMembership addedMembership = await AddMembershipAsync(context, team.TeamId, addedPlayer, "Player");
        Guid activityId = await CreateCompletedActivityAsync(context, team, removedMembership.TeamMembershipId);

        UpdateActivityRequest request = new(
            owner.Id,
            team.TeamId,
            activityId,
            3,
            new DateTime(2026, 8, 24, 18, 0, 0),
            new DateTime(2026, 8, 24, 20, 0, 0),
            "Participants terminés modifiés",
            "Description modifiée",
            "Compte rendu modifié",
            [
                new UpdateActivityParticipantRequest(team.OwnerMembershipId, true, Attendance.Absent),
            new UpdateActivityParticipantRequest(removedMembership.TeamMembershipId, false, null),
            new UpdateActivityParticipantRequest(addedMembership.TeamMembershipId, true, Attendance.Present)
            ],
            []);

        UpdateActivityResult result = await activityEditingService.UpdateAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        context.ChangeTracker.Clear();

        TeamActivity updatedActivity = await context.TeamActivities
            .AsNoTracking()
            .Include(item => item.Participants)
            .SingleAsync(item => item.ActivityId == activityId);

        Assert.Equal("Participants terminés modifiés", updatedActivity.Subtitle);
        Assert.Equal("Description modifiée", updatedActivity.Description);
        Assert.Equal("Compte rendu modifié", updatedActivity.Report);
        Assert.Equal(2, updatedActivity.Participants.Count);
        Assert.DoesNotContain(updatedActivity.Participants, participant => participant.TeamMembershipId == removedMembership.TeamMembershipId);
        Assert.Equal(Attendance.Absent, Assert.Single(updatedActivity.Participants, participant => participant.TeamMembershipId == team.OwnerMembershipId).Attendance);
        Assert.Equal(Attendance.Present, Assert.Single(updatedActivity.Participants, participant => participant.TeamMembershipId == addedMembership.TeamMembershipId).Attendance);
    }

    [Fact]
    public async Task GetAsync_WhenCompletedActivityIsEditable_ReturnsCurrentAndHistoricallyEligibleMembers()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser activeUser = await CreateUserAsync(userManager, "active@example.test", "Active", "B02");
        ApplicationUser eligibleFormerUser = await CreateUserAsync(userManager, "eligible@example.test", "Eligible", "C03");
        ApplicationUser ineligibleFormerUser = await CreateUserAsync(userManager, "ineligible@example.test", "Ineligible", "D04");
        TeamSetup team = await CreateTeamAsync(context, owner);
        TeamMembership activeMembership = await AddMembershipAsync(context, team.TeamId, activeUser, "Player");
        TeamMembership eligibleFormerMembership = await AddMembershipAsync(context, team.TeamId, eligibleFormerUser, "Player");
        TeamMembership ineligibleFormerMembership = await AddMembershipAsync(context, team.TeamId, ineligibleFormerUser, "Player");
        Guid activityId = await CreateCompletedActivityAsync(context, team, activeMembership.TeamMembershipId);

        eligibleFormerMembership.Leave(new DateTimeOffset(2026, 8, 24, 17, 0, 0, TimeSpan.Zero));
        ineligibleFormerMembership.Leave(new DateTimeOffset(2026, 8, 24, 15, 0, 0, TimeSpan.Zero));

        await context.SaveChangesAsync();

        ActivityEditDetails details = Assert.IsType<ActivityEditDetails>(await activityEditingService.GetAsync(owner.Id, team.TeamId, activityId));

        Assert.True(details.CanEdit);
        Assert.Equal(ActivityStatus.Completed, details.Status);
        Assert.Equal(3, details.Participants.Count);

        ActivityEditParticipantSummary ownerParticipant = Assert.Single(details.Participants, participant => participant.TeamMembershipId == team.OwnerMembershipId);
        ActivityEditParticipantSummary activeParticipant = Assert.Single(details.Participants, participant => participant.TeamMembershipId == activeMembership.TeamMembershipId);
        ActivityEditParticipantSummary eligibleFormerParticipant = Assert.Single(details.Participants, participant => participant.TeamMembershipId == eligibleFormerMembership.TeamMembershipId);

        Assert.True(ownerParticipant.IsSelected);
        Assert.Equal(Attendance.Present, ownerParticipant.Attendance);
        Assert.True(activeParticipant.IsSelected);
        Assert.Equal(Attendance.Absent, activeParticipant.Attendance);
        Assert.False(eligibleFormerParticipant.IsSelected);
        Assert.True(eligibleFormerParticipant.IsFormerMember);
        Assert.Null(eligibleFormerParticipant.Attendance);
        Assert.DoesNotContain(details.Participants, participant => participant.TeamMembershipId == ineligibleFormerMembership.TeamMembershipId);
    }

    [Fact]
    public async Task GetAsync_WhenActivityIsCancelled_ReturnsReadOnlyDetails()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IActivityEditingService activityEditingService = scope.ServiceProvider.GetRequiredService<IActivityEditingService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        TeamSetup team = await CreateTeamAsync(context, owner);
        Guid activityId = await CreatePlannedActivityAsync(context, team);
        TeamActivity activity = await context.TeamActivities.SingleAsync(item => item.ActivityId == activityId);

        activity.Cancel("Joueurs indisponibles", new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        await context.SaveChangesAsync();

        ActivityEditDetails details = Assert.IsType<ActivityEditDetails>(await activityEditingService.GetAsync(owner.Id, team.TeamId, activityId));

        Assert.Equal(ActivityStatus.Cancelled, details.Status);
        Assert.Equal("Joueurs indisponibles", details.CancellationReason);
        Assert.False(details.CanEdit);
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

        services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero)));
        services.AddScoped<IActivityEditingService, ActivityEditingService>();

        return services.BuildServiceProvider();
    }

    private static IReadOnlyCollection<UpdateActivityParticipantRequest> CreateParticipantRequests(TeamSetup team)
    {
        return
        [
            new UpdateActivityParticipantRequest(team.OwnerMembershipId, true, null)
        ];
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

    private static async Task<TeamMembership> AddMembershipAsync(ApplicationDbContext context, Guid teamId, ApplicationUser user, string roleCode)
    {
        int roleId = await context.TeamRoles
            .Where(role => role.Code == roleCode)
            .Select(role => role.TeamRoleId)
            .SingleAsync();

        TeamMembership membership = new(Guid.NewGuid(), teamId, user.Id, roleId, new DateTimeOffset(2026, 8, 23, 8, 30, 0, TimeSpan.Zero));

        context.TeamMemberships.Add(membership);
        await context.SaveChangesAsync();

        return membership;
    }

    private static async Task<Guid> CreatePlannedActivityAsync(ApplicationDbContext context, TeamSetup team)
    {
        ActivityType activityType = await context.ActivityTypes.SingleAsync(item => item.Code == "Meeting");
        Guid activityId = Guid.NewGuid();
        TeamActivity activity = new(
            activityId,
            team.TeamId,
            activityType,
            team.OwnerMembershipId,
            new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.Zero),
            "Europe/Paris",
            [team.OwnerMembershipId],
            new DateTimeOffset(2026, 8, 23, 8, 0, 0, TimeSpan.Zero),
            "Sous-titre initial",
            "Description initiale",
            "Compte rendu initial");

        context.TeamActivities.Add(activity);
        await context.SaveChangesAsync();

        return activityId;
    }

    private static async Task<Guid> CreateCompletedMatchActivityAsync(ApplicationDbContext context, TeamSetup team)
    {
        ActivityType activityType = await context.ActivityTypes.SingleAsync(item => item.Code == "Pracc");
        Guid activityId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = new(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset plannedStartUtc = new(2026, 8, 24, 16, 0, 0, TimeSpan.Zero);
        DateTimeOffset plannedEndUtc = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
        TeamActivity activity = new(activityId, team.TeamId, activityType, team.OwnerMembershipId, plannedStartUtc, plannedEndUtc, "Europe/Paris", [team.OwnerMembershipId], createdAtUtc, "Match terminé", "Description initiale", "Compte rendu initial");

        activity.UpdateOpponent("Navi", createdAtUtc.AddMinutes(5));
        activity.SetScores(13, 8, createdAtUtc.AddMinutes(10));
        activity.Complete(
            new Dictionary<Guid, Attendance>
            {
                [team.OwnerMembershipId] = Attendance.Present
            },
            plannedEndUtc);

        context.TeamActivities.Add(activity);
        await context.SaveChangesAsync();

        return activityId;
    }

    private static async Task<Guid> CreateCompletedActivityAsync(ApplicationDbContext context, TeamSetup team, Guid additionalMembershipId)
    {
        Guid activityId = await CreatePlannedActivityAsync(context, team);
        TeamActivity activity = await context.TeamActivities
            .Include(item => item.Participants)
            .SingleAsync(item => item.ActivityId == activityId);

        activity.ReplaceParticipants(
            [team.OwnerMembershipId, additionalMembershipId],
            new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero));

        activity.Complete(
            new Dictionary<Guid, Attendance>
            {
                [team.OwnerMembershipId] = Attendance.Present,
                [additionalMembershipId] = Attendance.Absent
            },
            new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));

        await context.SaveChangesAsync();

        return activityId;
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