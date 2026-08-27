namespace EsportTeamManager.Application.Images;

public sealed class StorePrivateImageRequest
{
    public Guid OwnerId { get; }

    public string OriginalFileName { get; }

    public Stream Content { get; }

    public StorePrivateImageRequest(Guid ownerId, string originalFileName, Stream content)
    {
        OwnerId = ownerId;
        OriginalFileName = originalFileName;
        Content = content;
    }
}