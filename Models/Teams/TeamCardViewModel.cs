namespace EsportTeamManager.Web.Models.Teams;

public sealed class TeamCardViewModel
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string RoleLabel { get; }

    public bool IsOwner { get; }

    public string Initials { get; }

    public TeamCardViewModel(Guid teamId, string name, string? tag, string roleLabel, bool isOwner, string initials)
    {
        TeamId = teamId;
        Name = name;
        Tag = tag;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
        Initials = initials;
    }
}