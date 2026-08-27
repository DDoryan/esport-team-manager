using EsportTeamManager.Domain.Entities;

namespace EsportTeamManager.Application.Images;

public sealed class StorePrivateImageResult
{
    public bool Succeeded { get; }

    public ImageFile? Image { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private StorePrivateImageResult(bool succeeded, ImageFile? image, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Image = image;
        Errors = errors;
    }

    public static StorePrivateImageResult Success(ImageFile image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return new StorePrivateImageResult(true, image, Array.Empty<string>());
    }

    public static StorePrivateImageResult Failure(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new StorePrivateImageResult(false, null, errors.ToArray());
    }
}