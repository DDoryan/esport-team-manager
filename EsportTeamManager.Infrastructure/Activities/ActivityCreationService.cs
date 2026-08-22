using EsportTeamManager.Application.Activities;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Activities;

public sealed class ActivityCreationService : IActivityCreationService
{
    private const string ManagerRoleCode = "Manager";
    private const string CoachRoleCode = "Coach";

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ActivityCreationService(ApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<bool> CanCreateAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty || teamId == Guid.Empty)
        {
            return false;
        }

        return await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.TeamId == teamId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new { Membership = membership, Team = team })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new { item.Team, Role = role })
            .AnyAsync(item => item.Team.OwnerUserId == userId || item.Role.Code == ManagerRoleCode || item.Role.Code == CoachRoleCode, cancellationToken);
    }

    public async Task<ActivityCreationOptions?> GetOptionsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await CanCreateAsync(userId, teamId, cancellationToken))
        {
            return null;
        }

        var team = await _context.Teams
            .AsNoTracking()
            .Where(item => item.TeamId == teamId)
            .Select(item => new
            {
                item.TeamId,
                item.Name,
                item.TimeZoneId,
                item.OwnerUserId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            return null;
        }

        IReadOnlyCollection<ActivityTypeOption> activityTypes = await _context.ActivityTypes
            .AsNoTracking()
            .Where(activityType => activityType.IsSystem || activityType.TeamId == teamId)
            .OrderBy(activityType => activityType.ActivityTypeId)
            .Select(activityType => new ActivityTypeOption(activityType.ActivityTypeId, activityType.Code, activityType.Label))
            .ToListAsync(cancellationToken);

        IReadOnlyCollection<ActivityParticipantOption> participants = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId && membership.Status == MembershipStatus.Active && membership.UserId.HasValue)
            .Join(_context.Users, membership => membership.UserId, user => (Guid?)user.Id, (membership, user) => new { Membership = membership, User = user })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new { item.Membership, item.User, Role = role })
            .OrderBy(item => item.User.Pseudo)
            .ThenBy(item => item.User.Tag)
            .Select(item => new ActivityParticipantOption(
                item.Membership.TeamMembershipId,
                item.User.Pseudo,
                item.User.Tag,
                item.Role.Label,
                item.User.Id == team.OwnerUserId))
            .ToListAsync(cancellationToken);

        return new ActivityCreationOptions(team.TeamId, team.Name, team.TimeZoneId, activityTypes, participants);
    }

    public async Task<CreateActivityResult> CreateAsync(CreateActivityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.UserId == Guid.Empty || request.TeamId == Guid.Empty)
        {
            return CreateActivityResult.Failure(["L’utilisateur ou l’équipe est introuvable."]);
        }

        var authorization = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == request.UserId && membership.TeamId == request.TeamId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new { Membership = membership, Team = team })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                CreatorMembershipId = item.Membership.TeamMembershipId,
                item.Team.OwnerUserId,
                item.Team.TimeZoneId,
                RoleCode = role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (authorization is null || authorization.OwnerUserId != request.UserId && authorization.RoleCode != ManagerRoleCode && authorization.RoleCode != CoachRoleCode)
        {
            return CreateActivityResult.Failure(["Vous n’êtes pas autorisé à créer une activité pour cette équipe."]);
        }

        if (request.ActivityTypeId <= 0)
        {
            return CreateActivityResult.Failure(["Le type d’activité est obligatoire."]);
        }

        if (request.PlannedStartLocal == default || request.PlannedEndLocal == default)
        {
            return CreateActivityResult.Failure(["Les dates et heures de début et de fin sont obligatoires."]);
        }

        Guid[] participantMembershipIds = request.ParticipantMembershipIds?
            .Where(identifier => identifier != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        if (participantMembershipIds.Length == 0)
        {
            return CreateActivityResult.Failure(["Sélectionnez au moins un participant."]);
        }

        int activeParticipantCount = await _context.TeamMemberships
            .AsNoTracking()
            .CountAsync(membership =>
                participantMembershipIds.Contains(membership.TeamMembershipId)
                && membership.TeamId == request.TeamId
                && membership.Status == MembershipStatus.Active
                && membership.UserId.HasValue,
                cancellationToken);

        if (activeParticipantCount != participantMembershipIds.Length)
        {
            return CreateActivityResult.Failure(["Un ou plusieurs participants sélectionnés ne sont plus membres actifs de cette équipe."]);
        }

        ActivityType? activityType = await _context.ActivityTypes
            .SingleOrDefaultAsync(item =>
                item.ActivityTypeId == request.ActivityTypeId
                && (item.IsSystem || item.TeamId == request.TeamId),
                cancellationToken);

        if (activityType is null)
        {
            return CreateActivityResult.Failure(["Le type d’activité sélectionné n’est pas disponible pour cette équipe."]);
        }

        TimeZoneInfo timeZone;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(authorization.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return CreateActivityResult.Failure(["Le fuseau horaire de l’équipe est introuvable."]);
        }
        catch (InvalidTimeZoneException)
        {
            return CreateActivityResult.Failure(["Le fuseau horaire de l’équipe n’est pas valide."]);
        }

        DateTime plannedStartLocal = DateTime.SpecifyKind(request.PlannedStartLocal, DateTimeKind.Unspecified);
        DateTime plannedEndLocal = DateTime.SpecifyKind(request.PlannedEndLocal, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(plannedStartLocal) || timeZone.IsInvalidTime(plannedEndLocal))
        {
            return CreateActivityResult.Failure(["L’une des heures choisies n’existe pas dans le fuseau de l’équipe en raison du changement d’heure."]);
        }

        if (timeZone.IsAmbiguousTime(plannedStartLocal) || timeZone.IsAmbiguousTime(plannedEndLocal))
        {
            return CreateActivityResult.Failure(["L’une des heures choisies est ambiguë dans le fuseau de l’équipe en raison du changement d’heure."]);
        }

        DateTimeOffset plannedStartUtc = new(TimeZoneInfo.ConvertTimeToUtc(plannedStartLocal, timeZone));
        DateTimeOffset plannedEndUtc = new(TimeZoneInfo.ConvertTimeToUtc(plannedEndLocal, timeZone));

        if (plannedEndUtc <= plannedStartUtc)
        {
            return CreateActivityResult.Failure(["La fin prévue doit être strictement postérieure au début prévu."]);
        }

        Guid activityId = Guid.NewGuid();
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        TeamActivity activity;
        List<ActivityLink> activityLinks = [];

        try
        {
            activity = new TeamActivity(
                activityId,
                request.TeamId,
                activityType,
                authorization.CreatorMembershipId,
                plannedStartUtc,
                plannedEndUtc,
                authorization.TimeZoneId,
                participantMembershipIds,
                utcNow,
                request.Subtitle,
                request.Description);

            if (activity.RequiresScores)
            {
                activity.UpdateOpponent(request.OpponentName, utcNow);
            }
            else if (!string.IsNullOrWhiteSpace(request.OpponentName))
            {
                return CreateActivityResult.Failure(["L’équipe adverse est disponible uniquement pour une pracc ou un match officiel."]);
            }

            foreach (CreateActivityLinkRequest link in request.Links)
            {
                activityLinks.Add(new ActivityLink(activityId, link.Name, link.Url));
            }
        }
        catch (DomainException)
        {
            return CreateActivityResult.Failure(["Les informations fournies ne permettent pas de créer l’activité."]);
        }

        _context.TeamActivities.Add(activity);
        _context.ActivityLinks.AddRange(activityLinks);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return CreateActivityResult.Failure(["L’activité n’a pas pu être créée. Veuillez réessayer."]);
        }

        return CreateActivityResult.Success(activityId);
    }
}