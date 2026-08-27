namespace EsportTeamManager.Application.Teams;

public sealed class OwnershipTransferActionResult
{
    public bool Succeeded { get; }

    public bool AccessDenied { get; }

    public Guid? OwnershipTransferId { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private OwnershipTransferActionResult(bool succeeded, bool accessDenied, Guid? ownershipTransferId, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        AccessDenied = accessDenied;
        OwnershipTransferId = ownershipTransferId;
        Errors = errors;
    }

    public static OwnershipTransferActionResult Success(Guid ownershipTransferId)
    {
        return new OwnershipTransferActionResult(true, false, ownershipTransferId, Array.Empty<string>());
    }

    public static OwnershipTransferActionResult Denied()
    {
        return new OwnershipTransferActionResult(false, true, null, Array.Empty<string>());
    }

    public static OwnershipTransferActionResult Failure(IEnumerable<string> errors)
    {
        return new OwnershipTransferActionResult(false, false, null, errors.ToArray());
    }
}