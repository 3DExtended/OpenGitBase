using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Tests.Services;

public class AuthCookieServiceTests
{
    [Fact]
    public void SetAuthCookie_InDevelopment_UsesNonSecureCookie()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var response = new DefaultHttpContext().Response;
        var service = new AuthCookieService(environment);

        service.SetAuthCookie(response, "jwt-token");

        var cookie = response.Headers.SetCookie.ToString();
        Assert.Contains(AuthCookieOptions.CookieName, cookie);
        Assert.Contains("jwt-token", cookie);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetAuthCookie_InProduction_UsesSecureCookie()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);

        var response = new DefaultHttpContext().Response;
        var service = new AuthCookieService(environment);

        service.SetAuthCookie(response, "jwt-token");

        var cookie = response.Headers.SetCookie.ToString();
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearAuthCookie_DeletesCookie()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var response = new DefaultHttpContext().Response;
        var service = new AuthCookieService(environment);

        service.ClearAuthCookie(response);

        var cookie = response.Headers.SetCookie.ToString();
        Assert.Contains(AuthCookieOptions.CookieName, cookie);
    }
}
