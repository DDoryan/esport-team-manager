using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace EsportTeamManager.Tests.Integration.Web;

public sealed class SecurityPipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityPipelineTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WhenRequested_AddsCorrelationHeader()
    {
        using HttpClient client = CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/Account/Login");

        response.EnsureSuccessStatusCode();

        IEnumerable<string> correlationIds = response.Headers.GetValues("X-Correlation-ID");
        string correlationId = Assert.Single(correlationIds);

        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task HomePost_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using HttpClient client = CreateHttpsClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/Home/Index");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Error_WhenRequested_ReturnsGenericPageWithMatchingCorrelationIdentifier()
    {
        using HttpClient client = CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/Home/Error");
        string content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        IEnumerable<string> correlationIds = response.Headers.GetValues("X-Correlation-ID");
        string correlationId = Assert.Single(correlationIds);

        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Contains("Une erreur est survenue", content);
        Assert.Contains("Identifiant de suivi", content);
        Assert.Contains(correlationId, content);
        Assert.DoesNotContain("Development Mode", content);
        Assert.DoesNotContain("Stack trace", content);
    }

    [Fact]
    public async Task ServerValidation_WhenRequiredValueIsMissing_ReturnsBadRequest()
    {
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(SecurityValidationController).Assembly);
            });
        });

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using StringContent requestContent = new("{}", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync("/__tests/security-validation", requestContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Activities_WhenUserIsAnonymous_RedirectsToLogin()
    {
        using HttpClient client = CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/Activities");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        string? redirectLocation = response.Headers.Location?.OriginalString;

        Assert.NotNull(redirectLocation);
        Assert.Contains("/Account/Login", redirectLocation);
        Assert.Contains("ReturnUrl=%2FActivities", redirectLocation);
    }

    private HttpClient CreateHttpsClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }
}