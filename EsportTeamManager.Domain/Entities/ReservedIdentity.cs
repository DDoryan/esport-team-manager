using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class ReservedIdentity
{
    public string IdentityHash { get; private set; } = string.Empty;

    public DateTimeOffset ReservedAtUtc { get; private set; }

    private ReservedIdentity()
    {
    }

    public ReservedIdentity(string identityHash, DateTimeOffset reservedAtUtc)
    {
        string normalizedHash = identityHash.Trim();

        if (normalizedHash.Length != 64 || !normalizedHash.All(Uri.IsHexDigit))
        {
            throw new DomainException("The reserved identity hash must contain exactly 64 hexadecimal characters.");
        }

        IdentityHash = normalizedHash.ToUpperInvariant();
        ReservedAtUtc = reservedAtUtc.ToUniversalTime();
    }
}