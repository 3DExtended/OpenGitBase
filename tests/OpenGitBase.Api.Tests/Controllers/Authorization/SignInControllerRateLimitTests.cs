using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Controllers.Authorization;

namespace OpenGitBase.Api.Tests.Controllers.Authorization;

// The rate limiter is off in the E2ETest host, so the policy binding is pinned here instead.
public class SignInControllerRateLimitTests
{
    [Theory]
    [InlineData(nameof(SignInController.LoginAsync))]
    [InlineData(nameof(SignInController.RequestPasswordResetAsync))]
    [InlineData(nameof(SignInController.UserPasswordResetAsync))]
    public void CredentialEndpoints_UseTheSensitiveRateLimitPolicy(string actionName)
    {
        var action = typeof(SignInController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);

        var attribute = action?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("sensitive", attribute.PolicyName);
    }
}
