namespace EsportTeamManager.Application.Teams;

public sealed class CreateTeamResult
{
    public bool Succeeded { get; }

    public Guid? TeamId { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private CreateTeamResult(bool succeeded, Guid? teamId, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        TeamId = teamId;
        Errors = errors;
    }

    public static CreateTeamResult Success(Guid teamId)
    {
        return new CreateTeamResult(true, teamId, Array.Empty<string>());
    }

    public static CreateTeamResult Failure(IEnumerable<string> errors)
    {
        return new CreateTeamResult(false, null, errors.ToArray());
    }
}