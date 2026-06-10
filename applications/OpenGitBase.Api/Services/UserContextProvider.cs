using System.Security.Claims;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Services;

public class UserContextProvider : IUserContext
{
    public UserContextProvider(IHttpContextAccessor contextAccessor, IQueryProcessor queryProcessor)
    {
        var httpContext =
            contextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available.");

        var username =
            httpContext.User.FindFirstValue(ClaimTypes.Name)
            ?? httpContext.User.FindFirstValue("name")
            ?? throw new InvalidOperationException("Username claim is missing from JWT.");

        var identityProviderId =
            httpContext.User.FindFirstValue("identityproviderid")
            ?? throw new InvalidOperationException("identityproviderid claim is missing from JWT.");

        var userId = Guid.Parse(identityProviderId);
        var loadedUser = queryProcessor
            .RunQueryAsync(
                new UserGetByIdQuery { ModelId = UserId.From(userId) },
                CancellationToken.None
            )
            .GetAwaiter()
            .GetResult();

        if (loadedUser.IsNone)
        {
            throw new InvalidOperationException("Could not find user for JWT.");
        }

        User = new UserIdentity { Username = username, IdentityProviderId = identityProviderId };
    }

    public UserIdentity User { get; }
}
