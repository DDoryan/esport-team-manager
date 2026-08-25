using EsportTeamManager.Application.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EsportTeamManager.Web.Services.Accounts;

public sealed class PasswordResetLinkFactory : IPasswordResetLinkFactory
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PasswordResetLinkFactory(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public string CreatePasswordResetLink(Guid userId, string token)
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Le contexte HTTP est indisponible.");
        string? passwordResetLink = _linkGenerator.GetUriByAction(httpContext, action: "ResetPassword", controller: "Account", values: new { userId, token });

        return passwordResetLink ?? throw new InvalidOperationException("Le lien de réinitialisation du mot de passe n’a pas pu être généré.");
    }
}