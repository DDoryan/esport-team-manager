namespace EsportTeamManager.Application.Accounts;

public sealed class ChangeAccountEmailResult
{
    public bool Succeeded { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private ChangeAccountEmailResult(bool succeeded, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static ChangeAccountEmailResult Success()
    {
        return new ChangeAccountEmailResult(true, Array.Empty<string>());
    }

    public static ChangeAccountEmailResult Failure(params string[] errors)
    {
        return new ChangeAccountEmailResult(false, errors);
    }
}