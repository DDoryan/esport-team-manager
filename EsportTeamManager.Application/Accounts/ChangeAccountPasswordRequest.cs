namespace EsportTeamManager.Application.Accounts;

public sealed class ChangeAccountPasswordRequest
{
    public Guid UserId { get; }

    public string CurrentPassword { get; }

    public string NewPassword { get; }

    public ChangeAccountPasswordRequest(Guid userId, string currentPassword, string newPassword)
    {
        UserId = userId;
        CurrentPassword = currentPassword;
        NewPassword = newPassword;
    }
}