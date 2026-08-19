using EsportTeamManager.Application.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EsportTeamManager.Web.Services.Accounts;

public sealed class EmailConfirmationLinkFactory : IEmailConfirmationLinkFactory
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmailConfirmationLinkFactory(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public string CreateEmailConfirmationLink(Guid userId, string token)
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Le contexte HTTP est indisponible.");
        string? confirmationLink = _linkGenerator.GetUriByAction(httpContext, action: "ConfirmEmail", controller: "Account", values: new { userId, token });

        return confirmationLink ?? throw new InvalidOperationException("Le lien de confirmation n’a pas pu être généré.");
    }
}