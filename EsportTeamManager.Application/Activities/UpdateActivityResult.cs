namespace EsportTeamManager.Application.Activities;

public sealed class UpdateActivityResult
{
    public bool Succeeded { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private UpdateActivityResult(bool succeeded, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static UpdateActivityResult Success()
    {
        return new UpdateActivityResult(true, Array.Empty<string>());
    }

    public static UpdateActivityResult Failure(IEnumerable<string> errors)
    {
        return new UpdateActivityResult(false, errors.ToArray());
    }
}