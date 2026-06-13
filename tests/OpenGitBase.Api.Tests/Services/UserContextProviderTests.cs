using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Services;

public class UserContextProviderTests
{
    [Fact]
    public void Constructor_WhenHttpContextMissing_Throws()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var processor = Substitute.For<IQueryProcessor>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new UserContextProvider(accessor, processor)
        );
        Assert.Contains("HttpContext", ex.Message);
    }

    [Fact]
    public void Constructor_WhenUsernameClaimMissing_Throws()
    {
        var accessor = CreateAccessor(new ClaimsPrincipal(new ClaimsIdentity()));
        var processor = Substitute.For<IQueryProcessor>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new UserContextProvider(accessor, processor)
        );
        Assert.Contains("Username claim", ex.Message);
    }

    [Fact]
    public void Constructor_WhenUserNotFound_Throws()
    {
        var userId = Guid.NewGuid();
        var accessor = CreateAccessor(
            new ClaimsPrincipal(
                new ClaimsIdentity([
                    new Claim(ClaimTypes.Name, "testuser"),
                    new Claim("identityproviderid", userId.ToString()),
                ])
            )
        );
        var processor = Substitute.For<IQueryProcessor>();
        processor
            .RunQueryAsync(Arg.Any<UserGetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<User>.None);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new UserContextProvider(accessor, processor)
        );
        Assert.Contains("Could not find user", ex.Message);
    }

    [Fact]
    public void Constructor_WhenValid_ReturnsUserIdentity()
    {
        var userId = Guid.NewGuid();
        var accessor = CreateAccessor(
            new ClaimsPrincipal(
                new ClaimsIdentity([
                    new Claim(ClaimTypes.Name, "testuser"),
                    new Claim("identityproviderid", userId.ToString()),
                ])
            )
        );
        var processor = Substitute.For<IQueryProcessor>();
        processor
            .RunQueryAsync(Arg.Any<UserGetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new User { Username = "testuser" }));

        var provider = new UserContextProvider(accessor, processor);

        Assert.Equal("testuser", provider.User.Username);
        Assert.Equal(userId.ToString(), provider.User.IdentityProviderId);
    }

    private static IHttpContextAccessor CreateAccessor(ClaimsPrincipal user)
    {
        var context = new DefaultHttpContext { User = user };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return accessor;
    }
}
