namespace EsportTeamManager.Application.Teams;

public sealed class TeamManagementDetails
{
    public Guid TeamId { get; }

    public string Name { get; }

    public string? Tag { get; }

    public string? Description { get; }

    public string TimeZoneId { get; }

    public bool CurrentUserIsOwner { get; }

    public bool CurrentUserCanInviteMembers { get; }

    public bool CurrentUserCanLeaveTeam => !CurrentUserIsOwner;

    public IReadOnlyCollection<TeamRoleOption> AvailableInvitationRoles { get; }

    public IReadOnlyCollection<TeamRoleOption> AvailableMemberRoles => AvailableInvitationRoles;

    public IReadOnlyCollection<TeamMemberSummary> Members { get; }

    public TeamManagementDetails(Guid teamId, string name, string? tag, string? description, string timeZoneId, bool currentUserIsOwner, bool currentUserCanInviteMembers, IReadOnlyCollection<TeamRoleOption> availableInvitationRoles, IReadOnlyCollection<TeamMemberSummary> members)
    {
        TeamId = teamId;
        Name = name;
        Tag = tag;
        Description = description;
        TimeZoneId = timeZoneId;
        CurrentUserIsOwner = currentUserIsOwner;
        CurrentUserCanInviteMembers = currentUserCanInviteMembers;
        AvailableInvitationRoles = availableInvitationRoles;
        Members = members;
    }
}