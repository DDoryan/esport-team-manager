namespace EsportTeamManager.Application.Teams;

public interface IUserTeamService
{
    Task<TeamMembershipActionResult> ChangeMemberRoleAsync(ChangeTeamMemberRoleRequest request, CancellationToken cancellationToken = default);

    Task<CreateTeamResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);

    Task<TeamManagementDetails?> GetManagementDetailsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<InviteTeamMemberResult> InviteMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default);

    Task<TeamMembershipActionResult> LeaveTeamAsync(LeaveTeamRequest request, CancellationToken cancellationToken = default);

    Task<TeamMembershipActionResult> RemoveMemberAsync(RemoveTeamMemberRequest request, CancellationToken cancellationToken = default);
}