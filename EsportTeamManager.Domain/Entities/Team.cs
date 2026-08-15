using EsportTeamManager.Domain.Exceptions;

namespace EsportTeamManager.Domain.Entities;

public class Team
{
    public Guid TeamId { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Tag { get; private set; }

    public string? Description { get; private set; }

    public string TimeZoneId { get; private set; } = "Europe/Paris";

    public int DeletedMemberCounter { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Team()
    {
    }

    public Team(Guid teamId, Guid ownerUserId, string name, DateTimeOffset createdAtUtc, string? tag = null, string? description = null, string timeZoneId = "Europe/Paris")
    {
        if (teamId == Guid.Empty)
        {
            throw new DomainException("The team identifier cannot be empty.");
        }

        if (ownerUserId == Guid.Empty)
        {
            throw new DomainException("The owner identifier cannot be empty.");
        }

        TeamId = teamId;
        OwnerUserId = ownerUserId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        DeletedMemberCounter = 0;

        UpdateInformation(name, tag, description, timeZoneId);
    }

    public void UpdateInformation(string name, string? tag, string? description, string timeZoneId)
    {
        string normalizedName = name.Trim();

        if (normalizedName.Length < 3 || normalizedName.Length > 50)
        {
            throw new DomainException("The team name must contain between 3 and 50 characters.");
        }

        string? normalizedTag = NormalizeOptionalText(tag);

        if (normalizedTag is not null && (normalizedTag.Length < 2 || normalizedTag.Length > 6))
        {
            throw new DomainException("The team tag must contain between 2 and 6 characters.");
        }

        string? normalizedDescription = NormalizeOptionalText(description);

        if (normalizedDescription is not null && normalizedDescription.Length > 500)
        {
            throw new DomainException("The team description cannot exceed 500 characters.");
        }

        string normalizedTimeZone = timeZoneId.Trim();

        if (normalizedTimeZone.Length == 0 || normalizedTimeZone.Length > 64)
        {
            throw new DomainException("The team time zone must contain between 1 and 64 characters.");
        }

        Name = normalizedName;
        Tag = normalizedTag;
        Description = normalizedDescription;
        TimeZoneId = normalizedTimeZone;
    }

    public void TransferOwnership(Guid newOwnerUserId)
    {
        if (newOwnerUserId == Guid.Empty)
        {
            throw new DomainException("The new owner identifier cannot be empty.");
        }

        if (newOwnerUserId == OwnerUserId)
        {
            throw new DomainException("The selected user already owns the team.");
        }

        OwnerUserId = newOwnerUserId;
    }

    public int ReserveDeletedMemberNumber()
    {
        DeletedMemberCounter = checked(DeletedMemberCounter + 1);

        return DeletedMemberCounter;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}