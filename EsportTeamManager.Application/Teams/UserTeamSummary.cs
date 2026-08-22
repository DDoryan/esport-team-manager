namespace EsportTeamManager.Application.Teams;

public sealed class UserTeamSummary
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string TimeZoneId { get; }

    public string RoleLabel { get; }

    public bool IsOwner { get; }

    public UserTeamSummary(Guid teamId, string name, string? tag, string timeZoneId, string roleLabel, bool isOwner)
    {
        TeamId = teamId;
        Name = name;
        Tag = tag;
        TimeZoneId = timeZoneId;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
    }
}