namespace EsportTeamManager.Application.Activities;

public sealed class CreateActivityResult
{
    public bool Succeeded { get; }

    public Guid? ActivityId { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private CreateActivityResult(bool succeeded, Guid? activityId, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        ActivityId = activityId;
        Errors = errors;
    }

    public static CreateActivityResult Success(Guid activityId)
    {
        return new CreateActivityResult(true, activityId, Array.Empty<string>());
    }

    public static CreateActivityResult Failure(IEnumerable<string> errors)
    {
        return new CreateActivityResult(false, null, errors.ToArray());
    }
}