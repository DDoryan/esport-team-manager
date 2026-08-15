using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class LegalDocumentVersion
{
    public int LegalDocumentVersionId { get; private set; }

    public LegalDocumentType DocumentType { get; private set; }

    public string VersionNumber { get; private set; } = string.Empty;

    public DateTimeOffset PublishedAtUtc { get; private set; }

    public bool RequiresAcceptance { get; private set; }

    private LegalDocumentVersion()
    {
    }

    public LegalDocumentVersion(int legalDocumentVersionId, LegalDocumentType documentType, string versionNumber, DateTimeOffset publishedAtUtc, bool requiresAcceptance)
    {
        if (legalDocumentVersionId <= 0)
        {
            throw new DomainException("The legal document version identifier must be positive.");
        }

        if (!Enum.IsDefined(typeof(LegalDocumentType), documentType))
        {
            throw new DomainException("The legal document type is invalid.");
        }

        string normalizedVersionNumber = versionNumber.Trim();

        if (normalizedVersionNumber.Length == 0 || normalizedVersionNumber.Length > 20)
        {
            throw new DomainException("The legal document version number must contain between 1 and 20 characters.");
        }

        LegalDocumentVersionId = legalDocumentVersionId;
        DocumentType = documentType;
        VersionNumber = normalizedVersionNumber;
        PublishedAtUtc = publishedAtUtc.ToUniversalTime();
        RequiresAcceptance = requiresAcceptance;
    }
}