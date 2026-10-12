using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Services;

internal static class AuthJwtBearerEvents
{
    public static JwtBearerEvents Create() =>
        new()
        {
            OnMessageReceived = context =>
            {
                if (
                    string.IsNullOrEmpty(context.Token)
                    && context.Request.Cookies.TryGetValue(AuthCookieOptions.CookieName, out var cookieToken)
                )
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<UserSessionValidator>();
                if (
                    context.Principal is null
                    || !await validator
                        .IsActiveAsync(context.Principal, context.HttpContext.RequestAborted)
                        .ConfigureAwait(false)
                )
                {
                    context.Fail("The session is no longer valid.");
                }
            },
        };
}
