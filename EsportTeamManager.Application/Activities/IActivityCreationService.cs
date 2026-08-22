namespace EsportTeamManager.Application.Activities;

public interface IActivityCreationService
{
    Task<bool> CanCreateAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default);

    Task<ActivityCreationOptions?> GetOptionsAsync(Guid userId, Guid teamId, CancellationToken cancellationToken = default);

    Task<CreateActivityResult> CreateAsync(CreateActivityRequest request, CancellationToken cancellationToken = default);
}