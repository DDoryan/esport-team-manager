namespace EsportTeamManager.Web.Models.Teams;

public sealed class OwnershipTransferRecipientOptionViewModel
{
    public Guid TeamMembershipId { get; }

    public string Pseudo { get; }

    public string Tag { get; }

    public string RoleLabel { get; }

    public OwnershipTransferRecipientOptionViewModel(Guid teamMembershipId, string pseudo, string tag, string roleLabel)
    {
        TeamMembershipId = teamMembershipId;
        Pseudo = pseudo;
        Tag = tag;
        RoleLabel = roleLabel;
    }
}