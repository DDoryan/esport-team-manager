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
using EsportTeamManager.Application.Images;

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
        Assert.False(details.CurrentUserCanLeaveTeam);
        Assert.Equal(3, details.AvailableMemberRoles.Count);
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
        Assert.True(ownerSummary.CanChangeRole);
        Assert.False(ownerSummary.CanRemove);

        Assert.Equal("Member", memberSummary.Pseudo);
        Assert.Equal("B02", memberSummary.Tag);
        Assert.Equal("Manager", memberSummary.RoleLabel);
        Assert.False(memberSummary.IsOwner);
        Assert.Equal(joinedAtUtc, memberSummary.JoinedAtUtc);
        Assert.Equal(managerRoleId, memberSummary.TeamRoleId);
        Assert.True(memberSummary.CanChangeRole);
        Assert.True(memberSummary.CanRemove);
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
    public async Task InviteMemberAsync_WhenOwnerInvitesActiveAccount_CreatesPendingInvitationNotificationAndTrace()
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
        Notification notification = await context.Notifications.AsNoTracking().SingleAsync(item => item.InvitationId == result.InvitationId);
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_INVITATION_CREATED");

        Assert.Equal(teamId, invitation.TeamId);
        Assert.Equal(owner.Id, invitation.SenderUserId);
        Assert.Equal(recipient.Id, invitation.RecipientUserId);
        Assert.Equal(coachRoleId, invitation.ProposedTeamRoleId);
        Assert.Equal(RequestStatus.Pending, invitation.Status);
        Assert.Null(invitation.CreatedMembershipId);
        Assert.Null(invitation.ResolvedAtUtc);
        Assert.InRange(invitation.CreatedAtUtc, beforeInvitationUtc, afterInvitationUtc);

        Assert.Equal(recipient.Id, notification.RecipientUserId);
        Assert.Equal(invitation.InvitationId, notification.InvitationId);
        Assert.Null(notification.OwnershipTransferId);
        Assert.Equal(invitation.CreatedAtUtc, notification.CreatedAtUtc);
        Assert.Null(notification.ReadAtUtc);

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

        Assert.True(details.CurrentUserCanLeaveTeam);
        Assert.Equal(2, details.AvailableMemberRoles.Count);

        TeamMemberSummary ownerSummary = details.Members.Single(member => member.IsOwner);
        TeamMemberSummary managerSummary = details.Members.Single(member => member.TeamMembershipId == managerMembership.TeamMembershipId);

        Assert.False(ownerSummary.CanChangeRole);
        Assert.False(ownerSummary.CanRemove);
        Assert.False(managerSummary.CanChangeRole);
        Assert.False(managerSummary.CanRemove);
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
        Assert.True(details.CurrentUserCanLeaveTeam);
        Assert.Empty(details.AvailableMemberRoles);
        Assert.All(details.Members, member =>
        {
            Assert.False(member.CanChangeRole);
            Assert.False(member.CanRemove);
        });

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

    [Fact]
    public async Task InitiateOwnershipTransferAsync_WhenOwnerSelectsActiveMember_CreatesTransferNotificationAndTrace()
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
        TeamMembership ownerMembership = await context.TeamMemberships.SingleAsync(membership => membership.TeamId == teamId && membership.UserId == owner.Id);
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, managerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(recipientMembership);

        await context.SaveChangesAsync();

        DateTimeOffset beforeInitiationUtc = DateTimeOffset.UtcNow;
        InitiateOwnershipTransferRequest request = new(owner.Id, teamId, recipientMembership.TeamMembershipId);

        OwnershipTransferActionResult result = await teamService.InitiateOwnershipTransferAsync(request);

        DateTimeOffset afterInitiationUtc = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.NotNull(result.OwnershipTransferId);
        Assert.Empty(result.Errors);

        OwnershipTransfer transfer = await context.OwnershipTransfers.AsNoTracking().SingleAsync();
        Notification notification = await context.Notifications.AsNoTracking().SingleAsync();
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_OWNERSHIP_TRANSFER_INITIATED");

        Assert.Equal(result.OwnershipTransferId, transfer.OwnershipTransferId);
        Assert.Equal(teamId, transfer.TeamId);
        Assert.Equal(ownerMembership.TeamMembershipId, transfer.InitiatorMembershipId);
        Assert.Equal(recipientMembership.TeamMembershipId, transfer.RecipientMembershipId);
        Assert.Equal(RequestStatus.Pending, transfer.Status);
        Assert.Null(transfer.ResolvedAtUtc);
        Assert.InRange(transfer.CreatedAtUtc, beforeInitiationUtc, afterInitiationUtc);

        Assert.Equal(recipient.Id, notification.RecipientUserId);
        Assert.Null(notification.InvitationId);
        Assert.Equal(transfer.OwnershipTransferId, notification.OwnershipTransferId);
        Assert.Null(notification.ReadAtUtc);

        Assert.Equal(owner.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(OwnershipTransfer), trace.ObjectType);
        Assert.Equal(transfer.OwnershipTransferId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
    }

    [Fact]
    public async Task InitiateOwnershipTransferAsync_WhenActorIsNotOwner_ReturnsDenied()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser actor = await CreateActiveUserAsync(userManager, "actor@example.test", "Actor", "B02");
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
        TeamMembership actorMembership = new(Guid.NewGuid(), teamId, actor.Id, managerRoleId, DateTimeOffset.UtcNow);
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.AddRange(actorMembership, recipientMembership);

        await context.SaveChangesAsync();

        InitiateOwnershipTransferRequest request = new(actor.Id, teamId, recipientMembership.TeamMembershipId);

        OwnershipTransferActionResult result = await teamService.InitiateOwnershipTransferAsync(request);

        Assert.False(result.Succeeded);
        Assert.True(result.AccessDenied);
        Assert.Null(result.OwnershipTransferId);
        Assert.Empty(result.Errors);
        Assert.Empty(await context.OwnershipTransfers.AsNoTracking().ToListAsync());
        Assert.Empty(await context.Notifications.AsNoTracking().ToListAsync());
        Assert.False(await context.ActionTraces.AsNoTracking().AnyAsync(trace => trace.ActionCode == "TEAM_OWNERSHIP_TRANSFER_INITIATED"));
    }

    [Fact]
    public async Task InitiateOwnershipTransferAsync_WhenTransferIsAlreadyPending_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser firstRecipient = await CreateActiveUserAsync(userManager, "first@example.test", "FirstRecipient", "B02");
        ApplicationUser secondRecipient = await CreateActiveUserAsync(userManager, "second@example.test", "SecondRecipient", "C03");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership firstMembership = new(Guid.NewGuid(), teamId, firstRecipient.Id, playerRoleId, DateTimeOffset.UtcNow);
        TeamMembership secondMembership = new(Guid.NewGuid(), teamId, secondRecipient.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.AddRange(firstMembership, secondMembership);

        await context.SaveChangesAsync();

        OwnershipTransferActionResult firstResult = await teamService.InitiateOwnershipTransferAsync(new InitiateOwnershipTransferRequest(owner.Id, teamId, firstMembership.TeamMembershipId));
        OwnershipTransferActionResult secondResult = await teamService.InitiateOwnershipTransferAsync(new InitiateOwnershipTransferRequest(owner.Id, teamId, secondMembership.TeamMembershipId));

        Assert.True(firstResult.Succeeded);
        Assert.False(secondResult.Succeeded);
        Assert.False(secondResult.AccessDenied);
        Assert.Null(secondResult.OwnershipTransferId);
        Assert.Equal("Un transfert de propriété est déjà en attente pour cette équipe.", Assert.Single(secondResult.Errors));
        Assert.Single(await context.OwnershipTransfers.AsNoTracking().ToListAsync());
        Assert.Single(await context.Notifications.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AcceptOwnershipTransferAsync_WhenRecipientIsActive_TransfersOwnershipAndKeepsFunctionalRoles()
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
        TeamMembership ownerMembership = await context.TeamMemberships.SingleAsync(membership => membership.TeamId == teamId && membership.UserId == owner.Id);
        int ownerRoleId = ownerMembership.TeamRoleId;
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, managerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(recipientMembership);

        await context.SaveChangesAsync();

        OwnershipTransferActionResult initiationResult = await teamService.InitiateOwnershipTransferAsync(new InitiateOwnershipTransferRequest(owner.Id, teamId, recipientMembership.TeamMembershipId));

        Assert.True(initiationResult.Succeeded);
        Assert.NotNull(initiationResult.OwnershipTransferId);

        ResolveOwnershipTransferRequest request = new(recipient.Id, teamId, initiationResult.OwnershipTransferId.Value);

        OwnershipTransferActionResult result = await teamService.AcceptOwnershipTransferAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Equal(initiationResult.OwnershipTransferId, result.OwnershipTransferId);
        Assert.Empty(result.Errors);

        Team team = await context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == teamId);
        OwnershipTransfer transfer = await context.OwnershipTransfers.AsNoTracking().SingleAsync();
        TeamMembership unchangedOwnerMembership = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == ownerMembership.TeamMembershipId);
        TeamMembership unchangedRecipientMembership = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == recipientMembership.TeamMembershipId);
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_OWNERSHIP_TRANSFER_ACCEPTED");

        Assert.Equal(recipient.Id, team.OwnerUserId);
        Assert.Equal(RequestStatus.Accepted, transfer.Status);
        Assert.NotNull(transfer.ResolvedAtUtc);
        Assert.Equal(ownerRoleId, unchangedOwnerMembership.TeamRoleId);
        Assert.Equal(managerRoleId, unchangedRecipientMembership.TeamRoleId);
        Assert.Equal(MembershipStatus.Active, unchangedOwnerMembership.Status);
        Assert.Equal(MembershipStatus.Active, unchangedRecipientMembership.Status);

        Assert.Equal(recipient.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(OwnershipTransfer), trace.ObjectType);
        Assert.Equal(transfer.OwnershipTransferId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
    }

    [Fact]
    public async Task AcceptOwnershipTransferAsync_WhenRecipientMembershipIsInactive_ReturnsFailure()
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
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, playerRoleId, DateTimeOffset.UtcNow.AddDays(-1));

        context.TeamMemberships.Add(recipientMembership);

        await context.SaveChangesAsync();

        OwnershipTransferActionResult initiationResult = await teamService.InitiateOwnershipTransferAsync(new InitiateOwnershipTransferRequest(owner.Id, teamId, recipientMembership.TeamMembershipId));

        Assert.True(initiationResult.Succeeded);
        Assert.NotNull(initiationResult.OwnershipTransferId);

        recipientMembership.Leave(DateTimeOffset.UtcNow);

        await context.SaveChangesAsync();

        ResolveOwnershipTransferRequest request = new(recipient.Id, teamId, initiationResult.OwnershipTransferId.Value);

        OwnershipTransferActionResult result = await teamService.AcceptOwnershipTransferAsync(request);

        Assert.False(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Null(result.OwnershipTransferId);
        Assert.Equal("Ce transfert de propriété ne peut plus être accepté.", Assert.Single(result.Errors));

        Team team = await context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == teamId);
        OwnershipTransfer transfer = await context.OwnershipTransfers.AsNoTracking().SingleAsync();

        Assert.Equal(owner.Id, team.OwnerUserId);
        Assert.Equal(RequestStatus.Pending, transfer.Status);
        Assert.Null(transfer.ResolvedAtUtc);
        Assert.False(await context.ActionTraces.AsNoTracking().AnyAsync(trace => trace.ActionCode == "TEAM_OWNERSHIP_TRANSFER_ACCEPTED"));
    }

    [Fact]
    public async Task RefuseOwnershipTransferAsync_WhenRecipientRefuses_KeepsCurrentOwner()
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
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(recipientMembership);

        await context.SaveChangesAsync();

        OwnershipTransferActionResult initiationResult = await teamService.InitiateOwnershipTransferAsync(new InitiateOwnershipTransferRequest(owner.Id, teamId, recipientMembership.TeamMembershipId));

        Assert.True(initiationResult.Succeeded);
        Assert.NotNull(initiationResult.OwnershipTransferId);

        OwnershipTransferActionResult result = await teamService.RefuseOwnershipTransferAsync(new ResolveOwnershipTransferRequest(recipient.Id, teamId, initiationResult.OwnershipTransferId.Value));

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Equal(initiationResult.OwnershipTransferId, result.OwnershipTransferId);
        Assert.Empty(result.Errors);

        Team team = await context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == teamId);
        OwnershipTransfer transfer = await context.OwnershipTransfers.AsNoTracking().SingleAsync();
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_OWNERSHIP_TRANSFER_REFUSED");

        Assert.Equal(owner.Id, team.OwnerUserId);
        Assert.Equal(RequestStatus.Refused, transfer.Status);
        Assert.NotNull(transfer.ResolvedAtUtc);
        Assert.Equal(recipient.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(OwnershipTransfer), trace.ObjectType);
        Assert.Equal(transfer.OwnershipTransferId.ToString(), trace.ObjectIdentifier);
    }

    [Fact]
    public async Task CancelOwnershipTransferAsync_WhenOwnerCancels_KeepsCurrentOwner()
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
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(recipientMembership);

        await context.SaveChangesAsync();

        OwnershipTransferActionResult initiationResult = await teamService.InitiateOwnershipTransferAsync(new InitiateOwnershipTransferRequest(owner.Id, teamId, recipientMembership.TeamMembershipId));

        Assert.True(initiationResult.Succeeded);
        Assert.NotNull(initiationResult.OwnershipTransferId);

        OwnershipTransferActionResult result = await teamService.CancelOwnershipTransferAsync(new ResolveOwnershipTransferRequest(owner.Id, teamId, initiationResult.OwnershipTransferId.Value));

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Equal(initiationResult.OwnershipTransferId, result.OwnershipTransferId);
        Assert.Empty(result.Errors);

        Team team = await context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == teamId);
        OwnershipTransfer transfer = await context.OwnershipTransfers.AsNoTracking().SingleAsync();
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_OWNERSHIP_TRANSFER_CANCELLED");

        Assert.Equal(owner.Id, team.OwnerUserId);
        Assert.Equal(RequestStatus.Cancelled, transfer.Status);
        Assert.NotNull(transfer.ResolvedAtUtc);
        Assert.Equal(owner.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(OwnershipTransfer), trace.ObjectType);
        Assert.Equal(transfer.OwnershipTransferId.ToString(), trace.ObjectIdentifier);
    }

    [Fact]
    public async Task ResolveOwnershipTransferAsync_WhenActorIsUnauthorized_ReturnsDenied()
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
        ApplicationUser otherUser = await CreateActiveUserAsync(userManager, "other@example.test", "OtherUser", "C03");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(recipientMembership);

        await context.SaveChangesAsync();

        OwnershipTransferActionResult initiationResult = await teamService.InitiateOwnershipTransferAsync(new InitiateOwnershipTransferRequest(owner.Id, teamId, recipientMembership.TeamMembershipId));

        Assert.True(initiationResult.Succeeded);
        Assert.NotNull(initiationResult.OwnershipTransferId);

        Guid transferId = initiationResult.OwnershipTransferId.Value;
        OwnershipTransferActionResult unauthorizedAcceptance = await teamService.AcceptOwnershipTransferAsync(new ResolveOwnershipTransferRequest(otherUser.Id, teamId, transferId));
        OwnershipTransferActionResult unauthorizedCancellation = await teamService.CancelOwnershipTransferAsync(new ResolveOwnershipTransferRequest(recipient.Id, teamId, transferId));

        Assert.False(unauthorizedAcceptance.Succeeded);
        Assert.True(unauthorizedAcceptance.AccessDenied);
        Assert.Empty(unauthorizedAcceptance.Errors);
        Assert.False(unauthorizedCancellation.Succeeded);
        Assert.True(unauthorizedCancellation.AccessDenied);
        Assert.Empty(unauthorizedCancellation.Errors);

        OwnershipTransfer transfer = await context.OwnershipTransfers.AsNoTracking().SingleAsync();
        Team team = await context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == teamId);

        Assert.Equal(RequestStatus.Pending, transfer.Status);
        Assert.Null(transfer.ResolvedAtUtc);
        Assert.Equal(owner.Id, team.OwnerUserId);
        Assert.False(await context.ActionTraces.AsNoTracking().AnyAsync(trace => trace.ActionCode == "TEAM_OWNERSHIP_TRANSFER_ACCEPTED" || trace.ActionCode == "TEAM_OWNERSHIP_TRANSFER_CANCELLED"));
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenOwnerChangesOwnRole_KeepsTeamOwnership()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        TeamMembership ownerMembership = await context.TeamMemberships.SingleAsync(membership => membership.TeamId == teamId && membership.UserId == owner.Id);
        int coachRoleId = await context.TeamRoles
            .Where(role => role.Code == "Coach")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        ChangeTeamMemberRoleRequest request = new(owner.Id, teamId, ownerMembership.TeamMembershipId, coachRoleId);

        TeamMembershipActionResult result = await teamService.ChangeMemberRoleAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Empty(result.Errors);

        Team team = await context.Teams.AsNoTracking().SingleAsync(item => item.TeamId == teamId);
        TeamMembership updatedMembership = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == ownerMembership.TeamMembershipId);
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_MEMBER_ROLE_CHANGED");

        Assert.Equal(owner.Id, team.OwnerUserId);
        Assert.Equal(coachRoleId, updatedMembership.TeamRoleId);
        Assert.Equal(owner.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(TeamMembership), trace.ObjectType);
        Assert.Equal(ownerMembership.TeamMembershipId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenManagerChangesPlayerToCoach_ChangesRole()
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
        ApplicationUser player = await CreateActiveUserAsync(userManager, "player@example.test", "PlayerUser", "C03");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        int coachRoleId = await context.TeamRoles
            .Where(role => role.Code == "Coach")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership managerMembership = new(Guid.NewGuid(), teamId, manager.Id, managerRoleId, DateTimeOffset.UtcNow);
        TeamMembership playerMembership = new(Guid.NewGuid(), teamId, player.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.AddRange(managerMembership, playerMembership);

        await context.SaveChangesAsync();

        ChangeTeamMemberRoleRequest request = new(manager.Id, teamId, playerMembership.TeamMembershipId, coachRoleId);

        TeamMembershipActionResult result = await teamService.ChangeMemberRoleAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Empty(result.Errors);

        TeamMembership updatedMembership = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == playerMembership.TeamMembershipId);
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_MEMBER_ROLE_CHANGED");

        Assert.Equal(coachRoleId, updatedMembership.TeamRoleId);
        Assert.Equal(manager.Id, trace.ActorUserId);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenManagerTargetsProtectedMemberOrRole_ReturnsDenied()
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
        ApplicationUser otherManager = await CreateActiveUserAsync(userManager, "other-manager@example.test", "OtherManager", "C03");
        ApplicationUser player = await CreateActiveUserAsync(userManager, "player@example.test", "PlayerUser", "D04");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        int coachRoleId = await context.TeamRoles
            .Where(role => role.Code == "Coach")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership ownerMembership = await context.TeamMemberships.SingleAsync(membership => membership.TeamId == teamId && membership.UserId == owner.Id);
        TeamMembership managerMembership = new(Guid.NewGuid(), teamId, manager.Id, managerRoleId, DateTimeOffset.UtcNow);
        TeamMembership otherManagerMembership = new(Guid.NewGuid(), teamId, otherManager.Id, managerRoleId, DateTimeOffset.UtcNow);
        TeamMembership playerMembership = new(Guid.NewGuid(), teamId, player.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.AddRange(managerMembership, otherManagerMembership, playerMembership);

        await context.SaveChangesAsync();

        TeamMembershipActionResult ownerResult = await teamService.ChangeMemberRoleAsync(new ChangeTeamMemberRoleRequest(manager.Id, teamId, ownerMembership.TeamMembershipId, coachRoleId));
        TeamMembershipActionResult managerResult = await teamService.ChangeMemberRoleAsync(new ChangeTeamMemberRoleRequest(manager.Id, teamId, otherManagerMembership.TeamMembershipId, coachRoleId));
        TeamMembershipActionResult promotionResult = await teamService.ChangeMemberRoleAsync(new ChangeTeamMemberRoleRequest(manager.Id, teamId, playerMembership.TeamMembershipId, managerRoleId));

        Assert.False(ownerResult.Succeeded);
        Assert.True(ownerResult.AccessDenied);
        Assert.False(managerResult.Succeeded);
        Assert.True(managerResult.AccessDenied);
        Assert.False(promotionResult.Succeeded);
        Assert.True(promotionResult.AccessDenied);
        Assert.False(await context.ActionTraces.AsNoTracking().AnyAsync(trace => trace.ActionCode == "TEAM_MEMBER_ROLE_CHANGED"));

        TeamMembership unchangedOwner = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == ownerMembership.TeamMembershipId);
        TeamMembership unchangedManager = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == otherManagerMembership.TeamMembershipId);
        TeamMembership unchangedPlayer = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == playerMembership.TeamMembershipId);

        Assert.Equal(playerRoleId, unchangedOwner.TeamRoleId);
        Assert.Equal(managerRoleId, unchangedManager.TeamRoleId);
        Assert.Equal(playerRoleId, unchangedPlayer.TeamRoleId);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenOwnerRemovesMember_ClosesMembershipAndCreatesTrace()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser member = await CreateActiveUserAsync(userManager, "member@example.test", "Member", "B02");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership membership = new(Guid.NewGuid(), teamId, member.Id, playerRoleId, DateTimeOffset.UtcNow.AddDays(-1));

        context.TeamMemberships.Add(membership);

        await context.SaveChangesAsync();

        DateTimeOffset beforeRemovalUtc = DateTimeOffset.UtcNow;
        RemoveTeamMemberRequest request = new(owner.Id, teamId, membership.TeamMembershipId);

        TeamMembershipActionResult result = await teamService.RemoveMemberAsync(request);

        DateTimeOffset afterRemovalUtc = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Empty(result.Errors);

        TeamMembership closedMembership = await context.TeamMemberships.AsNoTracking().SingleAsync(item => item.TeamMembershipId == membership.TeamMembershipId);
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_MEMBER_REMOVED");

        Assert.Equal(MembershipStatus.Removed, closedMembership.Status);
        Assert.Equal(member.Id, closedMembership.UserId);
        Assert.Null(closedMembership.FormerMemberId);
        Assert.NotNull(closedMembership.LeftAtUtc);
        Assert.InRange(closedMembership.LeftAtUtc.Value, beforeRemovalUtc, afterRemovalUtc);
        Assert.Equal(owner.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(TeamMembership), trace.ObjectType);
        Assert.Equal(membership.TeamMembershipId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
        Assert.Null(await teamService.GetManagementDetailsAsync(member.Id, teamId));
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenManagerAttemptsRemovalOrOwnerTargetsSelf_ReturnsDenied()
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
        ApplicationUser player = await CreateActiveUserAsync(userManager, "player@example.test", "PlayerUser", "C03");

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
        TeamMembership ownerMembership = await context.TeamMemberships.SingleAsync(membership => membership.TeamId == teamId && membership.UserId == owner.Id);
        TeamMembership managerMembership = new(Guid.NewGuid(), teamId, manager.Id, managerRoleId, DateTimeOffset.UtcNow);
        TeamMembership playerMembership = new(Guid.NewGuid(), teamId, player.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.AddRange(managerMembership, playerMembership);

        await context.SaveChangesAsync();

        TeamMembershipActionResult managerResult = await teamService.RemoveMemberAsync(new RemoveTeamMemberRequest(manager.Id, teamId, playerMembership.TeamMembershipId));
        TeamMembershipActionResult ownerSelfResult = await teamService.RemoveMemberAsync(new RemoveTeamMemberRequest(owner.Id, teamId, ownerMembership.TeamMembershipId));

        Assert.False(managerResult.Succeeded);
        Assert.True(managerResult.AccessDenied);
        Assert.False(ownerSelfResult.Succeeded);
        Assert.True(ownerSelfResult.AccessDenied);
        Assert.False(await context.ActionTraces.AsNoTracking().AnyAsync(trace => trace.ActionCode == "TEAM_MEMBER_REMOVED"));

        TeamMembership unchangedPlayer = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == playerMembership.TeamMembershipId);
        TeamMembership unchangedOwner = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == ownerMembership.TeamMembershipId);

        Assert.Equal(MembershipStatus.Active, unchangedPlayer.Status);
        Assert.Null(unchangedPlayer.LeftAtUtc);
        Assert.Equal(MembershipStatus.Active, unchangedOwner.Status);
        Assert.Null(unchangedOwner.LeftAtUtc);
    }

    [Fact]
    public async Task LeaveTeamAsync_WhenMemberLeaves_ClosesMembershipAndRemovesAccess()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser member = await CreateActiveUserAsync(userManager, "member@example.test", "Member", "B02");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership membership = new(Guid.NewGuid(), teamId, member.Id, playerRoleId, DateTimeOffset.UtcNow.AddDays(-1));

        context.TeamMemberships.Add(membership);

        await context.SaveChangesAsync();

        DateTimeOffset beforeDepartureUtc = DateTimeOffset.UtcNow;
        LeaveTeamRequest request = new(member.Id, teamId);

        TeamMembershipActionResult result = await teamService.LeaveTeamAsync(request);

        DateTimeOffset afterDepartureUtc = DateTimeOffset.UtcNow;

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Empty(result.Errors);

        TeamMembership closedMembership = await context.TeamMemberships.AsNoTracking().SingleAsync(item => item.TeamMembershipId == membership.TeamMembershipId);
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_MEMBER_LEFT");

        Assert.Equal(MembershipStatus.Left, closedMembership.Status);
        Assert.Equal(member.Id, closedMembership.UserId);
        Assert.NotNull(closedMembership.LeftAtUtc);
        Assert.InRange(closedMembership.LeftAtUtc.Value, beforeDepartureUtc, afterDepartureUtc);
        Assert.Equal(member.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(TeamMembership), trace.ObjectType);
        Assert.Equal(membership.TeamMembershipId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
        Assert.Empty(await teamService.GetTeamsForUserAsync(member.Id));
        Assert.Null(await teamService.GetManagementDetailsAsync(member.Id, teamId));
    }

    [Fact]
    public async Task LeaveTeamAsync_WhenOwnerAttemptsDeparture_ReturnsFailureAndKeepsMembershipActive()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        TeamMembership ownerMembership = await context.TeamMemberships.SingleAsync(membership => membership.TeamId == teamId && membership.UserId == owner.Id);
        LeaveTeamRequest request = new(owner.Id, teamId);

        TeamMembershipActionResult result = await teamService.LeaveTeamAsync(request);

        Assert.False(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Single(result.Errors);
        Assert.False(await context.ActionTraces.AsNoTracking().AnyAsync(trace => trace.ActionCode == "TEAM_MEMBER_LEFT"));

        TeamMembership unchangedMembership = await context.TeamMemberships.AsNoTracking().SingleAsync(membership => membership.TeamMembershipId == ownerMembership.TeamMembershipId);

        Assert.Equal(MembershipStatus.Active, unchangedMembership.Status);
        Assert.Null(unchangedMembership.LeftAtUtc);
    }

    [Fact]
    public async Task GetManagementDetailsAsync_WhenMembersJoinedAtDifferentTimes_ReturnsChronologicalOrder()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser firstMember = await CreateActiveUserAsync(userManager, "first@example.test", "ZuluMember", "B02");
        ApplicationUser secondMember = await CreateActiveUserAsync(userManager, "second@example.test", "AlphaMember", "C03");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        DateTimeOffset firstJoinedAtUtc = DateTimeOffset.UtcNow;
        DateTimeOffset secondJoinedAtUtc = firstJoinedAtUtc.AddSeconds(1);
        TeamMembership firstMembership = new(Guid.NewGuid(), teamId, firstMember.Id, playerRoleId, firstJoinedAtUtc);
        TeamMembership secondMembership = new(Guid.NewGuid(), teamId, secondMember.Id, playerRoleId, secondJoinedAtUtc);

        context.TeamMemberships.AddRange(firstMembership, secondMembership);

        await context.SaveChangesAsync();

        TeamManagementDetails? details = await teamService.GetManagementDetailsAsync(owner.Id, teamId);

        Assert.NotNull(details);
        Assert.Equal(["Owner", "ZuluMember", "AlphaMember"], details.Members.Select(member => member.Pseudo).ToArray());
    }

    [Fact]
    public async Task GetManagementDetailsAsync_WhenOwnershipTransferIsPending_ReturnsTransferOnlyForOwner()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser recipient = await CreateUserAsync(userManager, "recipient@example.test", "Recipient", "B02");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, managerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(recipientMembership);

        await context.SaveChangesAsync();

        OwnershipTransferActionResult transferResult = await teamService.InitiateOwnershipTransferAsync(new InitiateOwnershipTransferRequest(owner.Id, teamId, recipientMembership.TeamMembershipId));

        Assert.True(transferResult.Succeeded);
        Assert.NotNull(transferResult.OwnershipTransferId);

        TeamManagementDetails? ownerDetails = await teamService.GetManagementDetailsAsync(owner.Id, teamId);
        TeamManagementDetails? recipientDetails = await teamService.GetManagementDetailsAsync(recipient.Id, teamId);

        Assert.NotNull(ownerDetails);
        Assert.NotNull(ownerDetails.PendingOwnershipTransfer);
        Assert.Equal(transferResult.OwnershipTransferId, ownerDetails.PendingOwnershipTransfer.OwnershipTransferId);
        Assert.Equal(recipientMembership.TeamMembershipId, ownerDetails.PendingOwnershipTransfer.RecipientMembershipId);
        Assert.Equal("Recipient", ownerDetails.PendingOwnershipTransfer.RecipientPseudo);
        Assert.Equal("B02", ownerDetails.PendingOwnershipTransfer.RecipientTag);

        Assert.NotNull(recipientDetails);
        Assert.Null(recipientDetails.PendingOwnershipTransfer);
    }

    [Fact]
    public async Task UpdateInformationAsync_WhenOwnerProvidesValidInformation_UpdatesTeamAndCreatesTrace()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamResult creationResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(creationResult.Succeeded);
        Assert.NotNull(creationResult.TeamId);

        Guid teamId = creationResult.TeamId.Value;
        UpdateTeamInformationRequest request = new(owner.Id, teamId, "  Phoenix Elite  ", "  PHE  ", "  Équipe principale.  ", "Europe/London");

        UpdateTeamInformationResult result = await teamService.UpdateInformationAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Empty(result.Errors);

        Team updatedTeam = await context.Teams.AsNoTracking().SingleAsync(team => team.TeamId == teamId);
        ActionTrace trace = await context.ActionTraces.AsNoTracking().SingleAsync(item => item.ActionCode == "TEAM_INFORMATION_UPDATED");

        Assert.Equal("Phoenix Elite", updatedTeam.Name);
        Assert.Equal("PHE", updatedTeam.Tag);
        Assert.Equal("Équipe principale.", updatedTeam.Description);
        Assert.Equal("Europe/London", updatedTeam.TimeZoneId);
        Assert.Equal(owner.Id, trace.ActorUserId);
        Assert.Equal(teamId, trace.TeamId);
        Assert.Equal(nameof(Team), trace.ObjectType);
        Assert.Equal(teamId.ToString(), trace.ObjectIdentifier);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
    }

    [Fact]
    public async Task UpdateInformationAsync_WhenActiveMemberIsNotOwner_ReturnsDeniedAndKeepsTeamUnchanged()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser member = await CreateActiveUserAsync(userManager, "member@example.test", "Member", "B02");

        CreateTeamResult creationResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(creationResult.Succeeded);
        Assert.NotNull(creationResult.TeamId);

        Guid teamId = creationResult.TeamId.Value;
        int playerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership membership = new(Guid.NewGuid(), teamId, member.Id, playerRoleId, DateTimeOffset.UtcNow);

        context.TeamMemberships.Add(membership);

        await context.SaveChangesAsync();

        UpdateTeamInformationRequest request = new(member.Id, teamId, "Unauthorized name", "BAD", "Unauthorized description", "Europe/London");

        UpdateTeamInformationResult result = await teamService.UpdateInformationAsync(request);

        Assert.False(result.Succeeded);
        Assert.True(result.AccessDenied);

        Team unchangedTeam = await context.Teams.AsNoTracking().SingleAsync(team => team.TeamId == teamId);

        Assert.Equal("Phoenix Academy", unchangedTeam.Name);
        Assert.Equal("PHX", unchangedTeam.Tag);
        Assert.Null(unchangedTeam.Description);
        Assert.Equal("Europe/Paris", unchangedTeam.TimeZoneId);
        Assert.Empty(await context.ActionTraces.AsNoTracking().Where(trace => trace.ActionCode == "TEAM_INFORMATION_UPDATED").ToListAsync());
    }

    [Theory]
    [InlineData("AB", "PHX", "Europe/Paris")]
    [InlineData("Phoenix Academy", "P", "Europe/Paris")]
    [InlineData("Phoenix Academy", "TOOLONG", "Europe/Paris")]
    [InlineData("Phoenix Academy", "PHX", "Invalid/Zone")]
    public async Task UpdateInformationAsync_WhenInformationIsInvalid_ReturnsFailureAndKeepsTeamUnchanged(string name, string? tag, string timeZoneId)
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamResult creationResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(creationResult.Succeeded);
        Assert.NotNull(creationResult.TeamId);

        Guid teamId = creationResult.TeamId.Value;
        UpdateTeamInformationRequest request = new(owner.Id, teamId, name, tag, "Description valide", timeZoneId);

        UpdateTeamInformationResult result = await teamService.UpdateInformationAsync(request);

        Assert.False(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.NotEmpty(result.Errors);

        Team unchangedTeam = await context.Teams.AsNoTracking().SingleAsync(team => team.TeamId == teamId);

        Assert.Equal("Phoenix Academy", unchangedTeam.Name);
        Assert.Equal("PHX", unchangedTeam.Tag);
        Assert.Null(unchangedTeam.Description);
        Assert.Equal("Europe/Paris", unchangedTeam.TimeZoneId);
        Assert.Empty(await context.ActionTraces.AsNoTracking().Where(trace => trace.ActionCode == "TEAM_INFORMATION_UPDATED").ToListAsync());
    }

    [Fact]
    public async Task UpdateInformationAsync_WhenDescriptionExceedsLimit_ReturnsFailure()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamResult creationResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(creationResult.Succeeded);
        Assert.NotNull(creationResult.TeamId);

        Guid teamId = creationResult.TeamId.Value;
        UpdateTeamInformationRequest request = new(owner.Id, teamId, "Phoenix Academy", "PHX", new string('A', 501), "Europe/Paris");

        UpdateTeamInformationResult result = await teamService.UpdateInformationAsync(request);

        Assert.False(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.NotEmpty(result.Errors);

        Team unchangedTeam = await context.Teams.AsNoTracking().SingleAsync(team => team.TeamId == teamId);

        Assert.Null(unchangedTeam.Description);
    }

    [Fact]
    public async Task DeleteTeamAsync_WhenOwnerConfirmsName_DeletesCompleteTeamGraphAndPreservesReferencesAndTraces()
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

        CreateTeamResult creationResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(creationResult.Succeeded);
        Assert.NotNull(creationResult.TeamId);

        Guid teamId = creationResult.TeamId.Value;
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        TeamMembership ownerMembership = await context.TeamMemberships.SingleAsync(membership => membership.TeamId == teamId && membership.UserId == owner.Id);
        TeamRole customRole = new(100, "Analyst", "Analyste", false, teamId);
        ActivityType customActivityType = new(100, "Coaching", "Coaching", false, teamId);
        TeamMembership memberMembership = new(Guid.NewGuid(), teamId, member.Id, customRole.TeamRoleId, nowUtc);
        FormerMember formerMember = new(Guid.NewGuid(), teamId, 1, nowUtc);
        Invitation invitation = new(Guid.NewGuid(), teamId, owner.Id, member.Id, customRole.TeamRoleId, nowUtc);
        OwnershipTransfer ownershipTransfer = new(Guid.NewGuid(), teamId, ownerMembership.TeamMembershipId, memberMembership.TeamMembershipId, nowUtc);
        Notification invitationNotification = Notification.CreateForInvitation(Guid.NewGuid(), member.Id, invitation.InvitationId, nowUtc);
        Notification transferNotification = Notification.CreateForOwnershipTransfer(Guid.NewGuid(), member.Id, ownershipTransfer.OwnershipTransferId, nowUtc);
        Strategy strategy = new(teamId, ownerMembership.TeamMembershipId, 1, "Exécution site A", StrategySide.Attack, "Description de la stratégie.", null, nowUtc);
        Guid activityId = Guid.NewGuid();
        TeamActivity activity = new(activityId, teamId, customActivityType, ownerMembership.TeamMembershipId, nowUtc.AddDays(1), nowUtc.AddDays(1).AddHours(2), "Europe/Paris", [ownerMembership.TeamMembershipId, memberMembership.TeamMembershipId], nowUtc, "Session collective");
        ActivityLink activityLink = new(activityId, "Compte rendu", "https://example.test/compte-rendu");
        ActivityStrategy activityStrategy = new(activityId, strategy.StrategyId);
        ImageFile teamLogo = ImageFile.CreateTeamLogo(teamId, $"{Guid.NewGuid():N}.webp", "logo.png", "image/webp", 100, 128, 128, $"team-logos/{teamId:N}/logo.webp", $"team-logos/{teamId:N}/logo-thumbnail.webp", nowUtc);
        ImageFile strategyImage = ImageFile.CreateStrategyImage(strategy.StrategyId, $"{Guid.NewGuid():N}.webp", "strategy.png", "image/webp", 100, 128, 128, $"strategy-images/{strategy.StrategyId:N}/strategy.webp", $"strategy-images/{strategy.StrategyId:N}/strategy-thumbnail.webp", nowUtc);

        context.AddRange(customRole, customActivityType, memberMembership, formerMember, invitation, ownershipTransfer, invitationNotification, transferNotification, strategy, activity, activityLink, activityStrategy, teamLogo, strategyImage);

        await context.SaveChangesAsync();

        int mapCountBeforeDeletion = await context.Maps.CountAsync();
        int systemRoleCountBeforeDeletion = await context.TeamRoles.CountAsync(role => role.IsSystem);
        int systemActivityTypeCountBeforeDeletion = await context.ActivityTypes.CountAsync(activityType => activityType.IsSystem);

        context.ChangeTracker.Clear();

        DeleteTeamRequest request = new(owner.Id, teamId, "  phoenix academy  ");

        DeleteTeamResult result = await teamService.DeleteTeamAsync(request);

        Assert.True(result.Succeeded, string.Join(" | ", result.Errors));
        Assert.False(result.AccessDenied);
        Assert.Empty(result.Errors);

        context.ChangeTracker.Clear();

        Assert.False(await context.Teams.AnyAsync(team => team.TeamId == teamId));
        Assert.False(await context.TeamMemberships.AnyAsync(membership => membership.TeamId == teamId));
        Assert.False(await context.FormerMembers.AnyAsync(item => item.TeamId == teamId));
        Assert.False(await context.Invitations.AnyAsync(item => item.TeamId == teamId));
        Assert.False(await context.OwnershipTransfers.AnyAsync(item => item.TeamId == teamId));
        Assert.False(await context.Notifications.AnyAsync(item => item.NotificationId == invitationNotification.NotificationId || item.NotificationId == transferNotification.NotificationId));
        Assert.False(await context.TeamActivities.AnyAsync(item => item.TeamId == teamId));
        Assert.False(await context.ActivityParticipants.AnyAsync(item => item.ActivityId == activityId));
        Assert.False(await context.ActivityLinks.AnyAsync(item => item.ActivityId == activityId));
        Assert.False(await context.ActivityStrategies.AnyAsync(item => item.ActivityId == activityId));
        Assert.False(await context.Strategies.AnyAsync(item => item.TeamId == teamId));
        Assert.False(await context.ImageFiles.AnyAsync(item => item.ImageFileId == teamLogo.ImageFileId || item.ImageFileId == strategyImage.ImageFileId));
        Assert.False(await context.TeamRoles.AnyAsync(role => role.TeamId == teamId));
        Assert.False(await context.ActivityTypes.AnyAsync(activityType => activityType.TeamId == teamId));
        Assert.Equal(mapCountBeforeDeletion, await context.Maps.CountAsync());
        Assert.Equal(systemRoleCountBeforeDeletion, await context.TeamRoles.CountAsync(role => role.IsSystem));
        Assert.Equal(systemActivityTypeCountBeforeDeletion, await context.ActivityTypes.CountAsync(activityType => activityType.IsSystem));

        List<ActionTrace> traces = await context.ActionTraces.AsNoTracking().Where(trace => trace.ObjectType == nameof(Team) && trace.ObjectIdentifier == teamId.ToString()).ToListAsync();
        ActionTrace deletionTrace = Assert.Single(traces, trace => trace.ActionCode == "TEAM_DELETED");

        Assert.True(traces.Count >= 2);
        Assert.All(traces, trace => Assert.Null(trace.TeamId));
        Assert.Equal(owner.Id, deletionTrace.ActorUserId);
        Assert.Equal(TraceOutcome.Succeeded, deletionTrace.Outcome);
    }

    [Fact]
    public async Task DeleteTeamAsync_WhenConfirmationNameDoesNotMatch_ReturnsFailureWithoutDeletingTeam()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");

        CreateTeamResult creationResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(creationResult.Succeeded);
        Assert.NotNull(creationResult.TeamId);

        Guid teamId = creationResult.TeamId.Value;
        DeleteTeamRequest request = new(owner.Id, teamId, "Phoenix");

        DeleteTeamResult result = await teamService.DeleteTeamAsync(request);

        Assert.False(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.NotEmpty(result.Errors);
        Assert.True(await context.Teams.AnyAsync(team => team.TeamId == teamId));
        Assert.False(await context.ActionTraces.AnyAsync(trace => trace.ActionCode == "TEAM_DELETED"));
    }

    [Fact]
    public async Task DeleteTeamAsync_WhenActorIsNotOwner_ReturnsDeniedWithoutDeletingTeam()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = scope.ServiceProvider.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser otherUser = await CreateUserAsync(userManager, "other@example.test", "Other", "B02");

        CreateTeamResult creationResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(creationResult.Succeeded);
        Assert.NotNull(creationResult.TeamId);

        Guid teamId = creationResult.TeamId.Value;
        DeleteTeamRequest request = new(otherUser.Id, teamId, "Phoenix Academy");

        DeleteTeamResult result = await teamService.DeleteTeamAsync(request);

        Assert.False(result.Succeeded);
        Assert.True(result.AccessDenied);
        Assert.Empty(result.Errors);
        Assert.True(await context.Teams.AnyAsync(team => team.TeamId == teamId));
        Assert.False(await context.ActionTraces.AnyAsync(trace => trace.ActionCode == "TEAM_DELETED"));
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
        services.AddScoped<IPrivateImageService>(_ => new StubPrivateImageService());
        services.AddScoped<IUserTeamService, UserTeamService>();

        return services.BuildServiceProvider();
    }

    private sealed class StubPrivateImageService : IPrivateImageService
    {
        public Task<PrivateImageContent?> GetTeamLogoThumbnailAsync(Guid actorUserId, Guid teamId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<PrivateImageContent?>(null);
        }

        public Task<PrivateImageContent?> GetStrategyImageThumbnailAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
        {
            return GetStrategyImageAsync(actorUserId, teamId, strategyId, cancellationToken);
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
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(StorePrivateImageResult.Failure(["Le remplacement du logo n’est pas utilisé par ce test."]));
        }

        public Task<StorePrivateImageResult> StoreStrategyImageAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(StorePrivateImageResult.Failure(["Le stockage d’image n’est pas utilisé par ce test."]));
        }

        public Task<StorePrivateImageResult> StoreTeamLogoAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(StorePrivateImageResult.Failure(["Le stockage du logo n’est pas utilisé par ce test."]));
        }

        public Task DeleteStrategyImageFilesAsync(Guid strategyId, string optimizedStorageKey, string thumbnailStorageKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task<PrivateImageDeletionBatch> StageTeamImageFilesForDeletionAsync(Guid teamId, IReadOnlyCollection<Guid> strategyIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new PrivateImageDeletionBatch(Guid.NewGuid(), teamId, strategyIds));
        }

        public Task RestoreStagedTeamImageFilesAsync(PrivateImageDeletionBatch batch, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task CompleteStagedTeamImageDeletionAsync(PrivateImageDeletionBatch batch, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
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