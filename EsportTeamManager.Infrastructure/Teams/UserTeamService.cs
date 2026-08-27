using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using EsportTeamManager.Infrastructure.Identity;

namespace EsportTeamManager.Infrastructure.Teams;

public sealed class UserTeamService : IUserTeamService
{
    private const int InvitationLimitPerHour = 30;
    private const string CoachRoleCode = "Coach";
    private const string InvitationCreatedActionCode = "TEAM_INVITATION_CREATED";
    private const string ManagerRoleCode = "Manager";
    private const string PlayerRoleCode = "Player";
    private const string TeamCreatedActionCode = "TEAM_CREATED";
    private const string InvitationTargetErrorMessage = "L’invitation n’a pas pu être envoyée. Vérifiez l’identité saisie et le rôle proposé.";
    private const string TeamMemberRoleChangedActionCode = "TEAM_MEMBER_ROLE_CHANGED";
    private const string TeamMemberRemovedActionCode = "TEAM_MEMBER_REMOVED";
    private const string TeamMemberLeftActionCode = "TEAM_MEMBER_LEFT";

    private readonly ApplicationDbContext _context;
    private readonly ILookupNormalizer _lookupNormalizer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UserTeamService> _logger;

    public UserTeamService(ApplicationDbContext context, ILookupNormalizer lookupNormalizer, TimeProvider timeProvider, ILogger<UserTeamService> logger)
    {
        _context = context;
        _lookupNormalizer = lookupNormalizer;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<TeamMembershipActionResult> ChangeMemberRoleAsync(ChangeTeamMemberRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty || request.TeamMembershipId == Guid.Empty || request.NewTeamRoleId <= 0)
        {
            return TeamMembershipActionResult.Denied();
        }

        var actorAccess = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == request.TeamId && membership.UserId == request.ActorUserId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new
            {
                Membership = membership,
                Team = team
            })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                item.Team.OwnerUserId,
                ActorRoleCode = role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (actorAccess is null)
        {
            return TeamMembershipActionResult.Denied();
        }

        bool actorIsOwner = actorAccess.OwnerUserId == request.ActorUserId;
        bool actorIsManager = actorAccess.ActorRoleCode == ManagerRoleCode;

        if (!actorIsOwner && !actorIsManager)
        {
            return TeamMembershipActionResult.Denied();
        }

