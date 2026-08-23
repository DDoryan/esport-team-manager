namespace EsportTeamManager.Application.Activities;

public interface IActivityEditingService
{
    Task<ActivityEditDetails?> GetAsync(Guid userId, Guid teamId, Guid activityId, CancellationToken cancellationToken = default);

    Task<UpdateActivityResult> UpdateAsync(UpdateActivityRequest request, CancellationToken cancellationToken = default);
}