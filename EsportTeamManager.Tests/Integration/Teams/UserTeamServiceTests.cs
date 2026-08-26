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

    [Fact]
    public async Task GetManagementDetailsAsync_WhenUserHasActiveMembership_ReturnsTeamAndActiveMembers()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser member = await CreateUserAsync(userManager, "member@example.test", "Member", "B02");

        CreateTeamResult result = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.TeamId);

        Guid teamId = result.TeamId.Value;
        Team team = await context.Teams.SingleAsync(item => item.TeamId == teamId);
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow;
        TeamMembership membership = new(Guid.NewGuid(), teamId, member.Id, managerRoleId, joinedAtUtc);

        team.UpdateInformation("Phoenix Academy", "PHX", "Équipe amateur compétitive.", "Europe/Paris");
        context.TeamMemberships.Add(membership);

        await context.SaveChangesAsync();

        TeamManagementDetails? details = await teamService.GetManagementDetailsAsync(owner.Id, teamId);

        Assert.NotNull(details);
        Assert.Equal(teamId, details.TeamId);
        Assert.Equal("Phoenix Academy", details.Name);
        Assert.Equal("PHX", details.Tag);
        Assert.Equal("Équipe amateur compétitive.", details.Description);
        Assert.Equal("Europe/Paris", details.TimeZoneId);
        Assert.True(details.CurrentUserIsOwner);
        Assert.True(details.CurrentUserCanInviteMembers);
        Assert.Equal(3, details.AvailableInvitationRoles.Count);
        Assert.Contains(details.AvailableInvitationRoles, role => role.Label == "Manager");
        Assert.Contains(details.AvailableInvitationRoles, role => role.Label == "Coach");
        Assert.Contains(details.AvailableInvitationRoles, role => role.Label == "Joueur");
        Assert.Equal(2, details.Members.Count);

        TeamMemberSummary ownerSummary = details.Members.First();
        TeamMemberSummary memberSummary = details.Members.Single(item => item.TeamMembershipId == membership.TeamMembershipId);

        Assert.Equal("Owner", ownerSummary.Pseudo);
        Assert.Equal("A01", ownerSummary.Tag);
        Assert.Equal("Joueur", ownerSummary.RoleLabel);
        Assert.True(ownerSummary.IsOwner);

        Assert.Equal("Member", memberSummary.Pseudo);
        Assert.Equal("B02", memberSummary.Tag);
        Assert.Equal("Manager", memberSummary.RoleLabel);
        Assert.False(memberSummary.IsOwner);
        Assert.Equal(joinedAtUtc, memberSummary.JoinedAtUtc);
    }

    [Fact]
    public async Task GetManagementDetailsAsync_WhenMembershipIsClosed_ReturnsNull()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser formerMember = await CreateUserAsync(userManager, "former@example.test", "Former", "B02");

        CreateTeamResult result = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.TeamId);

        Guid teamId = result.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        TeamMembership membership = new(Guid.NewGuid(), teamId, formerMember.Id, playerRoleId, joinedAtUtc);

        context.TeamMemberships.Add(membership);

        await context.SaveChangesAsync();

        membership.Leave(joinedAtUtc.AddDays(1));

        await context.SaveChangesAsync();

        TeamManagementDetails? details = await teamService.GetManagementDetailsAsync(formerMember.Id, teamId);

        Assert.Null(details);
    }

    [Fact]
    public async Task GetManagementDetailsAsync_WhenUserRejoins_ReturnsOnlyNewActivePeriod()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser returningMember = await CreateUserAsync(userManager, "returning@example.test", "Returning", "B02");

        CreateTeamResult result = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.TeamId);

        Guid teamId = result.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        DateTimeOffset firstJoinedAtUtc = DateTimeOffset.UtcNow.AddDays(-4);
        DateTimeOffset rejoinedAtUtc = DateTimeOffset.UtcNow;
        TeamMembership formerMembership = new(Guid.NewGuid(), teamId, returningMember.Id, playerRoleId, firstJoinedAtUtc);

        context.TeamMemberships.Add(formerMembership);

        await context.SaveChangesAsync();

        formerMembership.Leave(firstJoinedAtUtc.AddDays(2));

        await context.SaveChangesAsync();

        TeamMembership activeMembership = new(Guid.NewGuid(), teamId, returningMember.Id, managerRoleId, rejoinedAtUtc);

        context.TeamMemberships.Add(activeMembership);

        await context.SaveChangesAsync();

        TeamManagementDetails? details = await teamService.GetManagementDetailsAsync(returningMember.Id, teamId);

        Assert.NotNull(details);
        Assert.False(details.CurrentUserIsOwner);

        TeamMemberSummary memberSummary = Assert.Single(details.Members, item => item.Pseudo == "Returning");

        Assert.Equal(activeMembership.TeamMembershipId, memberSummary.TeamMembershipId);
        Assert.Equal("B02", memberSummary.Tag);
        Assert.Equal("Manager", memberSummary.RoleLabel);
        Assert.Equal(rejoinedAtUtc, memberSummary.JoinedAtUtc);
        Assert.DoesNotContain(details.Members, item => item.TeamMembershipId == formerMembership.TeamMembershipId);
    }

    [Fact]
    public async Task InviteMemberAsync_WhenOwnerInvitesActiveAccount_CreatesPendingInvitationAndTrace()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser recipient = await CreateActiveUserAsync(userManager, "recipient@example.test", "Recipient", "B02");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int coachRoleId = await context.TeamRoles
            .Where(role => role.Code == "Coach")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        InviteTeamMemberRequest request = new(owner.Id, teamId, "  Recipient#B02  ", coachRoleId);
        DateTimeOffset beforeInvitationUtc = DateTimeOffset.UtcNow;

        InviteTeamMemberResult result = await teamService.InviteMemberAsync(request);

        DateTimeOffset afterInvitationUtc = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.NotNull(result.InvitationId);
        Assert.Empty(result.Errors);

        Invitation invitation = await context.Invitations.AsNoTracking().SingleAsync(item => item.InvitationId == result.InvitationId);
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_INVITATION_CREATED");

        Assert.Equal(teamId, invitation.TeamId);
        Assert.Equal(owner.Id, invitation.SenderUserId);
        Assert.Equal(recipient.Id, invitation.RecipientUserId);
        Assert.Equal(coachRoleId, invitation.ProposedTeamRoleId);
        Assert.Equal(RequestStatus.Pending, invitation.Status);
        Assert.Null(invitation.CreatedMembershipId);
        Assert.Null(invitation.ResolvedAtUtc);
        Assert.InRange(invitation.CreatedAtUtc, beforeInvitationUtc, afterInvitationUtc);

        Assert.Equal(owner.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(Invitation), trace.ObjectType);
        Assert.Equal(invitation.InvitationId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
        Assert.Equal(trace.OccurredAtUtc.AddMonths(6), trace.ExpiresAtUtc);
        Assert.False(await context.TeamMemberships.AsNoTracking().AnyAsync(membership => membership.TeamId == teamId && membership.UserId == recipient.Id));
    }

    [Fact]
    public async Task GetManagementDetailsAsync_WhenUserIsManager_ReturnsOnlyCoachAndPlayerInvitationRoles()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser manager = await CreateActiveUserAsync(userManager, "manager@example.test", "ManagerUser", "B02");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership managerMembership = new(Guid.NewGuid(), teamId, manager.Id, managerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(managerMembership);

        await context.SaveChangesAsync();

        TeamManagementDetails? details = await teamService.GetManagementDetailsAsync(manager.Id, teamId);

        Assert.NotNull(details);
        Assert.False(details.CurrentUserIsOwner);
        Assert.True(details.CurrentUserCanInviteMembers);
        Assert.Equal(2, details.AvailableInvitationRoles.Count);
        Assert.Contains(details.AvailableInvitationRoles, role => role.Label == "Coach");
        Assert.Contains(details.AvailableInvitationRoles, role => role.Label == "Joueur");
        Assert.DoesNotContain(details.AvailableInvitationRoles, role => role.Label == "Manager");
    }

    [Fact]
    public async Task InviteMemberAsync_WhenManagerInvitesPlayer_CreatesPendingInvitation()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser manager = await CreateActiveUserAsync(userManager, "manager@example.test", "ManagerUser", "B02");
        ApplicationUser recipient = await CreateActiveUserAsync(userManager, "recipient@example.test", "Recipient", "C03");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership managerMembership = new(Guid.NewGuid(), teamId, manager.Id, managerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(managerMembership);

        await context.SaveChangesAsync();

        InviteTeamMemberRequest request = new(manager.Id, teamId, "Recipient#C03", playerRoleId);

        InviteTeamMemberResult result = await teamService.InviteMemberAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.NotNull(result.InvitationId);

        Invitation invitation = await context.Invitations.AsNoTracking().SingleAsync();

        Assert.Equal(manager.Id, invitation.SenderUserId);
        Assert.Equal(recipient.Id, invitation.RecipientUserId);
        Assert.Equal(playerRoleId, invitation.ProposedTeamRoleId);
        Assert.Equal(RequestStatus.Pending, invitation.Status);
    }

    [Fact]
    public async Task InviteMemberAsync_WhenManagerProposesManagerRole_ReturnsDenied()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser manager = await CreateActiveUserAsync(userManager, "manager@example.test", "ManagerUser", "B02");
        ApplicationUser recipient = await CreateActiveUserAsync(userManager, "recipient@example.test", "Recipient", "C03");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership managerMembership = new(Guid.NewGuid(), teamId, manager.Id, managerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(managerMembership);

        await context.SaveChangesAsync();

        InviteTeamMemberRequest request = new(manager.Id, teamId, "Recipient#C03", managerRoleId);

        InviteTeamMemberResult result = await teamService.InviteMemberAsync(request);

        Assert.False(result.Succeeded);
        Assert.True(result.AccessDenied);
        Assert.Null(result.InvitationId);
        Assert.Empty(result.Errors);
        Assert.Empty(await context.Invitations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task InviteMemberAsync_WhenPlayerAttemptsInvitation_ReturnsDenied()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser player = await CreateActiveUserAsync(userManager, "player@example.test", "PlayerUser", "B02");
        ApplicationUser recipient = await CreateActiveUserAsync(userManager, "recipient@example.test", "Recipient", "C03");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership playerMembership = new(Guid.NewGuid(), teamId, player.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(playerMembership);

        await context.SaveChangesAsync();

        TeamManagementDetails? details = await teamService.GetManagementDetailsAsync(player.Id, teamId);

        Assert.NotNull(details);
        Assert.False(details.CurrentUserIsOwner);
        Assert.False(details.CurrentUserCanInviteMembers);
        Assert.Empty(details.AvailableInvitationRoles);

        InviteTeamMemberRequest request = new(player.Id, teamId, "Recipient#C03", playerRoleId);

        InviteTeamMemberResult result = await teamService.InviteMemberAsync(request);

        Assert.False(result.Succeeded);
        Assert.True(result.AccessDenied);
        Assert.Null(result.InvitationId);
        Assert.Empty(result.Errors);
        Assert.Empty(await context.Invitations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task InviteMemberAsync_WhenTargetCannotBeInvited_ReturnsSameNeutralFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser activeMember = await CreateActiveUserAsync(userManager, "active@example.test", "ActiveMember", "B02");
        ApplicationUser pendingRecipient = await CreateActiveUserAsync(userManager, "pending@example.test", "PendingUser", "C03");
        ApplicationUser unconfirmedAccount = await CreateUserAsync(userManager, "unconfirmed@example.test", "Unconfirmed", "D04");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership activeMembership = new(Guid.NewGuid(), teamId, activeMember.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(activeMembership);

        await context.SaveChangesAsync();

        InviteTeamMemberResult initialInvitation = await teamService.InviteMemberAsync(new InviteTeamMemberRequest(owner.Id, teamId, "PendingUser#C03", playerRoleId));

        Assert.True(initialInvitation.Succeeded);

        InviteTeamMemberResult unknownAccountResult = await teamService.InviteMemberAsync(new InviteTeamMemberRequest(owner.Id, teamId, "Unknown#D04", playerRoleId));
        InviteTeamMemberResult malformedIdentityResult = await teamService.InviteMemberAsync(new InviteTeamMemberRequest(owner.Id, teamId, "InvalidIdentity", playerRoleId));
        InviteTeamMemberResult unconfirmedAccountResult = await teamService.InviteMemberAsync(new InviteTeamMemberRequest(owner.Id, teamId, "Unconfirmed#D04", playerRoleId));
        InviteTeamMemberResult activeMemberResult = await teamService.InviteMemberAsync(new InviteTeamMemberRequest(owner.Id, teamId, "ActiveMember#B02", playerRoleId));
        InviteTeamMemberResult pendingInvitationResult = await teamService.InviteMemberAsync(new InviteTeamMemberRequest(owner.Id, teamId, "PendingUser#C03", playerRoleId));

        Assert.False(unknownAccountResult.Succeeded);
        Assert.False(unknownAccountResult.AccessDenied);
        Assert.False(malformedIdentityResult.Succeeded);
        Assert.False(malformedIdentityResult.AccessDenied);
        Assert.False(unconfirmedAccountResult.Succeeded);
        Assert.False(unconfirmedAccountResult.AccessDenied);
        Assert.False(activeMemberResult.Succeeded);
        Assert.False(activeMemberResult.AccessDenied);
        Assert.False(pendingInvitationResult.Succeeded);
        Assert.False(pendingInvitationResult.AccessDenied);

        string unknownAccountError = Assert.Single(unknownAccountResult.Errors);
        string malformedIdentityError = Assert.Single(malformedIdentityResult.Errors);
        string unconfirmedAccountError = Assert.Single(unconfirmedAccountResult.Errors);
        string activeMemberError = Assert.Single(activeMemberResult.Errors);
        string pendingInvitationError = Assert.Single(pendingInvitationResult.Errors);

        Assert.Equal(unknownAccountError, malformedIdentityError);
        Assert.Equal(unknownAccountError, unconfirmedAccountError);
        Assert.Equal(unknownAccountError, activeMemberError);
        Assert.Equal(unknownAccountError, pendingInvitationError);
        Assert.Single(await context.Invitations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task InviteMemberAsync_WhenFormerMemberIsInvited_CreatesPendingInvitation()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser formerMember = await CreateActiveUserAsync(userManager, "former@example.test", "FormerMember", "B02");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        DateTimeOffset joinedAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        TeamMembership formerMembership = new(Guid.NewGuid(), teamId, formerMember.Id, playerRoleId, joinedAtUtc);

        context.TeamMemberships.Add(formerMembership);

        await context.SaveChangesAsync();

        formerMembership.Leave(joinedAtUtc.AddDays(1));

        await context.SaveChangesAsync();

        InviteTeamMemberRequest request = new(owner.Id, teamId, "FormerMember#B02", playerRoleId);

        InviteTeamMemberResult result = await teamService.InviteMemberAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.NotNull(result.InvitationId);

        Invitation invitation = await context.Invitations.AsNoTracking().SingleAsync();

        Assert.Equal(formerMember.Id, invitation.RecipientUserId);
        Assert.Equal(RequestStatus.Pending, invitation.Status);
        Assert.False(await context.TeamMemberships.AsNoTracking().AnyAsync(membership => membership.TeamId == teamId && membership.UserId == formerMember.Id && membership.Status == MembershipStatus.Active));
    }

    [Fact]
    public async Task InviteMemberAsync_WhenSenderReachedHourlyLimitAcrossTeams_ReturnsRateLimitFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser recipient = await CreateActiveUserAsync(userManager, "recipient@example.test", "Recipient", "B02");

        CreateTeamResult firstTeamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));
        CreateTeamResult secondTeamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Neon Academy", "NEO", "Europe/Paris"));

        Assert.True(firstTeamResult.Succeeded);
        Assert.NotNull(firstTeamResult.TeamId);
        Assert.True(secondTeamResult.Succeeded);
        Assert.NotNull(secondTeamResult.TeamId);

        Guid firstTeamId = firstTeamResult.TeamId.Value;
        Guid secondTeamId = secondTeamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
        DateTimeOffset cancelledAtUtc = createdAtUtc.AddMinutes(1);

        for (int index = 0; index < 30; index++)
        {
            Guid invitationTeamId = index % 2 == 0 ? firstTeamId : secondTeamId;
            Invitation invitation = new(Guid.NewGuid(), invitationTeamId, owner.Id, recipient.Id, playerRoleId, createdAtUtc);

            invitation.Cancel(cancelledAtUtc);
            context.Invitations.Add(invitation);
        }

        await context.SaveChangesAsync();

        InviteTeamMemberRequest request = new(owner.Id, firstTeamId, "Recipient#B02", playerRoleId);

        InviteTeamMemberResult result = await teamService.InviteMemberAsync(request);

        Assert.False(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Null(result.InvitationId);
        Assert.Equal("Vous avez atteint la limite de 30 invitations par heure. Veuillez réessayer plus tard.", Assert.Single(result.Errors));
        Assert.Equal(30, await context.Invitations.AsNoTracking().CountAsync());
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

    private static async Task<ApplicationUser> CreateActiveUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag)
    {
        ApplicationUser user = await CreateUserAsync(userManager, email, pseudo, tag);

        user.MarkAsConfirmed(DateTimeOffset.UtcNow);

        IdentityResult result = await userManager.UpdateAsync(user);

        Assert.True(result.Succeeded, string.Join(" | ", result.Errors.Select(error => error.Description)));

        return user;
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