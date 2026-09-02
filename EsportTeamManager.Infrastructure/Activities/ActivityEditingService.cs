using EsportTeamManager.Application.Activities;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;

namespace EsportTeamManager.Infrastructure.Activities;

public sealed class ActivityEditingService : IActivityEditingService
{
    private const string ManagerRoleCode = "Manager";
    private const string CoachRoleCode = "Coach";
    private const string ActivityUpdatedActionCode = "ACTIVITY_UPDATED";
    private const string ActivityDeletedActionCode = "ACTIVITY_DELETED";

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ActivityEditingService> _logger;

    public ActivityEditingService(ApplicationDbContext context, TimeProvider timeProvider, ILogger<ActivityEditingService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ActivityEditDetails?> GetAsync(Guid userId, Guid teamId, Guid activityId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty || teamId == Guid.Empty || activityId == Guid.Empty)
        {
            return null;
        }

        var access = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.TeamId == teamId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new
            {
                Membership = membership,
                Team = team
            })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                item.Team,
                RoleCode = role.Code
            })
            .Select(item => new
            {
                item.Team.TeamId,
                TeamName = item.Team.Name,
                item.Team.TimeZoneId,
                item.Team.OwnerUserId,
                item.RoleCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (access is null)
        {
            return null;
        }

        TeamActivity? activity = await _context.TeamActivities
            .AsNoTracking()
            .Include(item => item.ActivityType)
            .Include(item => item.MatchDetail)
            .SingleOrDefaultAsync(item => item.ActivityId == activityId && item.TeamId == teamId, cancellationToken);

        if (activity is null)
        {
            return null;
        }

        TimeZoneInfo timeZone;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(access.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }

        IReadOnlyCollection<ActivityTypeOption> activityTypes = await _context.ActivityTypes
            .AsNoTracking()
            .Where(activityType => activityType.IsSystem || activityType.TeamId == teamId)
            .OrderBy(activityType => activityType.Label)
            .Select(activityType => new ActivityTypeOption(activityType.ActivityTypeId, activityType.Code, activityType.Label))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, Attendance?> existingParticipants = await _context.ActivityParticipants
            .AsNoTracking()
            .Where(participant => participant.ActivityId == activityId)
            .ToDictionaryAsync(participant => participant.TeamMembershipId, participant => participant.Attendance, cancellationToken);

        Guid[] existingParticipantIdentifiers = existingParticipants.Keys.ToArray();
        bool canEdit = access.OwnerUserId == userId || access.RoleCode == ManagerRoleCode || access.RoleCode == CoachRoleCode;
        bool includeMembershipOptions = canEdit;
        DateTimeOffset plannedStartUtc = activity.PlannedStartUtc;

        List<TeamMembership> teamMembershipCandidates = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId)
            .ToListAsync(cancellationToken);

        Guid[] visibleMembershipIdentifiers = teamMembershipCandidates
            .Where(membership =>
                existingParticipantIdentifiers.Contains(membership.TeamMembershipId)
                || includeMembershipOptions
                && (membership.Status == MembershipStatus.Active && membership.UserId.HasValue
                    || activity.Status == ActivityStatus.Completed
                    && membership.JoinedAtUtc <= plannedStartUtc
                    && (!membership.LeftAtUtc.HasValue || membership.LeftAtUtc.Value >= plannedStartUtc)))
            .Select(membership => membership.TeamMembershipId)
            .ToArray();

        var participantMemberships = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => visibleMembershipIdentifiers.Contains(membership.TeamMembershipId))
            .Join(_context.TeamRoles, membership => membership.TeamRoleId, role => role.TeamRoleId, (membership, role) => new
            {
                Membership = membership,
                RoleLabel = role.Label
            })
            .GroupJoin(_context.Users, item => item.Membership.UserId, user => (Guid?)user.Id, (item, users) => new
            {
                item.Membership,
                item.RoleLabel,
                Users = users
            })
            .SelectMany(item => item.Users.DefaultIfEmpty(), (item, user) => new
            {
                item.Membership,
                item.RoleLabel,
                User = user
            })
            .GroupJoin(_context.FormerMembers, item => item.Membership.FormerMemberId, formerMember => (Guid?)formerMember.FormerMemberId, (item, formerMembers) => new
            {
                item.Membership,
                item.RoleLabel,
                item.User,
                FormerMembers = formerMembers
            })
            .SelectMany(item => item.FormerMembers.DefaultIfEmpty(), (item, formerMember) => new
            {
                item.Membership,
                item.RoleLabel,
                item.User,
                FormerMember = formerMember
            })
            .ToListAsync(cancellationToken);

        IReadOnlyCollection<ActivityEditParticipantSummary> participants = participantMemberships
            .Select(item =>
            {
                string displayName = item.User is not null
                    ? $"{item.User.Pseudo}#{item.User.Tag}"
                    : item.FormerMember is not null
                        ? $"Utilisateur supprimé {item.FormerMember.LocalNumber}"
                        : "Ancien membre";

                bool isSelected = existingParticipants.TryGetValue(item.Membership.TeamMembershipId, out Attendance? attendance);

                return new ActivityEditParticipantSummary(
                    item.Membership.TeamMembershipId,
                    displayName,
                    item.RoleLabel,
                    item.Membership.UserId == access.OwnerUserId,
                    isSelected,
                    item.Membership.Status != MembershipStatus.Active,
                    attendance);
            })
            .OrderBy(participant => participant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        IReadOnlyCollection<ActivityEditLinkSummary> links = await _context.ActivityLinks
            .AsNoTracking()
            .Where(link => link.ActivityId == activityId)
            .OrderBy(link => link.Name)
            .Select(link => new ActivityEditLinkSummary(link.ActivityLinkId, link.Name, link.Url))
            .ToListAsync(cancellationToken);

        Guid[] existingStrategyIdentifiers = await _context.ActivityStrategies
            .AsNoTracking()
            .Where(activityStrategy => activityStrategy.ActivityId == activityId)
            .Select(activityStrategy => activityStrategy.StrategyId)
            .ToArrayAsync(cancellationToken);

        IReadOnlyCollection<ActivityEditStrategySummary> strategies = await _context.Strategies
            .AsNoTracking()
            .Where(strategy =>
                strategy.TeamId == teamId
                && (existingStrategyIdentifiers.Contains(strategy.StrategyId)
                    || canEdit && strategy.IsActive))
            .OrderBy(strategy => strategy.Name)
            .Select(strategy => new ActivityEditStrategySummary(
                strategy.StrategyId,
                strategy.Name,
                strategy.Map.Name,
                strategy.Side,
                strategy.IsActive,
                existingStrategyIdentifiers.Contains(strategy.StrategyId),
                _context.ImageFiles.Any(image => image.StrategyImageForStrategyId == strategy.StrategyId)))
            .ToListAsync(cancellationToken);

        DateTime plannedStartLocal = TimeZoneInfo.ConvertTime(activity.PlannedStartUtc, timeZone).DateTime;
        DateTime plannedEndLocal = TimeZoneInfo.ConvertTime(activity.PlannedEndUtc, timeZone).DateTime;

        return new ActivityEditDetails(
            activity.ActivityId,
            activity.TeamId,
            access.TeamName,
            access.TimeZoneId,
            activity.ActivityTypeId,
            activity.ActivityType.Code,
            activity.ActivityType.Label,
            activity.Subtitle,
            plannedStartLocal,
            plannedEndLocal,
            activity.Description,
            activity.Report,
            activity.Status,
            activity.CancellationReason,
            activity.MatchDetail?.OpponentName,
            activity.MatchDetail?.TeamScore,
            activity.MatchDetail?.OpponentScore,
            activity.UpdatedAtUtc,
            canEdit,
            activityTypes,
            participants,
            links,
            activity.MatchDetail?.Result,
            strategies);
    }

    public async Task<UpdateActivityResult> UpdateAsync(UpdateActivityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.UserId == Guid.Empty || request.TeamId == Guid.Empty || request.ActivityId == Guid.Empty)
        {
            return UpdateActivityResult.Failure(["L’utilisateur, l’équipe ou l’activité est introuvable."]);
        }

        if (request.ActivityTypeId <= 0)
        {
            return UpdateActivityResult.Failure(["Le type d’activité est obligatoire."]);
        }

        var access = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == request.UserId && membership.TeamId == request.TeamId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new
            {
                Membership = membership,
                Team = team
            })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                item.Team.OwnerUserId,
                item.Team.TimeZoneId,
                RoleCode = role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (access is null || access.OwnerUserId != request.UserId && access.RoleCode != ManagerRoleCode && access.RoleCode != CoachRoleCode)
        {
            return UpdateActivityResult.Failure(["Vous n’êtes pas autorisé à modifier cette activité."]);
        }

        TeamActivity? activity = await _context.TeamActivities
            .Include(item => item.ActivityType)
            .Include(item => item.MatchDetail)
            .Include(item => item.Participants)
            .SingleOrDefaultAsync(item => item.ActivityId == request.ActivityId && item.TeamId == request.TeamId, cancellationToken);

        if (activity is null)
        {
            return UpdateActivityResult.Failure(["L’activité est introuvable."]);
        }

        ActivityStatus requestedStatus = request.Status ?? activity.Status;

        if (!Enum.IsDefined(typeof(ActivityStatus), requestedStatus))
        {
            return UpdateActivityResult.Failure(["L’état demandé n’est pas valide."]);
        }

        bool departureRequiresConfirmation = activity.Status != requestedStatus && (activity.Status is ActivityStatus.Completed or ActivityStatus.Cancelled);

        if (departureRequiresConfirmation && !request.StatusChangeConfirmed)
        {
            return UpdateActivityResult.Failure(["Confirmez le changement depuis une activité terminée ou annulée."]);
        }

        ActivityType? activityType = await _context.ActivityTypes
            .SingleOrDefaultAsync(item => item.ActivityTypeId == request.ActivityTypeId && (item.IsSystem || item.TeamId == request.TeamId), cancellationToken);

        if (activityType is null)
        {
            return UpdateActivityResult.Failure(["Le type d’activité sélectionné n’est pas disponible pour cette équipe."]);
        }

        bool activityTypeRequiresScores = activityType.Code is "Pracc" or "OfficialMatch";
        bool teamScoreIsProvided = request.TeamScore.HasValue;
        bool opponentScoreIsProvided = request.OpponentScore.HasValue;

        if (activityTypeRequiresScores && string.IsNullOrWhiteSpace(request.OpponentName))
        {
            return UpdateActivityResult.Failure(["L’équipe adverse est obligatoire pour une pracc ou un match officiel."]);
        }

        if (activityTypeRequiresScores && teamScoreIsProvided != opponentScoreIsProvided)
        {
            return UpdateActivityResult.Failure(["Les deux scores doivent être renseignés ensemble."]);
        }

        if (activityTypeRequiresScores && (request.TeamScore < 0 || request.OpponentScore < 0))
        {
            return UpdateActivityResult.Failure(["Les scores ne peuvent pas être négatifs."]);
        }

        if (activityTypeRequiresScores && requestedStatus == ActivityStatus.Completed && !teamScoreIsProvided)
        {
            return UpdateActivityResult.Failure(["Les deux scores sont obligatoires pour une activité terminée."]);
        }

        if (!activityTypeRequiresScores && (!string.IsNullOrWhiteSpace(request.OpponentName) || teamScoreIsProvided || opponentScoreIsProvided))
        {
            return UpdateActivityResult.Failure(["L’adversaire et les scores sont disponibles uniquement pour une pracc ou un match officiel."]);
        }

        TimeZoneInfo timeZone;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(access.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return UpdateActivityResult.Failure(["Le fuseau horaire de l’équipe est introuvable."]);
        }
        catch (InvalidTimeZoneException)
        {
            return UpdateActivityResult.Failure(["Le fuseau horaire de l’équipe n’est pas valide."]);
        }

        DateTime plannedStartLocal = DateTime.SpecifyKind(request.PlannedStartLocal, DateTimeKind.Unspecified);
        DateTime plannedEndLocal = DateTime.SpecifyKind(request.PlannedEndLocal, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(plannedStartLocal) || timeZone.IsInvalidTime(plannedEndLocal))
        {
            return UpdateActivityResult.Failure(["L’une des heures choisies n’existe pas dans le fuseau de l’équipe en raison du changement d’heure."]);
        }

        if (timeZone.IsAmbiguousTime(plannedStartLocal) || timeZone.IsAmbiguousTime(plannedEndLocal))
        {
            return UpdateActivityResult.Failure(["L’une des heures choisies est ambiguë dans le fuseau de l’équipe en raison du changement d’heure."]);
        }

        DateTimeOffset plannedStartUtc = new(TimeZoneInfo.ConvertTimeToUtc(plannedStartLocal, timeZone));
        DateTimeOffset plannedEndUtc = new(TimeZoneInfo.ConvertTimeToUtc(plannedEndLocal, timeZone));

        if (plannedEndUtc <= plannedStartUtc)
        {
            return UpdateActivityResult.Failure(["La fin prévue doit être strictement postérieure au début prévu."]);
        }

        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();

        if (requestedStatus != ActivityStatus.Completed && activity.Status != requestedStatus)
        {
            try
            {
                activity.ChangeStatus(requestedStatus, null, request.CancellationReason, request.StatusChangeConfirmed, updatedAtUtc);
            }
            catch (DomainException)
            {
                return UpdateActivityResult.Failure(["Les informations fournies ne permettent pas de changer l’état de l’activité."]);
            }
        }

        string? participantUpdateError = await UpdateParticipantsAsync(activity, request.TeamId, request.Participants, requestedStatus, plannedStartUtc, updatedAtUtc, cancellationToken);

        if (participantUpdateError is not null)
        {
            return UpdateActivityResult.Failure([participantUpdateError]);
        }

        IReadOnlyDictionary<Guid, Attendance>? attendanceByMembershipId = requestedStatus == ActivityStatus.Completed
            ? request.Participants
                .Where(participant => participant.IsSelected)
                .ToDictionary(participant => participant.TeamMembershipId, participant => participant.Attendance!.Value)
            : null;

        string? linkSynchronizationError = await SynchronizeLinksAsync(request.ActivityId, request.Links, cancellationToken);

        if (linkSynchronizationError is not null)
        {
            return UpdateActivityResult.Failure([linkSynchronizationError]);
        }

        string? strategySynchronizationError = await SynchronizeStrategiesAsync(request.ActivityId, request.TeamId, request.StrategyIds, cancellationToken);

        if (strategySynchronizationError is not null)
        {
            return UpdateActivityResult.Failure([strategySynchronizationError]);
        }

        MatchDetail? previousMatchDetail = activity.MatchDetail;

        try
        {
            activity.ChangeType(activityType, updatedAtUtc);
            activity.Reschedule(plannedStartUtc, plannedEndUtc, access.TimeZoneId, updatedAtUtc);
            activity.UpdateTexts(request.Subtitle, request.Description, request.Report, updatedAtUtc);

            if (activity.RequiresScores)
            {
                activity.UpdateOpponent(request.OpponentName, updatedAtUtc);

                if (request.TeamScore.HasValue && request.OpponentScore.HasValue)
                {
                    activity.SetScores(request.TeamScore.Value, request.OpponentScore.Value, updatedAtUtc);
                }
                else
                {
                    activity.ClearScores(updatedAtUtc);
                }
            }

            activity.ChangeStatus(requestedStatus, attendanceByMembershipId, request.CancellationReason, request.StatusChangeConfirmed, updatedAtUtc);

            if (previousMatchDetail is not null && activity.MatchDetail is null)
            {
                _context.MatchDetails.Remove(previousMatchDetail);
            }
            else if (previousMatchDetail is null && activity.MatchDetail is not null)
            {
                _context.MatchDetails.Add(activity.MatchDetail);
            }
        }
        catch (DomainException)
        {
            return UpdateActivityResult.Failure(["Les informations fournies ne permettent pas de modifier l’activité."]);
        }

        ActionTrace actionTrace = new(request.UserId, request.TeamId, ActivityUpdatedActionCode, nameof(TeamActivity), request.ActivityId.ToString(), TraceOutcome.Succeeded, updatedAtUtc);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Activity update persistence failed for actor {ActorUserId}, team {TeamId} and activity {ActivityId}.", request.UserId, request.TeamId, request.ActivityId);

            return UpdateActivityResult.Failure(["L’activité n’a pas pu être modifiée. Veuillez réessayer."]);
        }

        return UpdateActivityResult.Success();
    }

    public async Task<DeleteActivityResult> DeleteAsync(DeleteActivityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty || request.ActivityId == Guid.Empty)
        {
            return DeleteActivityResult.Failure(["L’activité est introuvable."]);
        }

        var access = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == request.ActorUserId && membership.TeamId == request.TeamId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new
            {
                Membership = membership,
                Team = team
            })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                item.Team.OwnerUserId,
                RoleCode = role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (access is null || access.OwnerUserId != request.ActorUserId && access.RoleCode != ManagerRoleCode && access.RoleCode != CoachRoleCode)
        {
            return DeleteActivityResult.Failure(["Vous n’êtes pas autorisé à supprimer cette activité."]);
        }

        TeamActivity? activity = await _context.TeamActivities
            .SingleOrDefaultAsync(item => item.ActivityId == request.ActivityId && item.TeamId == request.TeamId, cancellationToken);

        if (activity is null)
        {
            return DeleteActivityResult.Failure(["L’activité est introuvable."]);
        }

        int participantCount = await _context.ActivityParticipants
            .AsNoTracking()
            .CountAsync(participant => participant.ActivityId == request.ActivityId, cancellationToken);

        int linkCount = await _context.ActivityLinks
            .AsNoTracking()
            .CountAsync(link => link.ActivityId == request.ActivityId, cancellationToken);

        int strategyAssociationCount = await _context.ActivityStrategies
            .AsNoTracking()
            .CountAsync(association => association.ActivityId == request.ActivityId, cancellationToken);

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.TeamActivities.Remove(activity);

        ActionTrace actionTrace = new(request.ActorUserId, request.TeamId, ActivityDeletedActionCode, nameof(TeamActivity), activity.ActivityId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            _logger.LogError(exception, "Activity deletion persistence failed for actor {ActorUserId}, team {TeamId} and activity {ActivityId}.", request.ActorUserId, request.TeamId, request.ActivityId);

            return DeleteActivityResult.Failure(["L’activité n’a pas pu être supprimée. Veuillez réessayer."]);
        }

        return DeleteActivityResult.Success(participantCount, linkCount, strategyAssociationCount);
    }

    private async Task<string?> UpdateParticipantsAsync(TeamActivity activity, Guid teamId, IReadOnlyCollection<UpdateActivityParticipantRequest> requestedParticipants, ActivityStatus requestedStatus, DateTimeOffset plannedStartUtc, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
    {
        if (requestedParticipants.Count == 0)
        {
            return "Sélectionnez au moins un participant.";
        }

        HashSet<Guid> requestedIdentifiers = [];

        foreach (UpdateActivityParticipantRequest requestedParticipant in requestedParticipants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (requestedParticipant.TeamMembershipId == Guid.Empty || !requestedIdentifiers.Add(requestedParticipant.TeamMembershipId))
            {
                return "La demande contient un identifiant de participant invalide ou répété.";
            }
        }

        UpdateActivityParticipantRequest[] selectedParticipants = requestedParticipants
            .Where(participant => participant.IsSelected)
            .ToArray();

        if (selectedParticipants.Length == 0)
        {
            return "Sélectionnez au moins un participant.";
        }

        HashSet<Guid> selectedIdentifiers = selectedParticipants
            .Select(participant => participant.TeamMembershipId)
            .ToHashSet();

        HashSet<Guid> existingIdentifiers = activity.Participants
            .Select(participant => participant.TeamMembershipId)
            .ToHashSet();

        if (requestedStatus != ActivityStatus.Completed)
        {
            if (requestedParticipants.Any(participant => participant.Attendance.HasValue))
            {
                return "Une activité planifiée ou annulée ne peut pas appliquer de présence.";
            }

            HashSet<Guid> activeMembershipIdentifiers = await _context.TeamMemberships
                .AsNoTracking()
                .Where(membership =>
                    selectedIdentifiers.Contains(membership.TeamMembershipId)
                    && membership.TeamId == teamId
                    && membership.Status == MembershipStatus.Active
                    && membership.UserId.HasValue)
                .Select(membership => membership.TeamMembershipId)
                .ToHashSetAsync(cancellationToken);

            if (selectedIdentifiers.Any(identifier => !activeMembershipIdentifiers.Contains(identifier) && !existingIdentifiers.Contains(identifier)))
            {
                return "Un ou plusieurs participants ne sont pas membres actifs de cette équipe et ne sont pas déjà associés à l’activité.";
            }

            try
            {
                activity.ReplaceParticipants(selectedIdentifiers, updatedAtUtc);
            }
            catch (DomainException)
            {
                return "Les participants fournis ne permettent pas de modifier l’activité.";
            }

            return null;
        }

        List<TeamMembership> selectedMemberships = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => selectedIdentifiers.Contains(membership.TeamMembershipId) && membership.TeamId == teamId)
            .ToListAsync(cancellationToken);

                HashSet<Guid> eligibleCompletedMembershipIdentifiers = selectedMemberships
                    .Where(membership =>
                        membership.Status == MembershipStatus.Active && membership.UserId.HasValue
                        || membership.JoinedAtUtc <= plannedStartUtc
                        && (!membership.LeftAtUtc.HasValue || membership.LeftAtUtc.Value >= plannedStartUtc))
                    .Select(membership => membership.TeamMembershipId)
                    .ToHashSet();

        if (selectedIdentifiers.Any(identifier => !eligibleCompletedMembershipIdentifiers.Contains(identifier) && !existingIdentifiers.Contains(identifier)))
        {
            return "Un ou plusieurs participants ne peuvent pas être associés à cette activité terminée.";
        }

        if (selectedParticipants.Any(participant => !participant.Attendance.HasValue || !Enum.IsDefined(typeof(Attendance), participant.Attendance.Value)))
        {
            return "Une présence doit être renseignée pour chaque participant inclus.";
        }

        Dictionary<Guid, Attendance> attendanceByMembershipId = selectedParticipants
            .ToDictionary(participant => participant.TeamMembershipId, participant => participant.Attendance!.Value);

        try
        {
            if (activity.Status == ActivityStatus.Completed)
            {
                activity.ReplaceParticipants(selectedIdentifiers, updatedAtUtc, attendanceByMembershipId);
            }
            else
            {
                activity.ReplaceParticipants(selectedIdentifiers, updatedAtUtc);
            }
        }
        catch (DomainException)
        {
            return "Les participants et présences fournis ne permettent pas de modifier l’activité.";
        }

        return null;
    }

    private async Task<string?> SynchronizeStrategiesAsync(Guid activityId, Guid teamId, IReadOnlyCollection<Guid> requestedStrategyIds, CancellationToken cancellationToken)
    {
        HashSet<Guid> requestedIdentifiers = [];

        foreach (Guid strategyId in requestedStrategyIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (strategyId == Guid.Empty || !requestedIdentifiers.Add(strategyId))
            {
                return "La demande contient un identifiant de stratégie invalide ou répété.";
            }
        }

        List<ActivityStrategy> existingAssociations = await _context.ActivityStrategies
            .Where(activityStrategy => activityStrategy.ActivityId == activityId)
            .ToListAsync(cancellationToken);

        HashSet<Guid> existingStrategyIdentifiers = existingAssociations
            .Select(activityStrategy => activityStrategy.StrategyId)
            .ToHashSet();

        if (requestedIdentifiers.Count > 0)
        {
            HashSet<Guid> eligibleStrategyIdentifiers = await _context.Strategies
                .AsNoTracking()
                .Where(strategy =>
                    requestedIdentifiers.Contains(strategy.StrategyId)
                    && strategy.TeamId == teamId
                    && (strategy.IsActive || existingStrategyIdentifiers.Contains(strategy.StrategyId)))
                .Select(strategy => strategy.StrategyId)
                .ToHashSetAsync(cancellationToken);

            if (requestedIdentifiers.Any(strategyId => !eligibleStrategyIdentifiers.Contains(strategyId)))
            {
                return "Une ou plusieurs stratégies ne sont pas actives dans cette équipe et ne sont pas déjà associées à l’activité.";
            }
        }

        IEnumerable<ActivityStrategy> removedAssociations = existingAssociations
            .Where(activityStrategy => !requestedIdentifiers.Contains(activityStrategy.StrategyId));

        _context.ActivityStrategies.RemoveRange(removedAssociations);

        IEnumerable<ActivityStrategy> addedAssociations = requestedIdentifiers
            .Where(strategyId => !existingStrategyIdentifiers.Contains(strategyId))
            .Select(strategyId => new ActivityStrategy(activityId, strategyId));

        _context.ActivityStrategies.AddRange(addedAssociations);

        return null;
    }

    private async Task<string?> SynchronizeLinksAsync(Guid activityId, IReadOnlyCollection<UpdateActivityLinkRequest> requestedLinks, CancellationToken cancellationToken)
    {
        List<ActivityLink> existingLinks = await _context.ActivityLinks
            .Where(link => link.ActivityId == activityId)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, ActivityLink> existingLinksById = existingLinks
            .ToDictionary(link => link.ActivityLinkId);

        HashSet<Guid> requestedExistingLinkIds = [];

        foreach (UpdateActivityLinkRequest requestedLink in requestedLinks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (requestedLink.ActivityLinkId == Guid.Empty)
            {
                continue;
            }

            if (!requestedExistingLinkIds.Add(requestedLink.ActivityLinkId))
            {
                return "La demande contient plusieurs fois le même lien.";
            }

            if (!existingLinksById.ContainsKey(requestedLink.ActivityLinkId))
            {
                return "Un lien fourni n’appartient pas à cette activité.";
            }
        }

        List<(UpdateActivityLinkRequest Request, ActivityLink ValidatedLink)> validatedLinks = [];

        try
        {
            foreach (UpdateActivityLinkRequest requestedLink in requestedLinks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ActivityLink validatedLink = new(activityId, requestedLink.Name, requestedLink.Url);

                validatedLinks.Add((requestedLink, validatedLink));
            }
        }
        catch (DomainException)
        {
            return "Les informations fournies ne permettent pas de modifier les liens de l’activité.";
        }

        foreach ((UpdateActivityLinkRequest requestedLink, ActivityLink validatedLink) in validatedLinks)
        {
            if (requestedLink.ActivityLinkId == Guid.Empty)
            {
                _context.ActivityLinks.Add(validatedLink);

                continue;
            }

            ActivityLink existingLink = existingLinksById[requestedLink.ActivityLinkId];

            existingLink.Update(validatedLink.Name, validatedLink.Url);
        }

        IEnumerable<ActivityLink> removedLinks = existingLinks
            .Where(link => !requestedExistingLinkIds.Contains(link.ActivityLinkId));

        _context.ActivityLinks.RemoveRange(removedLinks);

        return null;
    }
}