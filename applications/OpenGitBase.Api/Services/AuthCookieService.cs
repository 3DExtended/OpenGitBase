using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Services;

public class AuthCookieService : IAuthCookieService
{
    private readonly IWebHostEnvironment _environment;

    public AuthCookieService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public void SetAuthCookie(HttpResponse response, string jwtToken)
    {
        response.Cookies.Append(
            AuthCookieOptions.CookieName,
            jwtToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = UseSecureCookies(),
                SameSite = SameSiteMode.Lax,
                Path = "/",
            }
        );
    }

    public void ClearAuthCookie(HttpResponse response)
    {
        response.Cookies.Delete(
            AuthCookieOptions.CookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = UseSecureCookies(),
                SameSite = SameSiteMode.Lax,
                Path = "/",
            }
        );
    }

    private bool UseSecureCookies() =>
        !_environment.IsDevelopment()
        && !string.Equals(
            _environment.EnvironmentName,
            "E2ETest",
            StringComparison.OrdinalIgnoreCase
        );
}
