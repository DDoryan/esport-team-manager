namespace EsportTeamManager.Application.Images;

public sealed class PrivateImageContent
{
    public Stream Content { get; }

    public string MediaType { get; }

    public string? DownloadFileName { get; }

    public PrivateImageContent(Stream content, string mediaType)
        : this(content, mediaType, null)
    {
    }

    public PrivateImageContent(Stream content, string mediaType, string? downloadFileName)
    {
        ArgumentNullException.ThrowIfNull(content);

        Content = content;
        MediaType = mediaType;
        DownloadFileName = downloadFileName;
    }
}