using EsportTeamManager.Application.Images;
using EsportTeamManager.Application.Strategies;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace EsportTeamManager.Infrastructure.Strategies;

public sealed class StrategyEditingService : IStrategyEditingService
{
    private const string ManagerRoleCode = "Manager";
    private const string CoachRoleCode = "Coach";
    private const string StrategyCreatedActionCode = "STRATEGY_CREATED";
    private const string StrategyUpdatedActionCode = "STRATEGY_UPDATED";
    private const string StrategyDeletedActionCode = "STRATEGY_DELETED";

    private readonly ApplicationDbContext _context;
    private readonly IPrivateImageService _privateImageService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StrategyEditingService> _logger;

    public StrategyEditingService(ApplicationDbContext context, IPrivateImageService privateImageService, TimeProvider timeProvider, ILogger<StrategyEditingService> logger)
    {
        _context = context;
        _privateImageService = privateImageService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<bool> CanManageAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StrategyAuthorization? authorization = await GetAuthorizationAsync(userId, teamId, cancellationToken);

        return authorization?.CanManage == true;
    }

    public async Task<StrategyEditingDetails?> GetAsync(Guid userId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (strategyId == Guid.Empty)
        {
            return null;
        }

        StrategyAuthorization? authorization = await GetAuthorizationAsync(userId, teamId, cancellationToken);

        if (authorization is null)
        {
            return null;
        }

        return await _context.Strategies
                    .AsNoTracking()
            .Where(strategy => strategy.StrategyId == strategyId && strategy.TeamId == teamId)
            .Join(_context.Teams, strategy => strategy.TeamId, team => team.TeamId, (strategy, team) => new { Strategy = strategy, Team = team })
            .Select(item => new StrategyEditingDetails(
                item.Strategy.StrategyId,
                item.Strategy.TeamId,
                item.Team.Name,
                item.Strategy.MapId,
                item.Strategy.Name,
                item.Strategy.Side,
                item.Strategy.Description,
                item.Strategy.ExternalUrl,
                item.Strategy.IsActive,
                _context.ImageFiles.Any(image => image.StrategyImageForStrategyId == item.Strategy.StrategyId),
                _context.ActivityStrategies.Count(activityStrategy => activityStrategy.StrategyId == item.Strategy.StrategyId),
                authorization.CanManage))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<SaveStrategyResult> CreateAsync(CreateStrategyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty)
        {
            return SaveStrategyResult.Failure(["L’utilisateur ou l’équipe est introuvable."]);
        }

        StrategyAuthorization? authorization = await GetAuthorizationAsync(request.ActorUserId, request.TeamId, cancellationToken);

        if (authorization?.CanManage != true)
        {
            return SaveStrategyResult.Failure(["Vous n’êtes pas autorisé à créer une stratégie pour cette équipe."]);
        }

        if (!await MapExistsAsync(request.MapId, cancellationToken))
        {
            return SaveStrategyResult.Failure(["La carte sélectionnée est invalide."]);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        Strategy strategy;

        try
        {
            strategy = new Strategy(
                request.TeamId,
                authorization.MembershipId,
                request.MapId,
                request.Name,
                request.Side,
                request.Description,
                request.ExternalUrl,
                utcNow);

            strategy.SetActive(request.IsActive, utcNow);
            strategy.EnsureHasContent(request.Image is not null);
        }
        catch (DomainException)
        {
            return SaveStrategyResult.Failure(["Les informations fournies ne permettent pas de créer la stratégie."]);
        }

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.Strategies.Add(strategy);
        _context.ActionTraces.Add(new ActionTrace(
            request.ActorUserId,
            request.TeamId,
            StrategyCreatedActionCode,
            nameof(Strategy),
            strategy.StrategyId.ToString(),
            TraceOutcome.Succeeded,
            utcNow));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            if (request.Image is not null)
            {
                StorePrivateImageRequest imageRequest = new(strategy.StrategyId, request.Image.OriginalFileName, request.Image.Content);
                StorePrivateImageResult imageResult = await _privateImageService.StoreStrategyImageAsync(imageRequest, cancellationToken);

                if (!imageResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();

                    return SaveStrategyResult.Failure(imageResult.Errors);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            _logger.LogError(exception, "Strategy creation persistence failed for actor {ActorUserId}, team {TeamId} and strategy {StrategyId}.", request.ActorUserId, request.TeamId, strategy.StrategyId);

            return SaveStrategyResult.Failure(["La stratégie n’a pas pu être créée. Veuillez réessayer."]);
        }

        return SaveStrategyResult.Success(strategy.StrategyId);
    }

    public async Task<SaveStrategyResult> UpdateAsync(UpdateStrategyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty || request.StrategyId == Guid.Empty)
        {
            return SaveStrategyResult.Failure(["La stratégie est introuvable."]);
        }

        StrategyAuthorization? authorization = await GetAuthorizationAsync(request.ActorUserId, request.TeamId, cancellationToken);

        if (authorization?.CanManage != true)
        {
            return SaveStrategyResult.Failure(["Vous n’êtes pas autorisé à modifier cette stratégie."]);
        }

        Strategy? strategy = await _context.Strategies
            .SingleOrDefaultAsync(item => item.StrategyId == request.StrategyId && item.TeamId == request.TeamId, cancellationToken);

        if (strategy is null)
        {
            return SaveStrategyResult.Failure(["La stratégie est introuvable."]);
        }

        if (!await MapExistsAsync(request.MapId, cancellationToken))
        {
            return SaveStrategyResult.Failure(["La carte sélectionnée est invalide."]);
        }

        bool hasStoredImage = await _context.ImageFiles
            .AsNoTracking()
            .AnyAsync(image => image.StrategyImageForStrategyId == request.StrategyId, cancellationToken);

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        try
        {
            strategy.Rename(request.Name, utcNow);
            strategy.ChangeMap(request.MapId, utcNow);
            strategy.ChangeSide(request.Side, utcNow);
            strategy.UpdateContent(request.Description, request.ExternalUrl, utcNow);
            strategy.SetActive(request.IsActive, utcNow);
            strategy.EnsureHasContent(hasStoredImage || request.Image is not null);
        }
        catch (DomainException)
        {
            return SaveStrategyResult.Failure(["Les informations fournies ne permettent pas de modifier la stratégie."]);
        }

        _context.ActionTraces.Add(new ActionTrace(
            request.ActorUserId,
            request.TeamId,
            StrategyUpdatedActionCode,
            nameof(Strategy),
            strategy.StrategyId.ToString(),
            TraceOutcome.Succeeded,
            utcNow));

        try
        {
            if (request.Image is not null)
            {
                ReplaceStrategyImageRequest imageRequest = new(
                    request.ActorUserId,
                    request.TeamId,
                    request.StrategyId,
                    request.Image.OriginalFileName,
                    request.Image.Content);

                StorePrivateImageResult imageResult = await _privateImageService.ReplaceStrategyImageAsync(imageRequest, cancellationToken);

                if (!imageResult.Succeeded)
                {
                    _context.ChangeTracker.Clear();

                    return SaveStrategyResult.Failure(imageResult.Errors);
                }
            }
            else
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception)
        {
            _context.ChangeTracker.Clear();

            _logger.LogError(exception, "Strategy update persistence failed for actor {ActorUserId}, team {TeamId} and strategy {StrategyId}.", request.ActorUserId, request.TeamId, request.StrategyId);

            return SaveStrategyResult.Failure(["La stratégie n’a pas pu être modifiée. Veuillez réessayer."]);
        }

        return SaveStrategyResult.Success(strategy.StrategyId);
    }

    public async Task<DeleteStrategyResult> DeleteAsync(DeleteStrategyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty || request.StrategyId == Guid.Empty)
        {
            return DeleteStrategyResult.Failure(["La stratégie est introuvable."]);
        }

        StrategyAuthorization? authorization = await GetAuthorizationAsync(request.ActorUserId, request.TeamId, cancellationToken);

        if (authorization?.CanManage != true)
        {
            return DeleteStrategyResult.Failure(["Vous n’êtes pas autorisé à supprimer cette stratégie."]);
        }

        Strategy? strategy = await _context.Strategies
            .SingleOrDefaultAsync(item => item.StrategyId == request.StrategyId && item.TeamId == request.TeamId, cancellationToken);

        if (strategy is null)
        {
            return DeleteStrategyResult.Failure(["La stratégie est introuvable."]);
        }

        int associationCount = await _context.ActivityStrategies
            .AsNoTracking()
            .CountAsync(activityStrategy => activityStrategy.StrategyId == request.StrategyId, cancellationToken);

        var imageData = await _context.ImageFiles
            .AsNoTracking()
            .Where(image => image.StrategyImageForStrategyId == request.StrategyId)
            .Select(image => new
            {
                image.OptimizedStorageKey,
                image.ThumbnailStorageKey
            })
            .SingleOrDefaultAsync(cancellationToken);

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.Strategies.Remove(strategy);
        _context.ActionTraces.Add(new ActionTrace(
            request.ActorUserId,
            request.TeamId,
            StrategyDeletedActionCode,
            nameof(Strategy),
            strategy.StrategyId.ToString(),
            TraceOutcome.Succeeded,
            utcNow));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            _logger.LogError(exception, "Strategy deletion persistence failed for actor {ActorUserId}, team {TeamId} and strategy {StrategyId}.", request.ActorUserId, request.TeamId, request.StrategyId);

            return DeleteStrategyResult.Failure(["La stratégie n’a pas pu être supprimée. Veuillez réessayer."]);
        }

