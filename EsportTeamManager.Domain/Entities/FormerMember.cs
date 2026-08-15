using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class FormerMember
{
    public Guid FormerMemberId { get; private set; }

    public Guid TeamId { get; private set; }

    public int LocalNumber { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private FormerMember()
    {
    }

    public FormerMember(Guid formerMemberId, Guid teamId, int localNumber, DateTimeOffset createdAtUtc)
    {
        if (formerMemberId == Guid.Empty)
        {
            throw new DomainException("The former member identifier cannot be empty.");
        }

        if (teamId == Guid.Empty)
        {
            throw new DomainException("The team identifier cannot be empty.");
        }

        if (localNumber <= 0)
        {
            throw new DomainException("The former member local number must be positive.");
        }

        FormerMemberId = formerMemberId;
        TeamId = teamId;
        LocalNumber = localNumber;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }
}