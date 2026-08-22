namespace EsportTeamManager.Application.Activities;

public sealed class ActivityParticipantOption
{
    public Guid TeamMembershipId { get; }

    public string Pseudo { get; }

    public string Tag { get; }

    public string RoleLabel { get; }

    public bool IsOwner { get; }

    public ActivityParticipantOption(Guid teamMembershipId, string pseudo, string tag, string roleLabel, bool isOwner)
    {
        TeamMembershipId = teamMembershipId;
        Pseudo = pseudo;
        Tag = tag;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
    }
}