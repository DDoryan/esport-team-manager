namespace EsportTeamManager.Application.Teams;

public sealed class TeamMembershipActionResult
{
    public bool Succeeded { get; }

    public bool AccessDenied { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private TeamMembershipActionResult(bool succeeded, bool accessDenied, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        AccessDenied = accessDenied;
        Errors = errors;
    }

    public static TeamMembershipActionResult Success()
    {
        return new TeamMembershipActionResult(true, false, Array.Empty<string>());
    }

    public static TeamMembershipActionResult Denied()
    {
        return new TeamMembershipActionResult(false, true, Array.Empty<string>());
    }

    public static TeamMembershipActionResult Failure(IEnumerable<string> errors)
    {
        return new TeamMembershipActionResult(false, false, errors.ToArray());
    }
}