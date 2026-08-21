namespace EsportTeamManager.Application.Teams;

public sealed class UserTeamSummary
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string RoleLabel { get; }

    public bool IsOwner { get; }

    public UserTeamSummary(Guid teamId, string name, string? tag, string roleLabel, bool isOwner)
    {
        TeamId = teamId;
        Name = name;
        Tag = tag;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
    }
}