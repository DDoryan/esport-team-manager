using EsportTeamManager.Application.Emails;
using EsportTeamManager.Infrastructure.Emails;

namespace EsportTeamManager.Tests.Infrastructure.Emails;

public sealed class DevelopmentEmailServiceTests
{
    [Fact]
    public async Task SendAsync_WhenMessageIsValid_StoresRecipientSubjectAndContent()
    {
        DevelopmentEmailService emailService = new();
        EmailMessage message = new("player@example.test", "Confirmez votre adresse", "<p>Jeton de test</p>");

        await emailService.SendAsync(message);

        EmailMessage sentEmail = Assert.Single(emailService.SentEmails);

        Assert.Equal("player@example.test", sentEmail.Recipient);
        Assert.Equal("Confirmez votre adresse", sentEmail.Subject);
        Assert.Equal("<p>Jeton de test</p>", sentEmail.HtmlContent);
    }
}