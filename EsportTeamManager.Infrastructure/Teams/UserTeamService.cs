using EsportTeamManager.Application.Teams;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EsportTeamManager.Infrastructure.Teams;

public sealed class UserTeamService : IUserTeamService
{
    private const string PlayerRoleCode = "Player";

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public UserTeamService(ApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
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

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return CreateTeamResult.Failure(["L’équipe n’a pas pu être créée. Veuillez réessayer."]);
        }

        return CreateTeamResult.Success(teamId);
    }

    public async Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty)
        {
            return Array.Empty<UserTeamSummary>();
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