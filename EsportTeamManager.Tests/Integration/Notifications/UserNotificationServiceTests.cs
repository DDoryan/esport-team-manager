using EsportTeamManager.Application.Notifications;
using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Infrastructure.Notifications;
using EsportTeamManager.Infrastructure.Persistence;
using EsportTeamManager.Infrastructure.Teams;
using EsportTeamManager.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EsportTeamManager.Application.Images;

namespace EsportTeamManager.Tests.Integration.Notifications;

public sealed class UserNotificationServiceTests
{
    [Fact]
    public async Task GetForUserAsync_WhenInvitationExists_ReturnsUnreadInvitation()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        InvitationScenario scenario = await CreateInvitationScenarioAsync(scope.ServiceProvider);
        IUserNotificationService service = scope.ServiceProvider.GetRequiredService<IUserNotificationService>();

        IReadOnlyCollection<UserNotificationSummary> notifications = await service.GetForUserAsync(scenario.Recipient.Id);
        int unreadCount = await service.GetUnreadCountAsync(scenario.Recipient.Id);

        UserNotificationSummary notification = Assert.Single(notifications);

        Assert.Equal(scenario.NotificationId, notification.NotificationId);
        Assert.Equal(UserNotificationKind.Invitation, notification.Kind);
        Assert.Equal(scenario.InvitationId, notification.SourceId);
        Assert.Equal(scenario.TeamId, notification.TeamId);
        Assert.Equal("Phoenix Academy", notification.TeamName);
        Assert.Equal("PHX", notification.TeamTag);
        Assert.Equal("Owner", notification.ActorPseudo);
        Assert.Equal("A01", notification.ActorTag);
        Assert.Equal("Joueur", notification.ProposedRoleLabel);
        Assert.Equal(RequestStatus.Pending, notification.Status);
        Assert.Null(notification.ReadAtUtc);
        Assert.Equal(1, unreadCount);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WhenPendingInvitationHasNoNotification_BackfillsUnreadNotification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        InvitationScenario scenario = await CreateInvitationScenarioAsync(scope.ServiceProvider);
        IUserNotificationService service = scope.ServiceProvider.GetRequiredService<IUserNotificationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Notification originalNotification = await context.Notifications.SingleAsync(notification => notification.NotificationId == scenario.NotificationId);

        context.Notifications.Remove(originalNotification);

        await context.SaveChangesAsync();

        int unreadCount = await service.GetUnreadCountAsync(scenario.Recipient.Id);

        context.ChangeTracker.Clear();

        Notification backfilledNotification = await context.Notifications
            .AsNoTracking()
            .SingleAsync(notification => notification.InvitationId == scenario.InvitationId);