        var targetData = await _context.TeamMemberships
            .Where(membership => membership.TeamMembershipId == request.TeamMembershipId && membership.TeamId == request.TeamId && membership.Status == MembershipStatus.Active && membership.UserId.HasValue)
            .Join(_context.TeamRoles, membership => membership.TeamRoleId, role => role.TeamRoleId, (membership, role) => new
            {
                Membership = membership,
                CurrentRoleCode = role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (targetData is null)
        {
            return TeamMembershipActionResult.Denied();
        }

        var newRole = await _context.TeamRoles
            .AsNoTracking()
            .Where(role => role.TeamRoleId == request.NewTeamRoleId && role.IsSystem)
            .Select(role => new
            {
                role.TeamRoleId,
                role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (newRole is null)
        {
            return TeamMembershipActionResult.Failure(["Le rôle sélectionné n’est pas valide."]);
        }

        bool targetIsOwner = targetData.Membership.UserId == actorAccess.OwnerUserId;

        if (!actorIsOwner)
        {
            bool targetHasManageableRole = targetData.CurrentRoleCode == CoachRoleCode || targetData.CurrentRoleCode == PlayerRoleCode;
            bool newRoleIsAllowed = newRole.Code == CoachRoleCode || newRole.Code == PlayerRoleCode;

            if (targetIsOwner || !targetHasManageableRole || !newRoleIsAllowed)
            {
                return TeamMembershipActionResult.Denied();
            }
        }

        if (targetData.Membership.TeamRoleId == newRole.TeamRoleId)
        {
            return TeamMembershipActionResult.Success();
        }

        try
        {
            targetData.Membership.ChangeRole(newRole.TeamRoleId);
        }
        catch (DomainException)
        {
            return TeamMembershipActionResult.Failure(["Le rôle du membre n’a pas pu être modifié."]);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        ActionTrace actionTrace = new(request.ActorUserId, request.TeamId, TeamMemberRoleChangedActionCode, nameof(TeamMembership), request.TeamMembershipId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Team member role change persistence failed for actor {ActorUserId}, team {TeamId}, and membership {TeamMembershipId}.", request.ActorUserId, request.TeamId, request.TeamMembershipId);

            return TeamMembershipActionResult.Failure(["Le rôle du membre n’a pas pu être enregistré. Veuillez réessayer."]);
        }

        return TeamMembershipActionResult.Success();
    }

    public async Task<CreateTeamResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.OwnerUserId == Guid.Empty)
        {
            return CreateTeamResult.Failure(["L’utilisateur connecté est introuvable."]);
        }

        bool ownerExists = await _context.Users.AnyAsync(user => user.Id == request.OwnerUserId, cancellationToken);

        if (!ownerExists)
        {
            return CreateTeamResult.Failure(["L’utilisateur connecté est introuvable."]);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return CreateTeamResult.Failure(["Le nom de l’équipe est obligatoire."]);
        }

        if (!IsValidIanaTimeZone(request.TimeZoneId))
        {
            return CreateTeamResult.Failure(["Le fuseau horaire sélectionné n’est pas valide."]);
        }

        int? playerRoleId = await _context.TeamRoles
            .Where(role => role.IsSystem && role.Code == PlayerRoleCode)
            .Select(role => (int?)role.TeamRoleId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!playerRoleId.HasValue)
        {
            return CreateTeamResult.Failure(["Le rôle joueur de référence est indisponible."]);
        }

        Guid teamId = Guid.NewGuid();
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        Team team;
        TeamMembership membership;

        try
        {
            team = new Team(teamId, request.OwnerUserId, request.Name, utcNow, request.Tag, null, request.TimeZoneId);
            membership = new TeamMembership(Guid.NewGuid(), teamId, request.OwnerUserId, playerRoleId.Value, utcNow);
        }
        catch (DomainException)
        {
            return CreateTeamResult.Failure(["Les informations fournies ne permettent pas de créer l’équipe."]);
        }

        _context.Teams.Add(team);
        _context.TeamMemberships.Add(membership);

        ActionTrace actionTrace = new(request.OwnerUserId, teamId, TeamCreatedActionCode, nameof(Team), teamId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Team creation persistence failed for actor {ActorUserId} and team {TeamId}.", request.OwnerUserId, teamId);

            return CreateTeamResult.Failure(["L’équipe n’a pas pu être créée. Veuillez réessayer."]);
        }

        return CreateTeamResult.Success(teamId);
    }

    public async Task<TeamManagementDetails?> GetManagementDetailsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty || teamId == Guid.Empty)
        {
            return null;
        }

        var teamData = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId && membership.UserId == userId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new
            {
                Membership = membership,
                Team = team
            })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                item.Team.TeamId,
                item.Team.OwnerUserId,
                item.Team.Name,
                item.Team.Tag,
                item.Team.Description,
                item.Team.TimeZoneId,
                CurrentUserRoleCode = role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (teamData is null)
        {
            return null;
        }

        bool currentUserIsOwner = teamData.OwnerUserId == userId;
        bool currentUserIsManager = teamData.CurrentUserRoleCode == ManagerRoleCode;
        bool currentUserCanInviteMembers = currentUserIsOwner || currentUserIsManager;
        IReadOnlyCollection<TeamRoleOption> availableInvitationRoles = [];

