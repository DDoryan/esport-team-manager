using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

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

    [Fact]
    public async Task Health_WhenRailwayForwardsHttps_DoesNotRedirect()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        using HttpRequestMessage request = new(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForwardedHeaders_WhenRailwayForwardsHttps_UsesForwardedScheme()
    {
        IOptions<ForwardedHeadersOptions> options = _factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        ILoggerFactory loggerFactory = _factory.Services.GetRequiredService<ILoggerFactory>();
        DefaultHttpContext context = new();

        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        ForwardedHeadersMiddleware middleware = new(_ => Task.CompletedTask, loggerFactory, options);

        await middleware.Invoke(context);

        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ForwardedHeaders_WhenNoForwardedProtoIsProvided_KeepsHttpScheme()
    {
        IOptions<ForwardedHeadersOptions> options = _factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        ILoggerFactory loggerFactory = _factory.Services.GetRequiredService<ILoggerFactory>();
        DefaultHttpContext context = new();

        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Scheme = "http";

        ForwardedHeadersMiddleware middleware = new(_ => Task.CompletedTask, loggerFactory, options);

        await middleware.Invoke(context);

        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), context.Connection.RemoteIpAddress);
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