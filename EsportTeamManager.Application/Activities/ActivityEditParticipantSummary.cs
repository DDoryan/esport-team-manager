using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Activities;

public sealed class ActivityEditParticipantSummary
{
    public Guid TeamMembershipId { get; }

    public string DisplayName { get; }

    public string RoleLabel { get; }

    public bool IsOwner { get; }

    public bool IsSelected { get; }

    public bool IsFormerMember { get; }

    public Attendance? Attendance { get; }

    public ActivityEditParticipantSummary(Guid teamMembershipId, string displayName, string roleLabel, bool isOwner, bool isSelected, bool isFormerMember, Attendance? attendance)
    {
        TeamMembershipId = teamMembershipId;
        DisplayName = displayName;
        RoleLabel = roleLabel;
        IsOwner = isOwner;
        IsSelected = isSelected;
        IsFormerMember = isFormerMember;
        Attendance = attendance;
    }
}