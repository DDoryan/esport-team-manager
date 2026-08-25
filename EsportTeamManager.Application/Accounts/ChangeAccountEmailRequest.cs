namespace EsportTeamManager.Application.Accounts;

public sealed class ChangeAccountEmailRequest
{
    public Guid UserId { get; }

    public string CurrentPassword { get; }

    public string NewEmail { get; }

    public ChangeAccountEmailRequest(Guid userId, string currentPassword, string newEmail)
    {
        UserId = userId;
        CurrentPassword = currentPassword;
        NewEmail = newEmail;
    }
}