        Assert.Equal(1, unreadCount);
        Assert.NotEqual(scenario.NotificationId, backfilledNotification.NotificationId);
        Assert.Equal(scenario.Recipient.Id, backfilledNotification.RecipientUserId);
        Assert.Equal(scenario.InvitationId, backfilledNotification.InvitationId);
        Assert.Null(backfilledNotification.OwnershipTransferId);
        Assert.Null(backfilledNotification.ReadAtUtc);
    }

    [Fact]
    public async Task GetForUserAsync_WhenResolvedInvitationHasNoNotification_BackfillsReadNotification()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        InvitationScenario scenario = await CreateInvitationScenarioAsync(scope.ServiceProvider);
        IUserNotificationService service = scope.ServiceProvider.GetRequiredService<IUserNotificationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Invitation invitation = await context.Invitations.SingleAsync(item => item.InvitationId == scenario.InvitationId);
        Notification originalNotification = await context.Notifications.SingleAsync(notification => notification.NotificationId == scenario.NotificationId);

        invitation.Refuse(DateTimeOffset.UtcNow);
        context.Notifications.Remove(originalNotification);

        await context.SaveChangesAsync();

        IReadOnlyCollection<UserNotificationSummary> notifications = await service.GetForUserAsync(scenario.Recipient.Id);
        int unreadCount = await service.GetUnreadCountAsync(scenario.Recipient.Id);

        context.ChangeTracker.Clear();

        UserNotificationSummary summary = Assert.Single(notifications);
        Notification backfilledNotification = await context.Notifications
            .AsNoTracking()
            .SingleAsync(notification => notification.InvitationId == scenario.InvitationId);

        Assert.Equal(RequestStatus.Refused, summary.Status);
        Assert.NotNull(summary.ReadAtUtc);
        Assert.Equal(0, unreadCount);
        Assert.Equal(scenario.Recipient.Id, backfilledNotification.RecipientUserId);
        Assert.Equal(scenario.InvitationId, backfilledNotification.InvitationId);
        Assert.NotNull(backfilledNotification.ReadAtUtc);
    }

    [Fact]
    public async Task GetForUserAsync_WhenOwnershipTransferExists_ReturnsUnreadTransfer()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        OwnershipTransferScenario scenario = await CreateOwnershipTransferScenarioAsync(scope.ServiceProvider);
        IUserNotificationService service = scope.ServiceProvider.GetRequiredService<IUserNotificationService>();

        IReadOnlyCollection<UserNotificationSummary> notifications = await service.GetForUserAsync(scenario.Recipient.Id);
        int unreadCount = await service.GetUnreadCountAsync(scenario.Recipient.Id);

        UserNotificationSummary notification = Assert.Single(notifications);

        Assert.Equal(scenario.NotificationId, notification.NotificationId);
        Assert.Equal(UserNotificationKind.OwnershipTransfer, notification.Kind);
        Assert.Equal(scenario.OwnershipTransferId, notification.SourceId);
        Assert.Equal(scenario.TeamId, notification.TeamId);
        Assert.Equal("Phoenix Academy", notification.TeamName);
        Assert.Equal("PHX", notification.TeamTag);
        Assert.Equal("Owner", notification.ActorPseudo);
        Assert.Equal("A01", notification.ActorTag);
        Assert.Null(notification.ProposedRoleLabel);
        Assert.Equal(RequestStatus.Pending, notification.Status);
        Assert.Null(notification.ReadAtUtc);
        Assert.Equal(1, unreadCount);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WhenUnreadNotificationsExist_PersistsReadState()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        InvitationScenario scenario = await CreateInvitationScenarioAsync(scope.ServiceProvider);
        IUserNotificationService service = scope.ServiceProvider.GetRequiredService<IUserNotificationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await service.MarkAllAsReadAsync(scenario.Recipient.Id);

        int unreadCount = await service.GetUnreadCountAsync(scenario.Recipient.Id);

        context.ChangeTracker.Clear();

        Notification storedNotification = await context.Notifications
            .AsNoTracking()
            .SingleAsync(notification => notification.NotificationId == scenario.NotificationId);

        Assert.Equal(0, unreadCount);
        Assert.NotNull(storedNotification.ReadAtUtc);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenInvitationIsPending_CreatesMembershipAndTrace()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        InvitationScenario scenario = await CreateInvitationScenarioAsync(scope.ServiceProvider);
        IUserNotificationService service = scope.ServiceProvider.GetRequiredService<IUserNotificationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        InvitationActionResult result = await service.AcceptInvitationAsync(new ResolveInvitationRequest(scenario.Recipient.Id, scenario.InvitationId));

        context.ChangeTracker.Clear();

        Invitation storedInvitation = await context.Invitations
            .AsNoTracking()
            .SingleAsync(invitation => invitation.InvitationId == scenario.InvitationId);
        TeamMembership membership = await context.TeamMemberships
            .AsNoTracking()
            .SingleAsync(item => item.TeamId == scenario.TeamId && item.UserId == scenario.Recipient.Id);
        ActionTrace trace = await context.ActionTraces
            .AsNoTracking()
            .SingleAsync(item => item.ActionCode == "TEAM_INVITATION_ACCEPTED" && item.ObjectIdentifier == scenario.InvitationId.ToString());

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Equal(scenario.TeamId, result.TeamId);
        Assert.Empty(result.Errors);
        Assert.Equal(RequestStatus.Accepted, storedInvitation.Status);
        Assert.Equal(membership.TeamMembershipId, storedInvitation.CreatedMembershipId);
        Assert.NotNull(storedInvitation.ResolvedAtUtc);
        Assert.Equal(scenario.ProposedTeamRoleId, membership.TeamRoleId);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
    }

    [Fact]
    public async Task RefuseInvitationAsync_WhenInvitationIsPending_RefusesWithoutMembershipAndCreatesTrace()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        InvitationScenario scenario = await CreateInvitationScenarioAsync(scope.ServiceProvider);
        IUserNotificationService service = scope.ServiceProvider.GetRequiredService<IUserNotificationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        InvitationActionResult result = await service.RefuseInvitationAsync(new ResolveInvitationRequest(scenario.Recipient.Id, scenario.InvitationId));

        context.ChangeTracker.Clear();

        Invitation storedInvitation = await context.Invitations
            .AsNoTracking()
            .SingleAsync(invitation => invitation.InvitationId == scenario.InvitationId);
        bool membershipExists = await context.TeamMemberships
            .AsNoTracking()
            .AnyAsync(item => item.TeamId == scenario.TeamId && item.UserId == scenario.Recipient.Id);
        ActionTrace trace = await context.ActionTraces
            .AsNoTracking()
            .SingleAsync(item => item.ActionCode == "TEAM_INVITATION_REFUSED" && item.ObjectIdentifier == scenario.InvitationId.ToString());

        Assert.True(result.Succeeded);
        Assert.False(result.AccessDenied);
        Assert.Equal(scenario.TeamId, result.TeamId);
        Assert.Empty(result.Errors);
        Assert.Equal(RequestStatus.Refused, storedInvitation.Status);
        Assert.Null(storedInvitation.CreatedMembershipId);
        Assert.NotNull(storedInvitation.ResolvedAtUtc);
        Assert.False(membershipExists);
        Assert.Equal(TraceOutcome.Succeeded, trace.Outcome);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WhenActorIsNotRecipient_ReturnsDenied()
    {
        await using SqliteTestDatabase database = new();
        await database.InitializeAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider(database.ConnectionString);
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        InvitationScenario scenario = await CreateInvitationScenarioAsync(scope.ServiceProvider);
        IUserNotificationService service = scope.ServiceProvider.GetRequiredService<IUserNotificationService>();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        InvitationActionResult result = await service.AcceptInvitationAsync(new ResolveInvitationRequest(scenario.Owner.Id, scenario.InvitationId));

        context.ChangeTracker.Clear();

        Invitation storedInvitation = await context.Invitations
            .AsNoTracking()
            .SingleAsync(invitation => invitation.InvitationId == scenario.InvitationId);
        bool membershipExists = await context.TeamMemberships
            .AsNoTracking()
            .AnyAsync(item => item.TeamId == scenario.TeamId && item.UserId == scenario.Recipient.Id);

        Assert.False(result.Succeeded);
        Assert.True(result.AccessDenied);
        Assert.Null(result.TeamId);
        Assert.Empty(result.Errors);
        Assert.Equal(RequestStatus.Pending, storedInvitation.Status);
        Assert.Null(storedInvitation.ResolvedAtUtc);
        Assert.False(membershipExists);
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
        services.AddScoped<IUserNotificationService, UserNotificationService>();
        services.AddSingleton<IPrivateImageService, StubPrivateImageService>();

        return services.BuildServiceProvider();
    }

    private static async Task<InvitationScenario> CreateInvitationScenarioAsync(IServiceProvider services)
    {
        UserManager<ApplicationUser> userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = services.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateActiveUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser recipient = await CreateActiveUserAsync(userManager, "recipient@example.test", "Recipient", "B02");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int proposedTeamRoleId = await context.TeamRoles
            .Where(role => role.Code == "Player")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        Guid invitationId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        Invitation invitation = new(invitationId, teamId, owner.Id, recipient.Id, proposedTeamRoleId, createdAtUtc);
        Notification notification = Notification.CreateForInvitation(notificationId, recipient.Id, invitationId, createdAtUtc);

        context.Invitations.Add(invitation);
        context.Notifications.Add(notification);

        await context.SaveChangesAsync();

        return new InvitationScenario(owner, recipient, teamId, proposedTeamRoleId, invitationId, notificationId);
    }

    private static async Task<OwnershipTransferScenario> CreateOwnershipTransferScenarioAsync(IServiceProvider services)
    {
        UserManager<ApplicationUser> userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        IUserTeamService teamService = services.GetRequiredService<IUserTeamService>();
        ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();
        ApplicationUser owner = await CreateActiveUserAsync(userManager, "owner@example.test", "Owner", "A01");
        ApplicationUser recipient = await CreateActiveUserAsync(userManager, "recipient@example.test", "Recipient", "B02");

        CreateTeamResult teamResult = await teamService.CreateAsync(new CreateTeamRequest(owner.Id, "Phoenix Academy", "PHX", "Europe/Paris"));

        Assert.True(teamResult.Succeeded);
        Assert.NotNull(teamResult.TeamId);

        Guid teamId = teamResult.TeamId.Value;
        int managerRoleId = await context.TeamRoles
            .Where(role => role.Code == "Manager")
            .Select(role => role.TeamRoleId)
            .SingleAsync();
        TeamMembership initiatorMembership = await context.TeamMemberships
            .SingleAsync(membership => membership.TeamId == teamId && membership.UserId == owner.Id);
        TeamMembership recipientMembership = new(Guid.NewGuid(), teamId, recipient.Id, managerRoleId, DateTimeOffset.UtcNow.AddMinutes(-2));
        Guid ownershipTransferId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        OwnershipTransfer ownershipTransfer = new(ownershipTransferId, teamId, initiatorMembership.TeamMembershipId, recipientMembership.TeamMembershipId, createdAtUtc);
        Notification notification = Notification.CreateForOwnershipTransfer(notificationId, recipient.Id, ownershipTransferId, createdAtUtc);

        context.TeamMemberships.Add(recipientMembership);
        context.OwnershipTransfers.Add(ownershipTransfer);
        context.Notifications.Add(notification);

        await context.SaveChangesAsync();

        return new OwnershipTransferScenario(recipient, teamId, ownershipTransferId, notificationId);
    }

    private static async Task<ApplicationUser> CreateActiveUserAsync(UserManager<ApplicationUser> userManager, string email, string pseudo, string tag)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        ApplicationUser user = new(Guid.NewGuid(), email, pseudo, tag, utcNow, utcNow);
        IdentityResult createResult = await userManager.CreateAsync(user);

        Assert.True(createResult.Succeeded, string.Join(" | ", createResult.Errors.Select(error => error.Description)));

        user.MarkAsConfirmed(utcNow);

        IdentityResult updateResult = await userManager.UpdateAsync(user);

        Assert.True(updateResult.Succeeded, string.Join(" | ", updateResult.Errors.Select(error => error.Description)));

        return user;
    }

    private sealed class InvitationScenario
    {
        public ApplicationUser Owner { get; }

        public ApplicationUser Recipient { get; }

        public Guid TeamId { get; }

        public int ProposedTeamRoleId { get; }

        public Guid InvitationId { get; }

        public Guid NotificationId { get; }

        public InvitationScenario(ApplicationUser owner, ApplicationUser recipient, Guid teamId, int proposedTeamRoleId, Guid invitationId, Guid notificationId)
        {
            Owner = owner;
            Recipient = recipient;
            TeamId = teamId;
            ProposedTeamRoleId = proposedTeamRoleId;
            InvitationId = invitationId;
            NotificationId = notificationId;
        }
    }

    private sealed class OwnershipTransferScenario
    {
        public ApplicationUser Recipient { get; }

        public Guid TeamId { get; }

        public Guid OwnershipTransferId { get; }

        public Guid NotificationId { get; }

        public OwnershipTransferScenario(ApplicationUser recipient, Guid teamId, Guid ownershipTransferId, Guid notificationId)
        {
            Recipient = recipient;
            TeamId = teamId;
            OwnershipTransferId = ownershipTransferId;
            NotificationId = notificationId;
        }
    }

    private sealed class StubPrivateImageService : IPrivateImageService
    {
        public Task<PrivateImageContent?> GetTeamLogoThumbnailAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public Task<StorePrivateImageResult> StoreStrategyImageAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<StorePrivateImageResult> StoreTeamLogoAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
}