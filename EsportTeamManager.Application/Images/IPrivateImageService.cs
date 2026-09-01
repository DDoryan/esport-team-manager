namespace EsportTeamManager.Application.Images;

public interface IPrivateImageService
{
    Task<PrivateImageContent?> GetTeamLogoThumbnailAsync(Guid actorUserId, Guid teamId, CancellationToken cancellationToken = default);

    Task<PrivateImageContent?> GetStrategyImageThumbnailAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default);

    Task<PrivateImageContent?> GetStrategyImageAsync(Guid actorUserId, Guid teamId, Guid strategyId, CancellationToken cancellationToken = default);

    Task DeleteStrategyImageFilesAsync(Guid strategyId, string optimizedStorageKey, string thumbnailStorageKey, CancellationToken cancellationToken = default);

    Task<StorePrivateImageResult> ReplaceTeamLogoAsync(ReplaceTeamLogoRequest request, CancellationToken cancellationToken = default);

    Task<StorePrivateImageResult> ReplaceStrategyImageAsync(ReplaceStrategyImageRequest request, CancellationToken cancellationToken = default);

    Task<StorePrivateImageResult> StoreStrategyImageAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default);

    Task<StorePrivateImageResult> StoreTeamLogoAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default);
}