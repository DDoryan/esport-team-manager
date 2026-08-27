namespace EsportTeamManager.Application.Notifications;

public sealed class InvitationActionResult
{
    public bool Succeeded { get; }

    public bool AccessDenied { get; }

    public Guid? TeamId { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private InvitationActionResult(bool succeeded, bool accessDenied, Guid? teamId, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        AccessDenied = accessDenied;
        TeamId = teamId;
        Errors = errors;
    }

    public static InvitationActionResult Success(Guid teamId)
    {
        return new InvitationActionResult(true, false, teamId, Array.Empty<string>());
    }

    public static InvitationActionResult Denied()
    {
        return new InvitationActionResult(false, true, null, Array.Empty<string>());
    }

    public static InvitationActionResult Failure(IEnumerable<string> errors)
    {
        return new InvitationActionResult(false, false, null, errors.ToArray());
    }
}