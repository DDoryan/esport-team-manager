namespace EsportTeamManager.Application.Teams;

public interface IUserTeamService
{
    Task<CreateTeamResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);

    Task<TeamManagementDetails?> GetManagementDetailsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<InviteTeamMemberResult> InviteMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default);
}