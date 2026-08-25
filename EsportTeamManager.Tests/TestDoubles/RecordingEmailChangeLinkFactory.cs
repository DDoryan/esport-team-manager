using EsportTeamManager.Application.Accounts;

namespace EsportTeamManager.Tests.TestDoubles;

public sealed class RecordingEmailChangeLinkFactory : IEmailChangeLinkFactory
{
    public Guid? LastUserId { get; private set; }

    public string? LastToken { get; private set; }

    public string CreateEmailChangeLink(Guid userId, string token)
    {
        LastUserId = userId;
        LastToken = token;

        return $"https://example.test/Account/ConfirmEmailChange?userId={userId}&token={Uri.EscapeDataString(token)}";
    }
}