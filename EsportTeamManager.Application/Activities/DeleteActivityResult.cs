namespace EsportTeamManager.Application.Activities;

public sealed class DeleteActivityResult
{
    public bool Succeeded { get; }

    public int DeletedParticipantCount { get; }

    public int DeletedLinkCount { get; }

    public int DeletedStrategyAssociationCount { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private DeleteActivityResult(bool succeeded, int deletedParticipantCount, int deletedLinkCount, int deletedStrategyAssociationCount, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        DeletedParticipantCount = deletedParticipantCount;
        DeletedLinkCount = deletedLinkCount;
        DeletedStrategyAssociationCount = deletedStrategyAssociationCount;
        Errors = errors;
    }

    public static DeleteActivityResult Success(int deletedParticipantCount, int deletedLinkCount, int deletedStrategyAssociationCount)
    {
        return new DeleteActivityResult(true, deletedParticipantCount, deletedLinkCount, deletedStrategyAssociationCount, Array.Empty<string>());
    }

    public static DeleteActivityResult Failure(IReadOnlyCollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new DeleteActivityResult(false, 0, 0, 0, errors.ToArray());
    }
}