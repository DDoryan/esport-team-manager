namespace EsportTeamManager.Application.Teams;

public sealed class CreateTeamRequest
{
    public Guid OwnerUserId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string TimeZoneId { get; }

    public CreateTeamRequest(Guid ownerUserId, string name, string? tag, string timeZoneId)
    {
        OwnerUserId = ownerUserId;
        Name = name;
        Tag = tag;
        TimeZoneId = timeZoneId;
    }
}