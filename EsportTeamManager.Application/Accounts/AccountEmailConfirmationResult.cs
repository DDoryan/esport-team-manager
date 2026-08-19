namespace EsportTeamManager.Application.Accounts;

public sealed class AccountEmailConfirmationResult
{
    public bool Succeeded { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private AccountEmailConfirmationResult(bool succeeded, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static AccountEmailConfirmationResult Success()
    {
        return new AccountEmailConfirmationResult(true, Array.Empty<string>());
    }

    public static AccountEmailConfirmationResult Failure(params string[] errors)
    {
        return new AccountEmailConfirmationResult(false, errors);
    }
}