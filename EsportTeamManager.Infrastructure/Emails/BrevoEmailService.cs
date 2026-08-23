using System.Net.Http.Headers;
using System.Net.Http.Json;
using EsportTeamManager.Application.Emails;
using Microsoft.Extensions.Options;

namespace EsportTeamManager.Infrastructure.Emails;

public sealed class BrevoEmailService : IEmailService
{
    private const string SendEmailPath = "v3/smtp/email";

    private readonly HttpClient _httpClient;
    private readonly BrevoEmailOptions _options;

    public BrevoEmailService(HttpClient httpClient, IOptions<BrevoEmailOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        object requestBody = new
        {
            sender = new
            {
                email = _options.SenderEmail,
                name = _options.SenderName
            },
            to = new[]
            {
                new
                {
                    email = message.Recipient
                }
            },
            subject = message.Subject,
            htmlContent = message.HtmlContent
        };

        using HttpRequestMessage request = new(HttpMethod.Post, SendEmailPath);
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(requestBody);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}