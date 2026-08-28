using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using EsportTeamManager.Infrastructure.Identity;
using EsportTeamManager.Application.Images;

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
    private const string OwnershipTransferInitiatedActionCode = "TEAM_OWNERSHIP_TRANSFER_INITIATED";
    private const string OwnershipTransferAcceptedActionCode = "TEAM_OWNERSHIP_TRANSFER_ACCEPTED";
    private const string OwnershipTransferCancelledActionCode = "TEAM_OWNERSHIP_TRANSFER_CANCELLED";
    private const string OwnershipTransferRefusedActionCode = "TEAM_OWNERSHIP_TRANSFER_REFUSED";
    private const string TeamInformationUpdatedActionCode = "TEAM_INFORMATION_UPDATED";

    private readonly ApplicationDbContext _context;
    private readonly ILookupNormalizer _lookupNormalizer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UserTeamService> _logger;
    private readonly IPrivateImageService _privateImageService;

    public UserTeamService(ApplicationDbContext context, ILookupNormalizer lookupNormalizer, IPrivateImageService privateImageService, TimeProvider timeProvider, ILogger<UserTeamService> logger)
    {
        _context = context;
        _lookupNormalizer = lookupNormalizer;
        _privateImageService = privateImageService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<OwnershipTransferActionResult> AcceptOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        return ResolveOwnershipTransferAsync(request, RequestStatus.Accepted, cancellationToken);
    }

    public Task<OwnershipTransferActionResult> CancelOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        return ResolveOwnershipTransferAsync(request, RequestStatus.Cancelled, cancellationToken);
    }

    public Task<OwnershipTransferActionResult> RefuseOwnershipTransferAsync(ResolveOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        return ResolveOwnershipTransferAsync(request, RequestStatus.Refused, cancellationToken);
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

    public async Task<UpdateTeamInformationResult> UpdateInformationAsync(UpdateTeamInformationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty)
        {
            return UpdateTeamInformationResult.Denied();
        }

        Team? team = await _context.Teams
            .SingleOrDefaultAsync(candidate => candidate.TeamId == request.TeamId && candidate.OwnerUserId == request.ActorUserId, cancellationToken);

        if (team is null)
        {
            return UpdateTeamInformationResult.Denied();
        }

        List<string> errors = [];
        string normalizedName = request.Name?.Trim() ?? string.Empty;
        string? normalizedTag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim();
        string? normalizedDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (normalizedName.Length < 3 || normalizedName.Length > 50)
        {
            errors.Add("Le nom de l’équipe doit contenir entre 3 et 50 caractères.");
        }

        if (normalizedTag is not null && (normalizedTag.Length < 2 || normalizedTag.Length > 6))
        {
            errors.Add("Le tag de l’équipe doit contenir entre 2 et 6 caractères.");
        }

        if (normalizedDescription is not null && normalizedDescription.Length > 500)
        {
            errors.Add("La description de l’équipe ne peut pas dépasser 500 caractères.");
        }

        if (!IsValidIanaTimeZone(request.TimeZoneId))
        {
            errors.Add("Le fuseau horaire sélectionné n’est pas valide.");
        }

        bool hasLogoFileName = !string.IsNullOrWhiteSpace(request.LogoFileName);

        if (hasLogoFileName != request.HasLogo)
        {
            errors.Add("Le fichier du logo est invalide.");
        }

        if (errors.Count > 0)
        {
            return UpdateTeamInformationResult.Failure(errors);
        }

        try
        {
            team.UpdateInformation(normalizedName, normalizedTag, normalizedDescription, request.TimeZoneId);
        }
        catch (DomainException)
        {
            return UpdateTeamInformationResult.Failure(["Les informations de l’équipe ne sont pas valides."]);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        ActionTrace actionTrace = new(request.ActorUserId, request.TeamId, TeamInformationUpdatedActionCode, nameof(Team), request.TeamId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        if (request.HasLogo)
        {
            ReplaceTeamLogoRequest logoRequest = new(request.ActorUserId, request.TeamId, request.LogoFileName!, request.LogoContent!);
            StorePrivateImageResult logoResult = await _privateImageService.ReplaceTeamLogoAsync(logoRequest, cancellationToken);

            if (!logoResult.Succeeded)
            {
                return UpdateTeamInformationResult.Failure(logoResult.Errors);
            }

            return UpdateTeamInformationResult.Success();
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Team information update persistence failed for actor {ActorUserId} and team {TeamId}.", request.ActorUserId, request.TeamId);

            return UpdateTeamInformationResult.Failure(["Les informations de l’équipe n’ont pas pu être enregistrées. Veuillez réessayer."]);
        }

        return UpdateTeamInformationResult.Success();
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

        bool hasLogo = await _context.ImageFiles
            .AsNoTracking()
            .AnyAsync(image => image.TeamLogoForTeamId == teamId, cancellationToken);

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

        PendingOwnershipTransferSummary? pendingOwnershipTransfer = null;

        if (currentUserIsOwner)
        {
            var pendingTransfer = await _context.OwnershipTransfers
                .AsNoTracking()
                .Where(transfer => transfer.TeamId == teamId && transfer.Status == RequestStatus.Pending)
                .Join(_context.TeamMemberships, transfer => transfer.RecipientMembershipId, membership => membership.TeamMembershipId, (transfer, membership) => new
                {
                    Transfer = transfer,
                    Membership = membership
                })
                .Where(item => item.Membership.UserId.HasValue)
                .Join(_context.Users, item => item.Membership.UserId!.Value, user => user.Id, (item, user) => new
                {
                    item.Transfer.OwnershipTransferId,
                    item.Transfer.RecipientMembershipId,
                    user.Pseudo,
                    user.Tag,
                    item.Transfer.CreatedAtUtc
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (pendingTransfer is not null)
            {
                pendingOwnershipTransfer = new PendingOwnershipTransferSummary(
                    pendingTransfer.OwnershipTransferId,
                    pendingTransfer.RecipientMembershipId,
                    pendingTransfer.Pseudo,
                    pendingTransfer.Tag,
                    pendingTransfer.CreatedAtUtc);
            }
        }

        return new TeamManagementDetails(teamData.TeamId, teamData.Name, teamData.Tag, teamData.Description, teamData.TimeZoneId, currentUserIsOwner, currentUserCanInviteMembers, availableInvitationRoles, members, pendingOwnershipTransfer, hasLogo);
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

    public async Task<OwnershipTransferActionResult> InitiateOwnershipTransferAsync(InitiateOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.InitiatorUserId == Guid.Empty || request.TeamId == Guid.Empty || request.RecipientMembershipId == Guid.Empty)
        {
            return OwnershipTransferActionResult.Denied();
        }

        var initiatorData = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == request.TeamId && membership.UserId == request.InitiatorUserId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new
            {
                membership.TeamMembershipId,
                team.OwnerUserId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (initiatorData is null || initiatorData.OwnerUserId != request.InitiatorUserId)
        {
            return OwnershipTransferActionResult.Denied();
        }

        var recipientData = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamMembershipId == request.RecipientMembershipId && membership.TeamId == request.TeamId && membership.Status == MembershipStatus.Active && membership.UserId.HasValue)
            .Select(membership => new
            {
                membership.TeamMembershipId,
                RecipientUserId = membership.UserId!.Value
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (recipientData is null || recipientData.RecipientUserId == request.InitiatorUserId)
        {
            return OwnershipTransferActionResult.Failure(["Le membre sélectionné ne peut pas recevoir la propriété."]);
        }

        bool pendingTransferExists = await _context.OwnershipTransfers
            .AsNoTracking()
            .AnyAsync(transfer => transfer.TeamId == request.TeamId && transfer.Status == RequestStatus.Pending, cancellationToken);

        if (pendingTransferExists)
        {
            return OwnershipTransferActionResult.Failure(["Un transfert de propriété est déjà en attente pour cette équipe."]);
        }

        Guid ownershipTransferId = Guid.NewGuid();
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        OwnershipTransfer ownershipTransfer;

        try
        {
            ownershipTransfer = new OwnershipTransfer(ownershipTransferId, request.TeamId, initiatorData.TeamMembershipId, recipientData.TeamMembershipId, utcNow);
        }
        catch (DomainException)
        {
            return OwnershipTransferActionResult.Failure(["Le transfert de propriété n’a pas pu être créé."]);
        }

        Notification notification = Notification.CreateForOwnershipTransfer(Guid.NewGuid(), recipientData.RecipientUserId, ownershipTransferId, utcNow);
        ActionTrace actionTrace = new(request.InitiatorUserId, request.TeamId, OwnershipTransferInitiatedActionCode, nameof(OwnershipTransfer), ownershipTransferId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.OwnershipTransfers.Add(ownershipTransfer);
        _context.Notifications.Add(notification);
        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Ownership transfer initiation persistence failed for initiator {InitiatorUserId}, team {TeamId}, recipient membership {RecipientMembershipId}, and transfer {OwnershipTransferId}.", request.InitiatorUserId, request.TeamId, request.RecipientMembershipId, ownershipTransferId);

            return OwnershipTransferActionResult.Failure(["Le transfert de propriété n’a pas pu être enregistré. Veuillez réessayer."]);
        }

        return OwnershipTransferActionResult.Success(ownershipTransferId);
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

        Notification notification = Notification.CreateForInvitation(Guid.NewGuid(), recipient.Id, invitationId, utcNow);
        ActionTrace actionTrace = new(request.SenderUserId, request.TeamId, InvitationCreatedActionCode, nameof(Invitation), invitationId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.Invitations.Add(invitation);
        _context.Notifications.Add(notification);
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

    private async Task<OwnershipTransferActionResult> ResolveOwnershipTransferAsync(ResolveOwnershipTransferRequest request, RequestStatus finalStatus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty || request.OwnershipTransferId == Guid.Empty)
        {
            return OwnershipTransferActionResult.Denied();
        }

        if (finalStatus != RequestStatus.Accepted && finalStatus != RequestStatus.Refused && finalStatus != RequestStatus.Cancelled)
        {
            return OwnershipTransferActionResult.Denied();
        }

        var transferData = await _context.OwnershipTransfers
            .Where(transfer => transfer.OwnershipTransferId == request.OwnershipTransferId && transfer.TeamId == request.TeamId)
            .Join(_context.TeamMemberships, transfer => transfer.InitiatorMembershipId, initiatorMembership => initiatorMembership.TeamMembershipId, (transfer, initiatorMembership) => new
            {
                Transfer = transfer,
                InitiatorMembership = initiatorMembership
            })
            .Join(_context.TeamMemberships, item => item.Transfer.RecipientMembershipId, recipientMembership => recipientMembership.TeamMembershipId, (item, recipientMembership) => new
            {
                item.Transfer,
                item.InitiatorMembership,
                RecipientMembership = recipientMembership
            })
            .Join(_context.Teams, item => item.Transfer.TeamId, team => team.TeamId, (item, team) => new
            {
                item.Transfer,
                Team = team,
                InitiatorUserId = item.InitiatorMembership.UserId,
                InitiatorStatus = item.InitiatorMembership.Status,
                RecipientUserId = item.RecipientMembership.UserId,
                RecipientStatus = item.RecipientMembership.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (transferData is null || !transferData.InitiatorUserId.HasValue || !transferData.RecipientUserId.HasValue)
        {
            return OwnershipTransferActionResult.Denied();
        }

        bool actorIsRecipient = transferData.RecipientUserId.Value == request.ActorUserId;
        bool actorIsCurrentOwner = transferData.Team.OwnerUserId == request.ActorUserId;
        bool actorIsInitiator = transferData.InitiatorUserId.Value == request.ActorUserId;

        if (finalStatus == RequestStatus.Cancelled)
        {
            if (!actorIsCurrentOwner || !actorIsInitiator)
            {
                return OwnershipTransferActionResult.Denied();
            }
        }
        else if (!actorIsRecipient)
        {
            return OwnershipTransferActionResult.Denied();
        }

        if (transferData.Transfer.Status != RequestStatus.Pending)
        {
            return OwnershipTransferActionResult.Failure(["Ce transfert de propriété n’est plus en attente."]);
        }

        if (finalStatus == RequestStatus.Accepted)
        {
            bool initiatorStillOwnsTeam = transferData.Team.OwnerUserId == transferData.InitiatorUserId.Value;
            bool membershipsRemainActive = transferData.InitiatorStatus == MembershipStatus.Active && transferData.RecipientStatus == MembershipStatus.Active;

            if (!initiatorStillOwnsTeam || !membershipsRemainActive)
            {
                return OwnershipTransferActionResult.Failure(["Ce transfert de propriété ne peut plus être accepté."]);
            }
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        try
        {
            if (finalStatus == RequestStatus.Accepted)
            {
                transferData.Transfer.Accept(utcNow);
                transferData.Team.TransferOwnership(transferData.RecipientUserId.Value);
            }
            else if (finalStatus == RequestStatus.Refused)
            {
                transferData.Transfer.Refuse(utcNow);
            }
            else
            {
                transferData.Transfer.Cancel(utcNow);
            }
        }
        catch (DomainException)
        {
            return OwnershipTransferActionResult.Failure(["Le transfert de propriété n’a pas pu être traité."]);
        }

        string actionCode = finalStatus switch
        {
            RequestStatus.Accepted => OwnershipTransferAcceptedActionCode,
            RequestStatus.Refused => OwnershipTransferRefusedActionCode,
            _ => OwnershipTransferCancelledActionCode
        };
        ActionTrace actionTrace = new(request.ActorUserId, request.TeamId, actionCode, nameof(OwnershipTransfer), request.OwnershipTransferId.ToString(), TraceOutcome.Succeeded, utcNow);

        _context.ActionTraces.Add(actionTrace);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(exception, "Ownership transfer {OwnershipTransferId} was resolved concurrently for team {TeamId}.", request.OwnershipTransferId, request.TeamId);

            return OwnershipTransferActionResult.Failure(["Ce transfert de propriété a déjà été traité. Rechargez la page."]);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Ownership transfer resolution persistence failed for actor {ActorUserId}, team {TeamId}, transfer {OwnershipTransferId}, and status {FinalStatus}.", request.ActorUserId, request.TeamId, request.OwnershipTransferId, finalStatus);

            return OwnershipTransferActionResult.Failure(["Le transfert de propriété n’a pas pu être enregistré. Veuillez réessayer."]);
        }

        return OwnershipTransferActionResult.Success(request.OwnershipTransferId);
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