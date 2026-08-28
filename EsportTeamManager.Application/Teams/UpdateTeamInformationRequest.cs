namespace EsportTeamManager.Application.Teams;

public sealed class UpdateTeamInformationRequest
{
    public Guid ActorUserId { get; }

    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string? Description { get; }

    public string TimeZoneId { get; }

    public string? LogoFileName { get; }

    public Stream? LogoContent { get; }

    public bool HasLogo => LogoContent is not null;

    public UpdateTeamInformationRequest(Guid actorUserId, Guid teamId, string name, string? tag, string? description, string timeZoneId, string? logoFileName = null, Stream? logoContent = null)
    {
        ActorUserId = actorUserId;
        TeamId = teamId;
        Name = name;
        Tag = tag;
        Description = description;
        TimeZoneId = timeZoneId;
        LogoFileName = logoFileName;
        LogoContent = logoContent;
    }
}