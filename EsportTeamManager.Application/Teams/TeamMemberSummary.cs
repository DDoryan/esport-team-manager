namespace EsportTeamManager.Application.Teams;

public sealed class TeamMemberSummary
{
    public Guid TeamMembershipId { get; }

    public string Pseudo { get; }

    public string Tag { get; }

    public int TeamRoleId { get; }

    public string RoleLabel { get; }

    public bool IsOwner { get; }

    public bool CanChangeRole { get; }

    public bool CanRemove { get; }

    public DateTimeOffset JoinedAtUtc { get; }

    public TeamMemberSummary(Guid teamMembershipId, string pseudo, string tag, int teamRoleId, string roleLabel, bool isOwner, bool canChangeRole, bool canRemove, DateTimeOffset joinedAtUtc)
    {
        TeamMembershipId = teamMembershipId;
        Pseudo = pseudo;
        Tag = tag;
        TeamRoleId = teamRoleId;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
        CanChangeRole = canChangeRole;
        CanRemove = canRemove;
        JoinedAtUtc = joinedAtUtc;
    }
}