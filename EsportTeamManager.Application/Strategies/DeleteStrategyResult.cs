namespace EsportTeamManager.Application.Strategies;

public sealed class DeleteStrategyResult
{
    public bool Succeeded { get; }

    public int DeletedAssociationCount { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private DeleteStrategyResult(bool succeeded, int deletedAssociationCount, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        DeletedAssociationCount = deletedAssociationCount;
        Errors = errors;
    }

    public static DeleteStrategyResult Success(int deletedAssociationCount)
    {
        return new DeleteStrategyResult(true, deletedAssociationCount, Array.Empty<string>());
    }

    public static DeleteStrategyResult Failure(IReadOnlyCollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new DeleteStrategyResult(false, 0, errors.ToArray());
    }
}