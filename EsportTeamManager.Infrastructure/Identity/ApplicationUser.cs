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

    public DateTimeOffset? EmailConfirmationSentAtUtc { get; private set; }

    public DateTimeOffset? PasswordResetEmailWindowStartedAtUtc { get; private set; }

    public int PasswordResetEmailCount { get; private set; }

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
        EmailConfirmationSentAtUtc = null;
        PasswordResetEmailWindowStartedAtUtc = null;
        PasswordResetEmailCount = 0;
        ConfirmedAtUtc = null;
    }

    public bool CanSendConfirmationEmail(DateTimeOffset currentDateUtc)
    {
        DateTimeOffset normalizedCurrentDateUtc = currentDateUtc.ToUniversalTime();

        return EmailConfirmationSentAtUtc is null || normalizedCurrentDateUtc >= EmailConfirmationSentAtUtc.Value.AddMinutes(1);
    }

    public void RecordConfirmationEmailSent(DateTimeOffset sentAtUtc)
    {
        EmailConfirmationSentAtUtc = sentAtUtc.ToUniversalTime();
    }

    public bool CanSendPasswordResetEmail(DateTimeOffset currentDateUtc)
    {
        DateTimeOffset normalizedCurrentDateUtc = currentDateUtc.ToUniversalTime();

        if (PasswordResetEmailWindowStartedAtUtc is null || normalizedCurrentDateUtc >= PasswordResetEmailWindowStartedAtUtc.Value.AddHours(1))
        {
            return true;
        }

        return PasswordResetEmailCount < 3;
    }

    public void RecordPasswordResetEmailSent(DateTimeOffset sentAtUtc)
    {
        DateTimeOffset normalizedSentAtUtc = sentAtUtc.ToUniversalTime();

        if (PasswordResetEmailWindowStartedAtUtc is null || normalizedSentAtUtc >= PasswordResetEmailWindowStartedAtUtc.Value.AddHours(1))
        {
            PasswordResetEmailWindowStartedAtUtc = normalizedSentAtUtc;
            PasswordResetEmailCount = 1;

            return;
        }

        PasswordResetEmailCount++;
    }

    public void ReservePendingEmail(string email, string normalizedEmail, DateTimeOffset requestedAtUtc)
    {
        string pendingEmail = email.Trim();

        if (pendingEmail.Length == 0 || pendingEmail.Length > 254)
        {
            throw new DomainException("The pending email address is required and cannot exceed 254 characters.");
        }

        string normalizedPendingEmail = normalizedEmail.Trim();

        if (normalizedPendingEmail.Length == 0 || normalizedPendingEmail.Length > 254)
        {
            throw new DomainException("The normalized pending email address is required and cannot exceed 254 characters.");
        }

        if (string.Equals(NormalizedEmail, normalizedPendingEmail, StringComparison.Ordinal))
        {
            throw new DomainException("The pending email address must differ from the current email address.");
        }

        PendingEmail = pendingEmail;
        NormalizedPendingEmail = normalizedPendingEmail;
        PendingEmailExpiresAtUtc = requestedAtUtc.ToUniversalTime().AddHours(1);
    }

    public bool HasValidPendingEmail(DateTimeOffset currentDateUtc)
    {
        DateTimeOffset normalizedCurrentDateUtc = currentDateUtc.ToUniversalTime();

        return !string.IsNullOrWhiteSpace(PendingEmail)
            && !string.IsNullOrWhiteSpace(NormalizedPendingEmail)
            && PendingEmailExpiresAtUtc is not null
            && normalizedCurrentDateUtc < PendingEmailExpiresAtUtc.Value;
    }

    public void ClearPendingEmail()
    {
        PendingEmail = null;
        NormalizedPendingEmail = null;
        PendingEmailExpiresAtUtc = null;
    }

    public void MarkAsConfirmed(DateTimeOffset confirmedAtUtc)
    {
        AccountStatus = AccountStatus.Active;
        ConfirmedAtUtc = confirmedAtUtc.ToUniversalTime();
    }
}