using EsportTeamManager.Domain.Enums;

namespace EsportTeamManager.Application.Activities;

public sealed class UpdateActivityParticipantRequest
{
    public Guid TeamMembershipId { get; }

    public bool IsSelected { get; }

    public Attendance? Attendance { get; }

    public UpdateActivityParticipantRequest(Guid teamMembershipId, bool isSelected, Attendance? attendance)
    {
        TeamMembershipId = teamMembershipId;
        IsSelected = isSelected;
        Attendance = attendance;
    }
}