namespace EsportTeamManager.Application.Accounts;

public sealed class ChangeAccountPasswordResult
{
    public bool Succeeded { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private ChangeAccountPasswordResult(bool succeeded, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static ChangeAccountPasswordResult Success()
    {
        return new ChangeAccountPasswordResult(true, Array.Empty<string>());
    }

    public static ChangeAccountPasswordResult Failure(params string[] errors)
    {
        return new ChangeAccountPasswordResult(false, errors);
    }
}