using System.Buffers;
using EsportTeamManager.Application.Images;
using EsportTeamManager.Domain.Entities;
using EsportTeamManager.Domain.Exceptions;
using EsportTeamManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Infrastructure.Images;

public sealed class PrivateImageService : IPrivateImageService
{
    private const int BufferSize = 81_920;
    private const string StoredMediaType = "image/webp";
    private const string ManagerRoleCode = "Manager";
    private const string CoachRoleCode = "Coach";

    private static readonly HashSet<string> SupportedSourceMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    private readonly ApplicationDbContext _context;
    private readonly PrivateImageStorageOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PrivateImageService> _logger;
    private readonly string _rootPath;

    public PrivateImageService(ApplicationDbContext context, IHostEnvironment hostEnvironment, IOptions<PrivateImageStorageOptions> options, TimeProvider timeProvider, ILogger<PrivateImageService> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _rootPath = Path.IsPathRooted(_options.RootPath)
            ? Path.GetFullPath(_options.RootPath)
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, _options.RootPath));
    }

    public async Task<PrivateImageContent?> GetTeamLogoThumbnailAsync(Guid actorUserId, Guid teamId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (actorUserId == Guid.Empty || teamId == Guid.Empty)
        {
            return null;
        }

        bool userCanAccessTeam = await _context.TeamMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.TeamId == teamId && membership.UserId == actorUserId && membership.Status == MembershipStatus.Active, cancellationToken);

        if (!userCanAccessTeam)
        {
            return null;
        }

        var imageData = await _context.ImageFiles
            .AsNoTracking()
            .Where(image => image.TeamLogoForTeamId == teamId)
            .Select(image => new
            {
                image.ThumbnailStorageKey,
                image.MediaType
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (imageData is null)
        {
            return null;
        }

        string physicalPath = GetPhysicalPath(imageData.ThumbnailStorageKey);

        try
        {
            FileStream content = new(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

            return new PrivateImageContent(content, imageData.MediaType);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Private team logo reading failed for team {TeamId}.", teamId);

            return null;
        }
    }

    public Task<PrivateImageContent?> GetStrategyImageThumbnailAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
    {
        return GetStrategyImageContentAsync(actorUserId, teamId, strategyId, true, cancellationToken);
    }

    public Task<PrivateImageContent?> GetStrategyImageAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default)
    {
        return GetStrategyImageContentAsync(actorUserId, teamId, strategyId, false, cancellationToken);
    }

    public Task DeleteStrategyImageFilesAsync(Guid strategyId, string optimizedStorageKey, string thumbnailStorageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedOptimizedStorageKey = optimizedStorageKey?.Trim().Replace('\\', '/') ?? string.Empty;
        string normalizedThumbnailStorageKey = thumbnailStorageKey?.Trim().Replace('\\', '/') ?? string.Empty;

        if (strategyId == Guid.Empty
            || !IsStrategyImageStorageKey(strategyId, normalizedOptimizedStorageKey)
            || !IsStrategyImageStorageKey(strategyId, normalizedThumbnailStorageKey))
        {
            _logger.LogWarning("Strategy image cleanup was refused because its storage keys are invalid for strategy {StrategyId}.", strategyId);

            return Task.CompletedTask;
        }

        TryDeleteFile(GetPhysicalPath(normalizedOptimizedStorageKey));
        TryDeleteFile(GetPhysicalPath(normalizedThumbnailStorageKey));

        return Task.CompletedTask;
    }

    private async Task<PrivateImageContent?> GetStrategyImageContentAsync(Guid actorUserId, Guid teamId, Guid strategyId, bool useThumbnail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (actorUserId == Guid.Empty || teamId == Guid.Empty || strategyId == Guid.Empty)
        {
            return null;
        }

        bool userCanAccessStrategy = await _context.TeamMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.TeamId == teamId
                && membership.UserId == actorUserId
                && membership.Status == MembershipStatus.Active,
                cancellationToken);

        if (!userCanAccessStrategy)
        {
            return null;
        }

        var imageData = await _context.Strategies
            .AsNoTracking()
            .Where(strategy => strategy.StrategyId == strategyId && strategy.TeamId == teamId)
            .Select(strategy => new
            {
                strategy.Name,
                Image = _context.ImageFiles
                    .Where(image => image.StrategyImageForStrategyId == strategy.StrategyId)
                    .Select(image => new
                    {
                        image.OptimizedStorageKey,
                        image.ThumbnailStorageKey,
                        image.MediaType
                    })
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (imageData?.Image is null)
        {
            return null;
        }

        string storageKey = useThumbnail
            ? imageData.Image.ThumbnailStorageKey
            : imageData.Image.OptimizedStorageKey;

        string physicalPath = GetPhysicalPath(storageKey);
        string downloadFileName = CreateStrategyDownloadFileName(imageData.Name);

        try
        {
            FileStream content = new(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

            return new PrivateImageContent(content, imageData.Image.MediaType, downloadFileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Private strategy image reading failed for strategy {StrategyId}.", strategyId);

            return null;
        }
    }

    public async Task<StorePrivateImageResult> ReplaceStrategyImageAsync(ReplaceStrategyImageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty || request.StrategyId == Guid.Empty)
        {
            return StorePrivateImageResult.Failure(["La modification de l’image n’est pas autorisée."]);
        }

        bool actorCanManageStrategy = await _context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == request.ActorUserId && membership.TeamId == request.TeamId && membership.Status == MembershipStatus.Active)
            .Join(_context.Teams, membership => membership.TeamId, team => team.TeamId, (membership, team) => new { Membership = membership, Team = team })
            .Join(_context.TeamRoles, item => item.Membership.TeamRoleId, role => role.TeamRoleId, (item, role) => new { item.Team, Role = role })
            .AnyAsync(item =>
                _context.Strategies.Any(strategy => strategy.StrategyId == request.StrategyId && strategy.TeamId == request.TeamId)
                && (item.Team.OwnerUserId == request.ActorUserId || item.Role.Code == ManagerRoleCode || item.Role.Code == CoachRoleCode),
                cancellationToken);

        if (!actorCanManageStrategy)
        {
            return StorePrivateImageResult.Failure(["La modification de l’image n’est pas autorisée."]);
        }

        StorePrivateImageRequest storeRequest = new(request.StrategyId, request.OriginalFileName, request.Content);

        return await StoreAsync(storeRequest, false, _options.StrategyImageMaximumEdgePixels, true, cancellationToken);
    }

    public async Task<StorePrivateImageResult> ReplaceTeamLogoAsync(ReplaceTeamLogoRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ActorUserId == Guid.Empty || request.TeamId == Guid.Empty)
        {
            return StorePrivateImageResult.Failure(["La modification du logo n’est pas autorisée."]);
        }

        bool actorOwnsTeam = await _context.Teams
            .AsNoTracking()
            .AnyAsync(team => team.TeamId == request.TeamId && team.OwnerUserId == request.ActorUserId, cancellationToken);

        if (!actorOwnsTeam)
        {
            return StorePrivateImageResult.Failure(["La modification du logo n’est pas autorisée."]);
        }

        StorePrivateImageRequest storeRequest = new(request.TeamId, request.OriginalFileName, request.Content);

        return await StoreAsync(storeRequest, true, _options.TeamLogoMaximumEdgePixels, true, cancellationToken);
    }

    public Task<StorePrivateImageResult> StoreStrategyImageAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
    {
        return StoreAsync(request, false, _options.StrategyImageMaximumEdgePixels, false, cancellationToken);
    }

    public Task<StorePrivateImageResult> StoreTeamLogoAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default)
    {
        return StoreAsync(request, true, _options.TeamLogoMaximumEdgePixels, false, cancellationToken);
    }

    private async Task<StorePrivateImageResult> StoreAsync(StorePrivateImageRequest request, bool isTeamLogo, int maximumEdgePixels, bool replaceExistingImage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.OwnerId == Guid.Empty || string.IsNullOrWhiteSpace(request.OriginalFileName) || request.Content is null || !request.Content.CanRead)
        {
            return StorePrivateImageResult.Failure(["L’image à enregistrer est invalide."]);
        }

        string? ownerValidationError = await GetOwnerValidationErrorAsync(request.OwnerId, isTeamLogo, replaceExistingImage, cancellationToken);

        if (ownerValidationError is not null)
        {
            return StorePrivateImageResult.Failure([ownerValidationError]);
        }

        ImageFile? existingImage = null;
        string? previousOptimizedPhysicalPath = null;
        string? previousThumbnailPhysicalPath = null;

        if (replaceExistingImage)
        {
            existingImage = await _context.ImageFiles.SingleOrDefaultAsync(
                image => isTeamLogo
                    ? image.TeamLogoForTeamId == request.OwnerId
                    : image.StrategyImageForStrategyId == request.OwnerId,
                cancellationToken);

            if (existingImage is not null)
            {
                previousOptimizedPhysicalPath = GetPhysicalPath(existingImage.OptimizedStorageKey);
                previousThumbnailPhysicalPath = GetPhysicalPath(existingImage.ThumbnailStorageKey);
            }
        }

        string storageCategory = isTeamLogo ? "team-logos" : "strategy-images";
        string internalIdentifier = Guid.NewGuid().ToString("N");
        string internalFileName = $"{internalIdentifier}.webp";
        string thumbnailFileName = $"{internalIdentifier}-thumbnail.webp";
        string optimizedStorageKey = $"{storageCategory}/{request.OwnerId:N}/{internalFileName}";
        string thumbnailStorageKey = $"{storageCategory}/{request.OwnerId:N}/{thumbnailFileName}";
        string temporaryDirectoryPath = Path.Combine(_rootPath, ".temporary");
        string temporaryInputPath = Path.Combine(temporaryDirectoryPath, $"{Guid.NewGuid():N}.upload");
        string temporaryOptimizedPath = Path.Combine(temporaryDirectoryPath, $"{Guid.NewGuid():N}.webp");
        string temporaryThumbnailPath = Path.Combine(temporaryDirectoryPath, $"{Guid.NewGuid():N}.webp");
        string optimizedPhysicalPath = GetPhysicalPath(optimizedStorageKey);
        string thumbnailPhysicalPath = GetPhysicalPath(thumbnailStorageKey);
        bool operationSucceeded = false;

        try
        {
            Directory.CreateDirectory(temporaryDirectoryPath);

            await CopyToTemporaryFileAsync(request.Content, temporaryInputPath, cancellationToken);

            using Image sourceImage = await LoadAndValidateImageAsync(temporaryInputPath, cancellationToken);

            sourceImage.Mutate(context => context.AutoOrient());

            ValidateDimensions(sourceImage.Width, sourceImage.Height);

            using Image optimizedImage = CreateResizedImage(sourceImage, maximumEdgePixels);
            using Image thumbnailImage = CreateResizedImage(sourceImage, _options.ThumbnailMaximumEdgePixels);

            RemoveMetadata(optimizedImage);
            RemoveMetadata(thumbnailImage);

            await SaveWebpAsync(optimizedImage, temporaryOptimizedPath, isTeamLogo, cancellationToken);
            await SaveWebpAsync(thumbnailImage, temporaryThumbnailPath, isTeamLogo, cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(optimizedPhysicalPath)!);

            File.Move(temporaryOptimizedPath, optimizedPhysicalPath);
            File.Move(temporaryThumbnailPath, thumbnailPhysicalPath);

            long optimizedFileSizeBytes = new FileInfo(optimizedPhysicalPath).Length;
            DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();
            ImageFile imageFile;

            if (existingImage is not null)
            {
                existingImage.ReplaceStoredContent(internalFileName, request.OriginalFileName, StoredMediaType, optimizedFileSizeBytes, optimizedImage.Width, optimizedImage.Height, optimizedStorageKey, thumbnailStorageKey, createdAtUtc);
                imageFile = existingImage;
            }
            else
            {
                imageFile = isTeamLogo
                    ? ImageFile.CreateTeamLogo(request.OwnerId, internalFileName, request.OriginalFileName, StoredMediaType, optimizedFileSizeBytes, optimizedImage.Width, optimizedImage.Height, optimizedStorageKey, thumbnailStorageKey, createdAtUtc)
                    : ImageFile.CreateStrategyImage(request.OwnerId, internalFileName, request.OriginalFileName, StoredMediaType, optimizedFileSizeBytes, optimizedImage.Width, optimizedImage.Height, optimizedStorageKey, thumbnailStorageKey, createdAtUtc);

                _context.ImageFiles.Add(imageFile);
            }

            await _context.SaveChangesAsync(cancellationToken);

            operationSucceeded = true;

            if (previousOptimizedPhysicalPath is not null)
            {
                TryDeleteFile(previousOptimizedPhysicalPath);
            }

            if (previousThumbnailPhysicalPath is not null)
            {
                TryDeleteFile(previousThumbnailPhysicalPath);
            }

            return StorePrivateImageResult.Success(imageFile);
        }
        catch (PrivateImageValidationException exception)
        {
            return StorePrivateImageResult.Failure([exception.Message]);
        }
        catch (DomainException exception)
        {
            _logger.LogWarning(exception, "Image entity creation or replacement failed for owner {OwnerId}.", request.OwnerId);

            return StorePrivateImageResult.Failure(["L’image n’a pas pu être enregistrée."]);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Image database persistence failed for owner {OwnerId}.", request.OwnerId);

            return StorePrivateImageResult.Failure(["L’image n’a pas pu être enregistrée dans la base de données."]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Private image storage failed for owner {OwnerId}.", request.OwnerId);

            return StorePrivateImageResult.Failure(["Le stockage privé de l’image a échoué."]);
        }
        finally
        {
            TryDeleteFile(temporaryInputPath);
            TryDeleteFile(temporaryOptimizedPath);
            TryDeleteFile(temporaryThumbnailPath);

            if (!operationSucceeded)
            {
                TryDeleteFile(optimizedPhysicalPath);
                TryDeleteFile(thumbnailPhysicalPath);
            }
        }
    }

    private async Task<string?> GetOwnerValidationErrorAsync(Guid ownerId, bool isTeamLogo, bool replaceExistingImage, CancellationToken cancellationToken)
    {
        if (isTeamLogo)
        {
            bool teamExists = await _context.Teams.AsNoTracking().AnyAsync(team => team.TeamId == ownerId, cancellationToken);

            if (!teamExists)
            {
                return "L’équipe associée au logo est introuvable.";
            }

            if (!replaceExistingImage)
            {
                bool teamAlreadyHasLogo = await _context.ImageFiles.AsNoTracking().AnyAsync(image => image.TeamLogoForTeamId == ownerId, cancellationToken);

                if (teamAlreadyHasLogo)
                {
                    return "Un logo est déjà enregistré pour cette équipe.";
                }
            }

            return null;
        }

        bool strategyExists = await _context.Strategies.AsNoTracking().AnyAsync(strategy => strategy.StrategyId == ownerId, cancellationToken);

        if (!strategyExists)
        {
            return "La stratégie associée à l’image est introuvable.";
        }

        bool strategyAlreadyHasImage = await _context.ImageFiles.AsNoTracking().AnyAsync(image => image.StrategyImageForStrategyId == ownerId, cancellationToken);

        if (!replaceExistingImage && strategyAlreadyHasImage)
        {
            return "Une image est déjà enregistrée pour cette stratégie.";
        }

        return null;
    }

    private async Task CopyToTemporaryFileAsync(Stream source, string destinationPath, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long totalBytesRead = 0;

        try
        {
            await using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

            while (true)
            {
                int bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;

                if (totalBytesRead > _options.MaximumUploadSizeBytes)
                {
                    throw new PrivateImageValidationException("La taille du fichier image dépasse la limite autorisée.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, true);
        }

        if (totalBytesRead == 0)
        {
            throw new PrivateImageValidationException("Le fichier image est vide.");
        }
    }

    private async Task<Image> LoadAndValidateImageAsync(string sourcePath, CancellationToken cancellationToken)
    {
        DecoderOptions decoderOptions = new()
        {
            MaxFrames = 2,
            SkipMetadata = false
        };

        try
        {
            await using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

            IImageFormat detectedFormat = Image.DetectFormat(decoderOptions, source);
            string detectedMediaType = detectedFormat.DefaultMimeType.ToLowerInvariant();

            if (!SupportedSourceMediaTypes.Contains(detectedMediaType))
            {
                throw new PrivateImageValidationException("Le format réel du fichier doit être PNG, JPEG ou WebP.");
            }

            source.Position = 0;

            ImageInfo imageInfo = await Image.IdentifyAsync(decoderOptions, source, cancellationToken);

            ValidateDimensions(imageInfo.Width, imageInfo.Height);

            if (imageInfo.FrameMetadataCollection.Count > 1)
            {
                throw new PrivateImageValidationException("Les images animées ne sont pas autorisées.");
            }

            source.Position = 0;

            Image image = await Image.LoadAsync(decoderOptions, source, cancellationToken);

            if (image.Frames.Count > 1)
            {
                image.Dispose();

                throw new PrivateImageValidationException("Les images animées ne sont pas autorisées.");
            }

            return image;
        }
        catch (PrivateImageValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new PrivateImageValidationException("Le fichier ne contient pas une image valide ou il est corrompu.");
        }
    }

    private void ValidateDimensions(int widthPixels, int heightPixels)
    {
        if (widthPixels < _options.MinimumDimensionPixels || heightPixels < _options.MinimumDimensionPixels)
        {
            throw new PrivateImageValidationException($"L’image doit mesurer au moins {_options.MinimumDimensionPixels} × {_options.MinimumDimensionPixels} pixels.");
        }

        if (widthPixels > _options.MaximumDimensionPixels || heightPixels > _options.MaximumDimensionPixels)
        {
            throw new PrivateImageValidationException($"L’image ne peut pas dépasser {_options.MaximumDimensionPixels} pixels par côté.");
        }

        long pixelCount = (long)widthPixels * heightPixels;

        if (pixelCount > _options.MaximumPixelCount)
        {
            throw new PrivateImageValidationException("Le nombre total de pixels de l’image dépasse la limite autorisée.");
        }
    }

    private static Image CreateResizedImage(Image sourceImage, int maximumEdgePixels)
    {
        return sourceImage.Clone(context => context.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maximumEdgePixels, maximumEdgePixels),
            Sampler = KnownResamplers.Lanczos3
        }));
    }

    private static void RemoveMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.CicpProfile = null;
    }

    private async Task SaveWebpAsync(Image image, string destinationPath, bool useLosslessEncoding, CancellationToken cancellationToken)
    {
        WebpEncoder encoder = new()
        {
            FileFormat = useLosslessEncoding ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy,
            Method = WebpEncodingMethod.BestQuality,
            Quality = _options.WebpQuality,
            SkipMetadata = true
        };

        await using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        await image.SaveAsWebpAsync(destination, encoder, cancellationToken);
    }

    private static string CreateStrategyDownloadFileName(string strategyName)
    {
        char[] invalidCharacters =
        [
            '<',
        '>',
        ':',
        '"',
        '/',
        '\\',
        '|',
        '?',
        '*'
        ];

        string sanitizedName = new(strategyName
            .Trim()
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray());

        sanitizedName = sanitizedName.Trim(' ', '.', '-');

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            sanitizedName = "strategie";
        }

        return $"{sanitizedName}.webp";
    }

    private string GetPhysicalPath(string storageKey)
    {
        string relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        string physicalPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        string normalizedRootPath = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : $"{_rootPath}{Path.DirectorySeparatorChar}";
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!physicalPath.StartsWith(normalizedRootPath, comparison))
        {
            throw new InvalidOperationException("The private image storage path is invalid.");
        }

        return physicalPath;
    }

    private static bool IsStrategyImageStorageKey(Guid strategyId, string storageKey)
    {
        string expectedPrefix = $"strategy-images/{strategyId:N}/";

        if (!storageKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string fileName = storageKey[expectedPrefix.Length..];

        return fileName.Length > 0
            && !fileName.Contains('/')
            && fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Temporary or incomplete image file cleanup failed for {FilePath}.", filePath);
        }
    }

    private sealed class PrivateImageValidationException : Exception
    {
        public PrivateImageValidationException(string message) : base(message)
        {
        }
    }
}