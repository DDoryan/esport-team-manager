namespace EsportTeamManager.Application.Emails;

public sealed class EmailMessage
{
    public string Recipient { get; }

    public string Subject { get; }

    public string HtmlContent { get; }

    public EmailMessage(string recipient, string subject, string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new ArgumentException("Le destinataire est obligatoire.", nameof(recipient));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Le sujet est obligatoire.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException("Le contenu est obligatoire.", nameof(htmlContent));
        }

        Recipient = recipient.Trim();
        Subject = subject.Trim();
        HtmlContent = htmlContent;
    }
}