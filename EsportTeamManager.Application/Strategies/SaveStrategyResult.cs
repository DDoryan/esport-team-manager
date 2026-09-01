namespace EsportTeamManager.Application.Strategies;

public sealed class SaveStrategyResult
{
    public bool Succeeded { get; }

    public Guid? StrategyId { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private SaveStrategyResult(bool succeeded, Guid? strategyId, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        StrategyId = strategyId;
        Errors = errors;
    }

    public static SaveStrategyResult Success(Guid strategyId)
    {
        if (strategyId == Guid.Empty)
        {
            throw new ArgumentException("The strategy identifier cannot be empty.", nameof(strategyId));
        }

        return new SaveStrategyResult(true, strategyId, Array.Empty<string>());
    }

    public static SaveStrategyResult Failure(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new SaveStrategyResult(false, null, errors.ToArray());
    }
}