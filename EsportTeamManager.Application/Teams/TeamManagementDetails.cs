namespace EsportTeamManager.Application.Teams;

public sealed class TeamManagementDetails
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string? Description { get; }

    public string TimeZoneId { get; }

    public bool CurrentUserIsOwner { get; }

    public IReadOnlyCollection<TeamMemberSummary> Members { get; }

    public TeamManagementDetails(Guid teamId, string name, string? tag, string? description, string timeZoneId, bool currentUserIsOwner, IReadOnlyCollection<TeamMemberSummary> members)
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