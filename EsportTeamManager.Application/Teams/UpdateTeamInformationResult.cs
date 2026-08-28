namespace EsportTeamManager.Application.Teams;

public sealed class UpdateTeamInformationResult
{
    public bool Succeeded { get; }

    public bool AccessDenied { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private UpdateTeamInformationResult(bool succeeded, bool accessDenied, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        AccessDenied = accessDenied;
        Errors = errors;
    }

    public static UpdateTeamInformationResult Success()
    {
        return new UpdateTeamInformationResult(true, false, Array.Empty<string>());
    }

    public static UpdateTeamInformationResult Denied()
    {
        return new UpdateTeamInformationResult(false, true, Array.Empty<string>());
    }

    public static UpdateTeamInformationResult Failure(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new UpdateTeamInformationResult(false, false, errors.ToArray());
    }
}