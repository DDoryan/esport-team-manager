namespace RepriseWeb.ViewModels.Activities;

public sealed class ActivityEditParticipantViewModel
{
    public Guid TeamMembershipId { get; }

    public string DisplayName { get; }

    public string RoleLabel { get; }

    public bool IsOwner { get; }

    public string AttendanceLabel { get; }

    public ActivityEditParticipantViewModel(Guid teamMembershipId, string displayName, string roleLabel, bool isOwner, string attendanceLabel)
    {
        TeamMembershipId = teamMembershipId;
        DisplayName = displayName;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
        AttendanceLabel = attendanceLabel;
    }
}