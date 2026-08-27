namespace EsportTeamManager.Application.Teams;

public sealed class LeaveTeamRequest
{
    public Guid UserId { get; }

    public Guid TeamId { get; }

    public LeaveTeamRequest(Guid userId, Guid teamId)
    {
        UserId = userId;
        TeamId = teamId;
    }
}