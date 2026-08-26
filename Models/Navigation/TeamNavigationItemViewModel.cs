namespace EsportTeamManager.Web.Models.Navigation;

public sealed class TeamNavigationItemViewModel
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public TeamNavigationItemViewModel(Guid teamId, string name, string? tag)
    {
        TeamId = teamId;
        Name = name;
        Tag = tag;
    }
}