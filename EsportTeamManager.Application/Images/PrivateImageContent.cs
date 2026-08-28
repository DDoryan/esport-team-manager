namespace EsportTeamManager.Application.Images;

public sealed class PrivateImageContent
{
    public Stream Content { get; }

    public string MediaType { get; }

    public PrivateImageContent(Stream content, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(content);

        Content = content;
        MediaType = mediaType;
    }
}