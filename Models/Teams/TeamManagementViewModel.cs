namespace EsportTeamManager.Web.Models.Teams;

public sealed class TeamManagementViewModel
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string? Description { get; }

    public string TimeZoneId { get; }

    public bool CurrentUserIsOwner { get; }

    public bool CurrentUserCanInviteMembers { get; }

    public bool CurrentUserCanLeaveTeam { get; }

    public IReadOnlyCollection<TeamRoleOptionViewModel> AvailableRoles { get; }

    public IReadOnlyCollection<TeamMemberViewModel> Members { get; }

    public TeamManagementViewModel(Guid teamId, string name, string? tag, string? description, string timeZoneId, bool currentUserIsOwner, bool currentUserCanInviteMembers, bool currentUserCanLeaveTeam, IReadOnlyCollection<TeamRoleOptionViewModel> availableRoles, IReadOnlyCollection<TeamMemberViewModel> members)
    {
        TeamId = teamId;
        Name = name;
        Tag = tag;
        Description = description;
        TimeZoneId = timeZoneId;
        CurrentUserIsOwner = currentUserIsOwner;
        CurrentUserCanInviteMembers = currentUserCanInviteMembers;
        CurrentUserCanLeaveTeam = currentUserCanLeaveTeam;
        AvailableRoles = availableRoles;
        Members = members;
    }
}