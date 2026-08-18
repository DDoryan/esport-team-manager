using System.Collections.Concurrent;
using EsportTeamManager.Application.Emails;

namespace EsportTeamManager.Infrastructure.Emails;

public sealed class DevelopmentEmailService : IEmailService
{
    private readonly ConcurrentQueue<EmailMessage> _sentEmails;

    public IReadOnlyCollection<EmailMessage> SentEmails => _sentEmails.ToArray();

    public DevelopmentEmailService()
    {
        _sentEmails = new ConcurrentQueue<EmailMessage>();
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        _sentEmails.Enqueue(message);

        return Task.CompletedTask;
    }
}