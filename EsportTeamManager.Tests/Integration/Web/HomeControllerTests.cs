using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace EsportTeamManager.Tests.Integration.Web;

public sealed class HomeControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HomeControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/mentions-legales", "Mentions légales")]
    [InlineData("/conditions-generales-utilisation", "Conditions générales d’utilisation")]
    [InlineData("/politique-confidentialite", "Responsable du traitement")]
    public async Task PublicInformationPage_WhenRequestedByAnonymousUser_ReturnsExpectedContent(string requestPath, string expectedContent)
    {
        using HttpClient client = CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(requestPath);
        string content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedContent, content);
    }

    [Fact]
    public async Task Login_WhenRequested_DisplaysCompletePublicFooter()
    {
        using HttpClient client = CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/Account/Login");
        string content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        string expectedVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        response.EnsureSuccessStatusCode();

        Assert.Contains("href=\"/mentions-legales\"", content);
        Assert.Contains("href=\"/conditions-generales-utilisation\"", content);
        Assert.Contains("href=\"/politique-confidentialite\"", content);
        Assert.Contains(">Politique de confidentialité</a>", content);
        Assert.Contains("href=\"mailto:doryan.coach@gmail.com\"", content);
        Assert.Contains(">Envoyer un mail</a>", content);
        Assert.Contains($"class=\"app-version\">v{expectedVersion}</span>", content);
    }

    [Fact]
    public async Task Register_WhenBeforeSubmission_DisplaysTermsAndPrivacyLinks()
    {
        using HttpClient client = CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/Account/Register");
        string content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.EnsureSuccessStatusCode();

        Assert.Contains("J’accepte les conditions générales d’utilisation.", content);
        Assert.Contains("href=\"/conditions-generales-utilisation\"", content);
        Assert.Contains("Lire les CGU", content);
        Assert.Contains("href=\"/politique-confidentialite\"", content);
        Assert.Contains("Consulter la politique de confidentialité", content);
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