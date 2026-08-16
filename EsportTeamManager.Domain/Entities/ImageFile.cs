using System.IO;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities
{
    public sealed class ImageFile
    {
        public Guid ImageFileId { get; private set; }
        public Guid? TeamLogoForTeamId { get; private set; }
        public Guid? StrategyImageForStrategyId { get; private set; }
        public string InternalFileName { get; private set; } = string.Empty;
        public string OriginalFileName { get; private set; } = string.Empty;
        public string MediaType { get; private set; } = string.Empty;
        public long FileSizeBytes { get; private set; }
        public int WidthPixels { get; private set; }
        public int HeightPixels { get; private set; }
        public string OptimizedStorageKey { get; private set; } = string.Empty;
        public string ThumbnailStorageKey { get; private set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; private set; }

        private ImageFile()
        {
        }

        private ImageFile(Guid? teamLogoForTeamId, Guid? strategyImageForStrategyId, string internalFileName, string originalFileName, string mediaType, long fileSizeBytes, int widthPixels, int heightPixels, string optimizedStorageKey, string thumbnailStorageKey, DateTimeOffset createdAtUtc)
        {
            if (teamLogoForTeamId.HasValue == strategyImageForStrategyId.HasValue)
            {
                throw new DomainException("An image must belong to exactly one team or one strategy.");
            }

            if (teamLogoForTeamId.HasValue && teamLogoForTeamId.Value == Guid.Empty)
            {
                throw new DomainException("The team identifier is invalid.");
            }

            if (strategyImageForStrategyId.HasValue && strategyImageForStrategyId.Value == Guid.Empty)
            {
                throw new DomainException("The strategy identifier is invalid.");
            }

            string normalizedInternalFileName = internalFileName?.Trim() ?? string.Empty;
            string normalizedOriginalFileName = Path.GetFileName(originalFileName?.Trim() ?? string.Empty);
            string normalizedMediaType = mediaType?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedOptimizedStorageKey = optimizedStorageKey?.Trim() ?? string.Empty;
            string normalizedThumbnailStorageKey = thumbnailStorageKey?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedInternalFileName))
            {
                throw new DomainException("The internal file name is required.");
            }

            if (normalizedInternalFileName.Length > 100)
            {
                throw new DomainException("The internal file name cannot exceed 100 characters.");
            }

            if (string.IsNullOrWhiteSpace(normalizedOriginalFileName))
            {
                throw new DomainException("The original file name is required.");
            }

            if (normalizedOriginalFileName.Length > 255)
            {
                throw new DomainException("The original file name cannot exceed 255 characters.");
            }

            if (normalizedMediaType != "image/png" && normalizedMediaType != "image/jpeg" && normalizedMediaType != "image/webp")
            {
                throw new DomainException("The image format must be PNG, JPEG or WebP.");
            }

            if (fileSizeBytes <= 0)
            {
                throw new DomainException("The image file size must be positive.");
            }

            if (widthPixels <= 0 || heightPixels <= 0)
            {
                throw new DomainException("The image dimensions must be positive.");
            }

            if (string.IsNullOrWhiteSpace(normalizedOptimizedStorageKey) || normalizedOptimizedStorageKey.Length > 500)
            {
                throw new DomainException("The optimized image storage key is invalid.");
            }

            if (string.IsNullOrWhiteSpace(normalizedThumbnailStorageKey) || normalizedThumbnailStorageKey.Length > 500)
            {
                throw new DomainException("The thumbnail storage key is invalid.");
            }

            ImageFileId = Guid.NewGuid();
            TeamLogoForTeamId = teamLogoForTeamId;
            StrategyImageForStrategyId = strategyImageForStrategyId;
            InternalFileName = normalizedInternalFileName;
            OriginalFileName = normalizedOriginalFileName;
            MediaType = normalizedMediaType;
            FileSizeBytes = fileSizeBytes;
            WidthPixels = widthPixels;
            HeightPixels = heightPixels;
            OptimizedStorageKey = normalizedOptimizedStorageKey;
            ThumbnailStorageKey = normalizedThumbnailStorageKey;
            CreatedAtUtc = createdAtUtc.ToUniversalTime();
        }

        public static ImageFile CreateTeamLogo(Guid teamId, string internalFileName, string originalFileName, string mediaType, long fileSizeBytes, int widthPixels, int heightPixels, string optimizedStorageKey, string thumbnailStorageKey, DateTimeOffset createdAtUtc)
        {
            return new ImageFile(teamId, null, internalFileName, originalFileName, mediaType, fileSizeBytes, widthPixels, heightPixels, optimizedStorageKey, thumbnailStorageKey, createdAtUtc);
        }

        public static ImageFile CreateStrategyImage(Guid strategyId, string internalFileName, string originalFileName, string mediaType, long fileSizeBytes, int widthPixels, int heightPixels, string optimizedStorageKey, string thumbnailStorageKey, DateTimeOffset createdAtUtc)
        {
            return new ImageFile(null, strategyId, internalFileName, originalFileName, mediaType, fileSizeBytes, widthPixels, heightPixels, optimizedStorageKey, thumbnailStorageKey, createdAtUtc);
        }
    }
}