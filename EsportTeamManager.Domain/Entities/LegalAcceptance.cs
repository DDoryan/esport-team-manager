using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class LegalAcceptance
{
    public Guid UserId { get; private set; }

    public int LegalDocumentVersionId { get; private set; }

    public DateTimeOffset AcceptedAtUtc { get; private set; }

    private LegalAcceptance()
    {
    }

    public LegalAcceptance(Guid userId, int legalDocumentVersionId, DateTimeOffset acceptedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("The user identifier cannot be empty.");
        }

        if (legalDocumentVersionId <= 0)
        {
            throw new DomainException("The legal document version identifier must be positive.");
        }

        UserId = userId;
        LegalDocumentVersionId = legalDocumentVersionId;
        AcceptedAtUtc = acceptedAtUtc.ToUniversalTime();
    }
}