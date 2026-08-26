using Microsoft.AspNetCore.Http;

namespace EsportTeamManager.Web.Navigation;

public static class LastVisitedTeamCookie
{
    private const string CookieName = "EsportTeamManager.LastVisitedTeamId";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static Guid? Read(HttpRequest request)
    {
        string? value = request.Cookies[CookieName];

        return Guid.TryParse(value, out Guid teamId) ? teamId : null;
    }

    public static void Write(HttpResponse response, Guid teamId)
    {
        CookieOptions options = CreateCookieOptions();

        response.Cookies.Append(CookieName, teamId.ToString(), options);
    }

    public static void Delete(HttpResponse response)
    {
        CookieOptions options = CreateCookieOptions();

        response.Cookies.Delete(CookieName, options);
    }

    private static CookieOptions CreateCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = Lifetime,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = true
        };
    }
}