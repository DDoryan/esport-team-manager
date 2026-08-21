namespace EsportTeamManager.Application.Teams;

public interface IUserTeamService
{
    Task<CreateTeamResult> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserTeamSummary>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}