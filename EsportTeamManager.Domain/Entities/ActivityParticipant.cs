using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class ActivityParticipant
{
    public Guid ActivityId { get; private set; }

    public Guid TeamMembershipId { get; private set; }

    public Attendance? Attendance { get; private set; }

    private ActivityParticipant()
    {
    }

    public ActivityParticipant(Guid activityId, Guid teamMembershipId)
    {
        if (activityId == Guid.Empty)
        {
            throw new DomainException("The activity identifier cannot be empty.");
        }

        if (teamMembershipId == Guid.Empty)
        {
            throw new DomainException("The team membership identifier cannot be empty.");
        }

        ActivityId = activityId;
        TeamMembershipId = teamMembershipId;
        Attendance = null;
    }

    public void MarkAttendance(Attendance attendance)
    {
        if (!Enum.IsDefined(typeof(Attendance), attendance))
        {
            throw new DomainException("The attendance value is invalid.");
        }

        Attendance = attendance;
    }

    public void ClearAttendance()
    {
        Attendance = null;
    }
}