namespace EsportTeamManager.Application.Teams;

public sealed class DeleteTeamResult
{
    public bool Succeeded { get; }

    public bool AccessDenied { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private DeleteTeamResult(bool succeeded, bool accessDenied, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        AccessDenied = accessDenied;
        Errors = errors;
    }

    public static DeleteTeamResult Success()
    {
        return new DeleteTeamResult(true, false, Array.Empty<string>());
    }

    public static DeleteTeamResult Denied()
    {
        return new DeleteTeamResult(false, true, Array.Empty<string>());
    }

    public static DeleteTeamResult Failure(IEnumerable<string> errors)
    {
        return new DeleteTeamResult(false, false, errors.ToArray());
    }
}