        if (currentUserCanInviteMembers)
        {
            IQueryable<TeamRole> roleQuery = _context.TeamRoles
                .AsNoTracking()
                .Where(role => role.IsSystem);

            if (!currentUserIsOwner)
            {
                roleQuery = roleQuery.Where(role => role.Code == CoachRoleCode || role.Code == PlayerRoleCode);
            }

            availableInvitationRoles = await roleQuery
                .OrderBy(role => role.TeamRoleId)
                .Select(role => new TeamRoleOption(role.TeamRoleId, role.Label))
                .ToListAsync(cancellationToken);
        }

        var memberRowsQuery = _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId && membership.Status == MembershipStatus.Active && membership.UserId.HasValue)
            .Join(_context.Users, membership => membership.UserId!.Value, user => user.Id, (membership, user) => new
            {
                Membership = membership,
                User = user
            })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                item.Membership.TeamMembershipId,
                UserId = item.User.Id,
                item.User.Pseudo,
                item.User.Tag,
                role.TeamRoleId,
                RoleLabel = role.Label,
                RoleCode = role.Code,
                item.Membership.JoinedAtUtc
            });

        bool useClientSideMemberOrdering = string.Equals(_context.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal);
        var memberRows = useClientSideMemberOrdering
            ? await memberRowsQuery.ToListAsync(cancellationToken)
            : await memberRowsQuery
                .OrderBy(member => member.JoinedAtUtc)
                .ThenBy(member => member.Pseudo)
                .ThenBy(member => member.Tag)
                .ToListAsync(cancellationToken);
        var orderedMemberRows = memberRows.AsEnumerable();

        if (useClientSideMemberOrdering)
        {
            orderedMemberRows = memberRows
                .OrderBy(member => member.JoinedAtUtc)
                .ThenBy(member => member.Pseudo)
                .ThenBy(member => member.Tag);
        }

        IReadOnlyCollection<TeamMemberSummary> members =
        [
            .. orderedMemberRows.Select(member => new TeamMemberSummary(
                member.TeamMembershipId,
                member.Pseudo,
                member.Tag,
                member.TeamRoleId,
                member.RoleLabel,
                member.UserId == teamData.OwnerUserId,
                currentUserIsOwner || (currentUserIsManager && member.UserId != teamData.OwnerUserId && (member.RoleCode == CoachRoleCode || member.RoleCode == PlayerRoleCode)),
                currentUserIsOwner && member.UserId != userId,
                member.JoinedAtUtc))
        ];

        return new TeamManagementDetails(teamData.TeamId, teamData.Name, teamData.Tag, teamData.Description, teamData.TimeZoneId, currentUserIsOwner, currentUserCanInviteMembers, availableInvitationRoles, members);
    }

    public async Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
        {
            return [];
        }

        return await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new { Membership = membership, Team = team })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new { item.Team, Role = role })
            .OrderBy(item => item.Team.Name)
            .ThenBy(item => item.Team.Tag)
            .Select(item => new UserTeamSummary(item.Team.TeamId, item.Team.Name, item.Team.Tag, item.Team.TimeZoneId, item.Role.Label, item.Team.OwnerUserId == userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<InviteTeamMemberResult> InviteMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.SenderUserId == Guid.Empty || request.TeamId == Guid.Empty)
        {
            return InviteTeamMemberResult.Denied();
        }

        var senderAccess = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == request.TeamId && membership.UserId == request.SenderUserId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new
            {
                Membership = membership,
                Team = team
            })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new
            {
                item.Team.OwnerUserId,
                CurrentUserRoleCode = role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (senderAccess is null)
        {
            return InviteTeamMemberResult.Denied();
        }

        bool senderIsOwner = senderAccess.OwnerUserId == request.SenderUserId;
        bool senderIsManager = senderAccess.CurrentUserRoleCode == ManagerRoleCode;

        if (!senderIsOwner && !senderIsManager)
        {
            return InviteTeamMemberResult.Denied();
        }

        var proposedRole = await _context.TeamRoles
            .AsNoTracking()
            .Where(role => role.TeamRoleId == request.ProposedTeamRoleId && role.IsSystem)
            .Select(role => new
            {
                role.TeamRoleId,
                role.Code
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (proposedRole is null)
        {
            return InviteTeamMemberResult.Failure([InvitationTargetErrorMessage]);
        }

        if (!senderIsOwner && proposedRole.Code == ManagerRoleCode)
        {
            return InviteTeamMemberResult.Denied();
        }

        if (!TryParseRecipientIdentity(request.RecipientIdentity, out string recipientPseudo, out string recipientTag))
        {
            return InviteTeamMemberResult.Failure([InvitationTargetErrorMessage]);
        }

        string? normalizedRecipientUserName = _lookupNormalizer.NormalizeName($"{recipientPseudo}#{recipientTag}");

        if (string.IsNullOrWhiteSpace(normalizedRecipientUserName))
        {
            return InviteTeamMemberResult.Failure([InvitationTargetErrorMessage]);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        DateTimeOffset invitationWindowStartUtc = utcNow.AddHours(-1);
        IQueryable<Invitation> senderInvitations = _context.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.SenderUserId == request.SenderUserId);
        int recentInvitationCount;

        if (string.Equals(_context.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            IReadOnlyCollection<DateTimeOffset> senderInvitationDates = await senderInvitations
                .Select(invitation => invitation.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            recentInvitationCount = senderInvitationDates.Count(createdAtUtc => createdAtUtc >= invitationWindowStartUtc);
        }
        else
        {
            recentInvitationCount = await senderInvitations.CountAsync(invitation => invitation.CreatedAtUtc >= invitationWindowStartUtc, cancellationToken);
        }

        if (recentInvitationCount >= InvitationLimitPerHour)
        {
            return InviteTeamMemberResult.Failure(["Vous avez atteint la limite de 30 invitations par heure. Veuillez réessayer plus tard."]);
        }

        ApplicationUser? recipient = await _context.Users
            .SingleOrDefaultAsync(user => user.NormalizedUserName == normalizedRecipientUserName && user.AccountStatus == AccountStatus.Active, cancellationToken);

        if (recipient is null)
        {
            return InviteTeamMemberResult.Failure([InvitationTargetErrorMessage]);
        }

        bool recipientIsActiveMember = await _context.TeamMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.TeamId == request.TeamId && membership.UserId == recipient.Id && membership.Status == MembershipStatus.Active, cancellationToken);
        bool recipientHasPendingInvitation = await _context.Invitations
            .AsNoTracking()
            .AnyAsync(invitation => invitation.TeamId == request.TeamId && invitation.RecipientUserId == recipient.Id && invitation.Status == RequestStatus.Pending, cancellationToken);

        if (recipientIsActiveMember || recipientHasPendingInvitation)
        {
            return InviteTeamMemberResult.Failure([InvitationTargetErrorMessage]);
        }

        Guid invitationId = Guid.NewGuid();
        Invitation invitation;

        try
        {
            invitation = new Invitation(invitationId, request.TeamId, request.SenderUserId, recipient.Id, proposedRole.TeamRoleId, utcNow);
        }
        catch (DomainException)
        {
            return InviteTeamMemberResult.Failure([InvitationTargetErrorMessage]);
        }

        _context.Invitations.Add(invitation);

        ActionTrace actionTrace = new(request.SenderUserId, request.TeamId, InvitationCreatedActionCode, nameof(Invitation), invitationId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Team invitation persistence failed for actor {ActorUserId}, team {TeamId}, and invitation {InvitationId}.", request.SenderUserId, request.TeamId, invitationId);

            return InviteTeamMemberResult.Failure(["L’invitation n’a pas pu être enregistrée. Veuillez réessayer."]);
        }

        return InviteTeamMemberResult.Success(invitationId);
    }

    public async Task<TeamMembershipActionResult> LeaveTeamAsync(LeaveTeamRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.UserId == Guid.Empty || request.TeamId == Guid.Empty)
        {
            return TeamMembershipActionResult.Denied();
        }

        var membershipData = await _context.TeamMemberships
            .Where(membership => membership.TeamId == request.TeamId && membership.UserId == request.UserId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new
            {
                Membership = membership,
                team.OwnerUserId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (membershipData is null)
        {
            return TeamMembershipActionResult.Denied();
        }

        if (membershipData.OwnerUserId == request.UserId)
        {
            return TeamMembershipActionResult.Failure(["Le propriétaire doit transférer la propriété ou supprimer l’équipe avant de pouvoir la quitter."]);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        try
        {
            membershipData.Membership.Leave(utcNow);
        }
        catch (DomainException)
        {
            return TeamMembershipActionResult.Failure(["L’équipe n’a pas pu être quittée."]);
        }

        ActionTrace actionTrace = new(request.UserId, request.TeamId, TeamMemberLeftActionCode, nameof(TeamMembership), membershipData.Membership.TeamMembershipId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Team departure persistence failed for user {UserId}, team {TeamId}, and membership {TeamMembershipId}.", request.UserId, request.TeamId, membershipData.Membership.TeamMembershipId);

            return TeamMembershipActionResult.Failure(["Le départ de l’équipe n’a pas pu être enregistré. Veuillez réessayer."]);
        }

        return TeamMembershipActionResult.Success();
    }

    public async Task<TeamMembershipActionResult> RemoveMemberAsync(RemoveTeamMemberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty || request.TeamMembershipId == Guid.Empty)
        {
            return TeamMembershipActionResult.Denied();
        }

        bool actorIsOwner = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == request.TeamId && membership.UserId == request.ActorUserId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => team.OwnerUserId)
            .AnyAsync(ownerUserId => ownerUserId == request.ActorUserId, cancellationToken);

        if (!actorIsOwner)
        {
            return TeamMembershipActionResult.Denied();
        }

        TeamMembership? targetMembership = await _context.TeamMemberships
            .SingleOrDefaultAsync(membership => membership.TeamMembershipId == request.TeamMembershipId && membership.TeamId == request.TeamId && membership.Status == MembershipStatus.Active && membership.UserId.HasValue, cancellationToken);

        if (targetMembership is null || targetMembership.UserId == request.ActorUserId)
        {
            return TeamMembershipActionResult.Denied();
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        try
        {
            targetMembership.Remove(utcNow);
        }
        catch (DomainException)
        {
            return TeamMembershipActionResult.Failure(["Le membre n’a pas pu être exclu."]);
        }

        ActionTrace actionTrace = new(request.ActorUserId, request.TeamId, TeamMemberRemovedActionCode, nameof(TeamMembership), request.TeamMembershipId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Team member removal persistence failed for actor {ActorUserId}, team {TeamId}, and membership {TeamMembershipId}.", request.ActorUserId, request.TeamId, request.TeamMembershipId);

            return TeamMembershipActionResult.Failure(["L’exclusion du membre n’a pas pu être enregistrée. Veuillez réessayer."]);
        }

        return TeamMembershipActionResult.Success();
    }

    private static bool TryParseRecipientIdentity(string recipientIdentity, out string pseudo, out string tag)
    {
        pseudo = string.Empty;
        tag = string.Empty;

        if (string.IsNullOrWhiteSpace(recipientIdentity))
        {
            return false;
        }

        string normalizedIdentity = recipientIdentity.Trim();
        int separatorIndex = normalizedIdentity.LastIndexOf('#');

        if (separatorIndex <= 0 || separatorIndex == normalizedIdentity.Length - 1)
        {
            return false;
        }

        pseudo = normalizedIdentity[..separatorIndex].Trim();
        tag = normalizedIdentity[(separatorIndex + 1)..].Trim();

        if (pseudo.Length < 3 || pseudo.Length > 20)
        {
            return false;
        }

        if (tag.Length < 3 || tag.Length > 5 || !tag.All(char.IsLetterOrDigit))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidIanaTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());

            return timeZone.HasIanaId;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}