        if (imageData is not null)
        {
            await _privateImageService.DeleteStrategyImageFilesAsync(
                request.StrategyId,
                imageData.OptimizedStorageKey,
                imageData.ThumbnailStorageKey,
                CancellationToken.None);
        }

        return DeleteStrategyResult.Success(associationCount);
    }

    private async Task<StrategyAuthorization?> GetAuthorizationAsync(Guid userId, Guid teamId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || teamId == Guid.Empty)
        {
            return null;
        }

        return await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.TeamId == teamId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new { Membership = membership, Team = team })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new StrategyAuthorization(
                item.Membership.TeamMembershipId,
                item.Team.OwnerUserId == userId || role.Code == ManagerRoleCode || role.Code == CoachRoleCode))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task<bool> MapExistsAsync(int mapId, CancellationToken cancellationToken)
    {
        if (mapId <= 0)
        {
            return Task.FromResult(false);
        }

        return _context.Maps
            .AsNoTracking()
            .AnyAsync(map => map.MapId == mapId, cancellationToken);
    }

    private sealed class StrategyAuthorization
    {
        public Guid MembershipId { get; }

        public bool CanManage { get; }

        public StrategyAuthorization(Guid membershipId, bool canManage)
        {
            MembershipId = membershipId;
            CanManage = canManage;
        }
    }
}