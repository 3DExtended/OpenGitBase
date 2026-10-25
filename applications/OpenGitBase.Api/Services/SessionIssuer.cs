using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Services;

/// <summary>Hands a user a fresh session: the token, also set as the auth cookie.</summary>
public sealed class SessionIssuer
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IJWTTokenGenerator _jwtTokenGenerator;
    private readonly IAuthCookieService _authCookieService;

    public SessionIssuer(
        IQueryProcessor queryProcessor,
        IJWTTokenGenerator jwtTokenGenerator,
        IAuthCookieService authCookieService
    )
    {
        _queryProcessor = queryProcessor;
        _jwtTokenGenerator = jwtTokenGenerator;
        _authCookieService = authCookieService;
    }

    /// <summary>The new session token, or null when the user no longer exists.</summary>
    public async Task<string?> IssueAsync(HttpResponse response, UserId userId, CancellationToken cancellationToken)
    {
        var user = await _queryProcessor
            .RunQueryAsync(new UserGetByIdQuery { ModelId = userId }, cancellationToken)
            .ConfigureAwait(false);
        if (user.IsNone)
        {
            return null;
        }

        var token = _jwtTokenGenerator.GetJWTToken(user.Get().Username, userId.Value.ToString(), user.Get().IsAdmin);
        _authCookieService.SetAuthCookie(response, token);
        return token;
    }
}
