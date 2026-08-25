using EsportTeamManager.Application.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EsportTeamManager.Web.Services.Accounts;

public sealed class EmailChangeLinkFactory : IEmailChangeLinkFactory
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmailChangeLinkFactory(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public string CreateEmailChangeLink(Guid userId, string token)
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Le contexte HTTP est indisponible.");
        string? emailChangeLink = _linkGenerator.GetUriByAction(httpContext, action: "ConfirmEmailChange", controller: "Account", values: new { userId, token });

        return emailChangeLink ?? throw new InvalidOperationException("Le lien de changement d’adresse électronique n’a pas pu être généré.");
    }
}