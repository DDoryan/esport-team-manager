namespace EsportTeamManager.Application.Accounts;

public sealed class AccountPasswordResetResult
{
    public bool Succeeded { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private AccountPasswordResetResult(bool succeeded, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static AccountPasswordResetResult Success()
    {
        return new AccountPasswordResetResult(true, Array.Empty<string>());
    }

    public static AccountPasswordResetResult Failure(params string[] errors)
    {
        return new AccountPasswordResetResult(false, errors);
    }
}