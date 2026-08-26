namespace EsportTeamManager.Application.Teams;

public sealed class TeamMemberSummary
{
    public Guid TeamMembershipId { get; }

    public string Pseudo { get; }

    public string Tag { get; }

    public string RoleLabel { get; }

    public bool IsOwner { get; }

    public DateTimeOffset JoinedAtUtc { get; }

    public TeamMemberSummary(Guid teamMembershipId, string pseudo, string tag, string roleLabel, bool isOwner, DateTimeOffset joinedAtUtc)
    {
        TeamMembershipId = teamMembershipId;
        Pseudo = pseudo;
        Tag = tag;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
        JoinedAtUtc = joinedAtUtc;
    }
}