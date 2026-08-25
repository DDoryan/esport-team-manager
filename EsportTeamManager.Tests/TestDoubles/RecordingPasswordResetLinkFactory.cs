using EsportTeamManager.Application.Accounts;

namespace EsportTeamManager.Tests.TestDoubles;

public sealed class RecordingPasswordResetLinkFactory : IPasswordResetLinkFactory
{
    public Guid? LastUserId { get; private set; }

    public string? LastToken { get; private set; }

    public string CreatePasswordResetLink(Guid userId, string token)
    {
        LastUserId = userId;
        LastToken = token;

        return $"https://example.test/Account/ResetPassword?userId={userId:D}&token={Uri.EscapeDataString(token)}";
    }
}