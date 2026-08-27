using EsportTeamManager.Application.Notifications;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EsportTeamManager.Infrastructure.Notifications;

public sealed class UserNotificationService : IUserNotificationService
{
    private const string InvitationAcceptedActionCode = "TEAM_INVITATION_ACCEPTED";
    private const string InvitationRefusedActionCode = "TEAM_INVITATION_REFUSED";

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UserNotificationService> _logger;

    public UserNotificationService(ApplicationDbContext context, TimeProvider timeProvider, ILogger<UserNotificationService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<InvitationActionResult> AcceptInvitationAsync(ResolveInvitationRequest request, CancellationToken cancellationToken = default)
    {
        return ResolveInvitationAsync(request, RequestStatus.Accepted, cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserNotificationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
        {
            return [];
        }

        await EnsureInvitationNotificationsAsync(userId, cancellationToken);

        var invitationRows = await (
            from notification in _context.Notifications.AsNoTracking()
            join invitation in _context.Invitations.AsNoTracking() on notification.InvitationId equals (Guid?)invitation.InvitationId
            join team in _context.Teams.AsNoTracking() on invitation.TeamId equals team.TeamId
            join sender in _context.Users.AsNoTracking() on invitation.SenderUserId equals sender.Id
            join role in _context.TeamRoles.AsNoTracking() on invitation.ProposedTeamRoleId equals role.TeamRoleId
            where notification.RecipientUserId == userId
            select new
            {
                notification.NotificationId,
                notification.ReadAtUtc,
                invitation.InvitationId,
                invitation.TeamId,
                TeamName = team.Name,
                TeamTag = team.Tag,
                ActorPseudo = sender.Pseudo,
                ActorTag = sender.Tag,
                ProposedRoleLabel = role.Label,
                invitation.Status,
                notification.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var ownershipTransferRows = await (
            from notification in _context.Notifications.AsNoTracking()
            join transfer in _context.OwnershipTransfers.AsNoTracking() on notification.OwnershipTransferId equals (Guid?)transfer.OwnershipTransferId
            join team in _context.Teams.AsNoTracking() on transfer.TeamId equals team.TeamId
            join initiatorMembership in _context.TeamMemberships.AsNoTracking() on transfer.InitiatorMembershipId equals initiatorMembership.TeamMembershipId
            join initiator in _context.Users.AsNoTracking() on initiatorMembership.UserId equals (Guid?)initiator.Id
            where notification.RecipientUserId == userId
            select new
            {
                notification.NotificationId,
                notification.ReadAtUtc,
                transfer.OwnershipTransferId,
                transfer.TeamId,
                TeamName = team.Name,
                TeamTag = team.Tag,
                ActorPseudo = initiator.Pseudo,
                ActorTag = initiator.Tag,
                transfer.Status,
                notification.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        IReadOnlyCollection<UserNotificationSummary> notifications =
        [
            .. invitationRows
                .Select(row => new UserNotificationSummary(
                    row.NotificationId,
                    UserNotificationKind.Invitation,
                    row.InvitationId,
                    row.TeamId,
                    row.TeamName,
                    row.TeamTag,
                    row.ActorPseudo,
                    row.ActorTag,
                    row.ProposedRoleLabel,
                    row.Status,
                    row.CreatedAtUtc,
                    row.ReadAtUtc))
                .Concat(ownershipTransferRows.Select(row => new UserNotificationSummary(
                    row.NotificationId,
                    UserNotificationKind.OwnershipTransfer,
                    row.OwnershipTransferId,
                    row.TeamId,
                    row.TeamName,
                    row.TeamTag,
                    row.ActorPseudo,
                    row.ActorTag,
                    null,
                    row.Status,
                    row.CreatedAtUtc,
                    row.ReadAtUtc)))
                .OrderByDescending(notification => notification.CreatedAtUtc)
                .ThenBy(notification => notification.NotificationId)
        ];

        return notifications;
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
        {
            return 0;
        }

        await EnsureInvitationNotificationsAsync(userId, cancellationToken);

        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.RecipientUserId == userId && !notification.ReadAtUtc.HasValue, cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
        {
            return;
        }

        await EnsureInvitationNotificationsAsync(userId, cancellationToken);

        List<Notification> unreadNotifications = await _context.Notifications
            .Where(notification => notification.RecipientUserId == userId && !notification.ReadAtUtc.HasValue)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count == 0)
        {
            return;
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        foreach (Notification notification in unreadNotifications)
        {
            notification.MarkAsRead(utcNow);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Notification reading persistence failed for user {UserId}.", userId);

            throw;
        }
    }

    public Task<InvitationActionResult> RefuseInvitationAsync(ResolveInvitationRequest request, CancellationToken cancellationToken = default)
    {
        return ResolveInvitationAsync(request, RequestStatus.Refused, cancellationToken);
    }

    private async Task EnsureInvitationNotificationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var missingInvitations = await _context.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.RecipientUserId == userId)
            .Where(invitation => !_context.Notifications.Any(notification => notification.InvitationId == invitation.InvitationId))
            .Select(invitation => new
            {
                invitation.InvitationId,
                invitation.Status,
                invitation.CreatedAtUtc,
                invitation.ResolvedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (missingInvitations.Count == 0)
        {
            return;
        }

        foreach (var invitation in missingInvitations)
        {
            Notification notification = Notification.CreateForInvitation(Guid.NewGuid(), userId, invitation.InvitationId, invitation.CreatedAtUtc);

            if (invitation.Status != RequestStatus.Pending)
            {
                notification.MarkAsRead(invitation.ResolvedAtUtc ?? invitation.CreatedAtUtc);
            }

            _context.Notifications.Add(notification);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _context.ChangeTracker.Clear();

            bool missingInvitationRemains = await _context.Invitations
                .AsNoTracking()
                .Where(invitation => invitation.RecipientUserId == userId)
                .AnyAsync(invitation => !_context.Notifications.Any(notification => notification.InvitationId == invitation.InvitationId), cancellationToken);

            if (missingInvitationRemains)
            {
                _logger.LogError(exception, "Legacy invitation notification backfill failed for user {UserId}.", userId);

                throw;
            }

            _logger.LogInformation(exception, "Legacy invitation notifications were backfilled concurrently for user {UserId}.", userId);
        }
    }

    private async Task<InvitationActionResult> ResolveInvitationAsync(ResolveInvitationRequest request, RequestStatus finalStatus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.InvitationId == Guid.Empty)
        {
            return InvitationActionResult.Denied();
        }

        if (finalStatus != RequestStatus.Accepted && finalStatus != RequestStatus.Refused)
        {
            return InvitationActionResult.Denied();
        }

        Invitation? invitation = await _context.Invitations
            .SingleOrDefaultAsync(invitation => invitation.InvitationId == request.InvitationId, cancellationToken);

        if (invitation is null || invitation.RecipientUserId != request.ActorUserId)
        {
            return InvitationActionResult.Denied();
        }

        if (invitation.Status != RequestStatus.Pending)
        {
            return InvitationActionResult.Failure(["Cette invitation n’est plus en attente."]);
        }

        TeamMembership? createdMembership = null;
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        if (finalStatus == RequestStatus.Accepted)
        {
            bool activeMembershipExists = await _context.TeamMemberships
                .AsNoTracking()
                .AnyAsync(membership => membership.TeamId == invitation.TeamId && membership.UserId == request.ActorUserId && membership.Status == MembershipStatus.Active, cancellationToken);

            if (activeMembershipExists)
            {
                return InvitationActionResult.Failure(["Cette invitation ne peut plus être acceptée."]);
            }

            createdMembership = new TeamMembership(Guid.NewGuid(), invitation.TeamId, request.ActorUserId, invitation.ProposedTeamRoleId, utcNow);
        }

        try
        {
            if (finalStatus == RequestStatus.Accepted)
            {
                invitation.Accept(createdMembership!.TeamMembershipId, utcNow);
            }
            else
            {
                invitation.Refuse(utcNow);
            }
        }
        catch (DomainException)
        {
            return InvitationActionResult.Failure(["L’invitation n’a pas pu être traitée."]);
        }

        if (createdMembership is not null)
        {
            _context.TeamMemberships.Add(createdMembership);
        }

        string actionCode = finalStatus == RequestStatus.Accepted
            ? InvitationAcceptedActionCode
            : InvitationRefusedActionCode;
        ActionTrace actionTrace = new(request.ActorUserId, invitation.TeamId, actionCode, nameof(Invitation), invitation.InvitationId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(exception, "Invitation {InvitationId} was resolved concurrently by user {UserId}.", request.InvitationId, request.ActorUserId);

            return InvitationActionResult.Failure(["Cette invitation a déjà été traitée. Rechargez la page."]);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Invitation resolution persistence failed for user {UserId}, invitation {InvitationId}, and status {FinalStatus}.", request.ActorUserId, request.InvitationId, finalStatus);

            return InvitationActionResult.Failure(["L’invitation n’a pas pu être enregistrée. Veuillez réessayer."]);
        }

        return InvitationActionResult.Success(invitation.TeamId);
    }
}