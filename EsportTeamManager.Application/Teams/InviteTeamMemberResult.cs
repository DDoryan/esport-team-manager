namespace EsportTeamManager.Application.Teams;

public sealed class InviteTeamMemberResult
{
    public bool Succeeded { get; }

    public bool AccessDenied { get; }

    public Guid? InvitationId { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private InviteTeamMemberResult(bool succeeded, bool accessDenied, Guid? invitationId, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        AccessDenied = accessDenied;
        InvitationId = invitationId;
        Errors = errors;
    }

    public static InviteTeamMemberResult Success(Guid invitationId)
    {
        return new InviteTeamMemberResult(true, false, invitationId, Array.Empty<string>());
    }

    public static InviteTeamMemberResult Denied()
    {
        return new InviteTeamMemberResult(false, true, null, Array.Empty<string>());
    }

    public static InviteTeamMemberResult Failure(IEnumerable<string> errors)
    {
        return new InviteTeamMemberResult(false, false, null, errors.ToArray());
    }
}