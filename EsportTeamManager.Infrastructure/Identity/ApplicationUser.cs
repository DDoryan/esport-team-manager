using EsportTeamManager.Domain.Enums;
using EsportTeamManager.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string Pseudo { get; private set; } = string.Empty;

    public string Tag { get; private set; } = string.Empty;

    public string? PendingEmail { get; private set; }

    public string? NormalizedPendingEmail { get; private set; }

    public DateTimeOffset? PendingEmailExpiresAtUtc { get; private set; }

    public AccountStatus AccountStatus { get; private set; }

    public DateTimeOffset MinimumAgeDeclaredAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    private ApplicationUser()
    {
    }

    public ApplicationUser(Guid userId, string email, string pseudo, string tag, DateTimeOffset minimumAgeDeclaredAtUtc, DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("The user identifier cannot be empty.");
        }

        string normalizedEmail = email.Trim();

        if (normalizedEmail.Length == 0 || normalizedEmail.Length > 254)
        {
            throw new DomainException("The email address is required and cannot exceed 254 characters.");
        }

        string normalizedPseudo = pseudo.Trim();

        if (normalizedPseudo.Length < 3 || normalizedPseudo.Length > 20)
        {
            throw new DomainException("The pseudonym must contain between 3 and 20 characters.");
        }

        string normalizedTag = tag.Trim();

        if (normalizedTag.Length < 3 || normalizedTag.Length > 5 || !normalizedTag.All(char.IsLetterOrDigit))
        {
            throw new DomainException("The tag must contain between 3 and 5 alphanumeric characters.");
        }

        Id = userId;
        Email = normalizedEmail;
        UserName = $"{normalizedPseudo}#{normalizedTag}";
        Pseudo = normalizedPseudo;
        Tag = normalizedTag;
        PendingEmail = null;
        NormalizedPendingEmail = null;
        PendingEmailExpiresAtUtc = null;
        AccountStatus = AccountStatus.PendingConfirmation;
        MinimumAgeDeclaredAtUtc = minimumAgeDeclaredAtUtc.ToUniversalTime();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        ConfirmedAtUtc = null;
    }
}