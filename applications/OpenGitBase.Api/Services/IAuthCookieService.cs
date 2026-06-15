namespace OpenGitBase.Api.Services;

public interface IAuthCookieService
{
    void SetAuthCookie(HttpResponse response, string jwtToken);

    void ClearAuthCookie(HttpResponse response);
}
