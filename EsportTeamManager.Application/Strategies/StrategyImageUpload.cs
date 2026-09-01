namespace EsportTeamManager.Application.Strategies;

public sealed class StrategyImageUpload
{
    public string OriginalFileName { get; }

    public Stream Content { get; }

    public StrategyImageUpload(string originalFileName, Stream content)
    {
        OriginalFileName = originalFileName;
        Content = content;
    }
}