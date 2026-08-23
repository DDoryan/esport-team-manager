using System.Net;
using System.Text.Json;
using EsportTeamManager.Application.Emails;
using EsportTeamManager.Infrastructure.Emails;
using Microsoft.Extensions.Options;

namespace EsportTeamManager.Tests.Infrastructure.Emails;

public sealed class BrevoEmailServiceTests
{
    [Fact]
    public async Task SendAsync_WhenMessageIsValid_SendsExpectedRequest()
    {
        RecordingHttpMessageHandler messageHandler = new(HttpStatusCode.Created);
        using HttpClient httpClient = new(messageHandler)
        {
            BaseAddress = new Uri("https://api.brevo.com/")
        };

        BrevoEmailOptions options = new()
        {
            ApiKey = "test-api-key",
            SenderEmail = "contact@example.test",
            SenderName = "Esport Team Manager"
        };

        BrevoEmailService emailService = new(httpClient, Options.Create(options));
        EmailMessage message = new("recipient@example.test", "Confirmation", "<p>Confirmez votre compte.</p>");

        await emailService.SendAsync(message);

        Assert.Equal(HttpMethod.Post, messageHandler.Method);
        Assert.Equal(new Uri("https://api.brevo.com/v3/smtp/email"), messageHandler.RequestUri);
        Assert.Equal("test-api-key", messageHandler.ApiKey);
        Assert.NotNull(messageHandler.Content);

        using JsonDocument document = JsonDocument.Parse(messageHandler.Content);
        JsonElement root = document.RootElement;
        JsonElement sender = root.GetProperty("sender");
        JsonElement recipient = Assert.Single(root.GetProperty("to").EnumerateArray().ToArray());

        Assert.Equal("contact@example.test", sender.GetProperty("email").GetString());
        Assert.Equal("Esport Team Manager", sender.GetProperty("name").GetString());
        Assert.Equal("recipient@example.test", recipient.GetProperty("email").GetString());
        Assert.Equal("Confirmation", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Confirmez votre compte.</p>", root.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task SendAsync_WhenBrevoReturnsAnError_ThrowsHttpRequestException()
    {
        RecordingHttpMessageHandler messageHandler = new(HttpStatusCode.InternalServerError);
        using HttpClient httpClient = new(messageHandler)
        {
            BaseAddress = new Uri("https://api.brevo.com/")
        };

        BrevoEmailOptions options = new()
        {
            ApiKey = "test-api-key",
            SenderEmail = "contact@example.test",
            SenderName = "Esport Team Manager"
        };

        BrevoEmailService emailService = new(httpClient, Options.Create(options));
        EmailMessage message = new("recipient@example.test", "Confirmation", "<p>Confirmez votre compte.</p>");

        await Assert.ThrowsAsync<HttpRequestException>(() => emailService.SendAsync(message));
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? ApiKey { get; private set; }

        public string? Content { get; private set; }

        public RecordingHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("api-key").Single();
            Content = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode);
        }
    }
}