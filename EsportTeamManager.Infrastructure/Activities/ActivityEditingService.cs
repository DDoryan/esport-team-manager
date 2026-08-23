using EsportTeamManager.Application.Activities;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Activities;

public sealed class ActivityEditingService : IActivityEditingService
{
    private const string ManagerRoleCode = "Manager";
    private const string CoachRoleCode = "Coach";

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ActivityEditingService(ApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
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

        var participantData = await _context.ActivityParticipants
            .AsNoTracking()
            .Where(participant => participant.ActivityId == activityId)
            .Join(_context.TeamMemberships, participant => participant.TeamMembershipId, membership => membership.TeamMembershipId, (participant, membership) => new
            {
                Participant = participant,
                Membership = membership
            })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                item.Participant,
                item.Membership,
                RoleLabel = role.Label
            })
            .GroupJoin(_context.Users, item => item.Membership.UserId, user => user.Id, (item, users) => new
            {
                item.Participant,
                item.Membership,
                item.RoleLabel,
                Users = users
            })
            .SelectMany(item => item.Users.DefaultIfEmpty(), (item, user) => new
            {
                item.Participant.TeamMembershipId,
                DisplayName = user == null ? "Ancien membre" : user.UserName ?? "Ancien membre",
                item.RoleLabel,
                IsOwner = item.Membership.UserId == access.OwnerUserId,
                item.Participant.Attendance
            })
            .OrderBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        IReadOnlyCollection<ActivityEditParticipantSummary> participants = participantData
            .Select(participant => new ActivityEditParticipantSummary(
                participant.TeamMembershipId,
                participant.DisplayName,
                participant.RoleLabel,
                participant.IsOwner,
                participant.Attendance))
            .ToArray();

        IReadOnlyCollection<ActivityEditLinkSummary> links = await _context.ActivityLinks
            .AsNoTracking()
            .Where(link => link.ActivityId == activityId)
            .OrderBy(link => link.Name)
            .Select(link => new ActivityEditLinkSummary(link.ActivityLinkId, link.Name, link.Url))
            .ToListAsync(cancellationToken);

        bool canEdit = activity.Status != ActivityStatus.Cancelled && (access.OwnerUserId == userId || access.RoleCode == ManagerRoleCode || access.RoleCode == CoachRoleCode);
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
            links);
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
            .SingleOrDefaultAsync(item => item.ActivityId == request.ActivityId && item.TeamId == request.TeamId, cancellationToken);

        if (activity is null)
        {
            return UpdateActivityResult.Failure(["L’activité est introuvable."]);
        }

        ActivityType? activityType = await _context.ActivityTypes
            .SingleOrDefaultAsync(item => item.ActivityTypeId == request.ActivityTypeId && (item.IsSystem || item.TeamId == request.TeamId), cancellationToken);

        if (activityType is null)
        {
            return UpdateActivityResult.Failure(["Le type d’activité sélectionné n’est pas disponible pour cette équipe."]);
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
        MatchDetail? previousMatchDetail = activity.MatchDetail;

        try
        {
            activity.ChangeType(activityType, updatedAtUtc);
            activity.Reschedule(plannedStartUtc, plannedEndUtc, access.TimeZoneId, updatedAtUtc);
            activity.UpdateTexts(request.Subtitle, request.Description, request.Report, updatedAtUtc);

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

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return UpdateActivityResult.Failure(["L’activité n’a pas pu être modifiée. Veuillez réessayer."]);
        }

        return UpdateActivityResult.Success();
    }
}