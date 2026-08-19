using EsportTeamManager.Application.Accounts;

namespace EsportTeamManager.Tests.TestDoubles;

public sealed class RecordingEmailConfirmationLinkFactory : IEmailConfirmationLinkFactory
{
    public Guid? LastUserId { get; private set; }

    public string? LastToken { get; private set; }

    public string CreateEmailConfirmationLink(Guid userId, string token)
    {
        LastUserId = userId;
        LastToken = token;

        return $"https://example.test/Account/ConfirmEmail?userId={userId}&token={Uri.EscapeDataString(token)}";
    }
}