namespace EsportTeamManager.Application.Accounts;

public sealed class RegisterAccountResult
{
    public bool Succeeded { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private RegisterAccountResult(bool succeeded, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static RegisterAccountResult Success()
    {
        return new RegisterAccountResult(true, Array.Empty<string>());
    }

    public static RegisterAccountResult Failure(IEnumerable<string> errors)
    {
        return new RegisterAccountResult(false, errors.ToArray());
    }
}