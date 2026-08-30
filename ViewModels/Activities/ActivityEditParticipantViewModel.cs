namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityEditParticipantViewModel
{
    public Guid TeamMembershipId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string RoleLabel { get; set; } = string.Empty;

    public bool IsOwner { get; set; }

    public bool IsSelected { get; set; }

    public bool IsFormerMember { get; set; }

    public bool IsPresent { get; set; }

    public ActivityEditParticipantViewModel()
    {
    }

    public ActivityEditParticipantViewModel(Guid teamMembershipId, string displayName, string roleLabel, bool isOwner, bool isSelected, bool isFormerMember, bool isPresent)
    {
        TeamMembershipId = teamMembershipId;
        DisplayName = displayName;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
        IsSelected = isSelected;
        IsFormerMember = isFormerMember;
        IsPresent = isPresent;
    }
}