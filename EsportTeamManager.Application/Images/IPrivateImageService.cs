namespace EsportTeamManager.Application.Images;

public interface IPrivateImageService
{
    Task<StorePrivateImageResult> StoreStrategyImageAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default);

    Task<StorePrivateImageResult> StoreTeamLogoAsync(StorePrivateImageRequest request, CancellationToken cancellationToken = default);
}