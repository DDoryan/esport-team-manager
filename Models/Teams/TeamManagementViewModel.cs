namespace EsportTeamManager.Web.Models.Teams;

public sealed class TeamManagementViewModel
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string? Description { get; }

    public string TimeZoneId { get; }

    public bool CurrentUserIsOwner { get; }

    public IReadOnlyCollection<TeamMemberViewModel> Members { get; }

    public TeamManagementViewModel(Guid teamId, string name, string? tag, string? description, string timeZoneId, bool currentUserIsOwner, IReadOnlyCollection<TeamMemberViewModel> members)
    {
        TeamId = teamId;
        Name = name;
        Tag = tag;
        Description = description;
        TimeZoneId = timeZoneId;
        CurrentUserIsOwner = currentUserIsOwner;
        Members = members;
    }
}