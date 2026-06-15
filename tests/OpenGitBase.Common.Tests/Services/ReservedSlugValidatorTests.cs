using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class ReservedSlugValidatorTests
{
    [Theory]
    [InlineData("explore")]
    [InlineData("sign-in")]
    [InlineData("sign-up")]
    [InlineData("settings")]
    [InlineData("api")]
    [InlineData("register")]
    [InlineData("health")]
    [InlineData("swagger")]
    [InlineData("__visual__")]
    [InlineData("forgot-password")]
    [InlineData("reset-password")]
    [InlineData("verify-email")]
    [InlineData("sign-out")]
    [InlineData("EXPLORE")]
    public void IsReserved_WhenSlugIsReserved_ReturnsTrue(string slug)
    {
        Assert.True(ReservedSlugValidator.IsReserved(slug));
    }

    [Theory]
    [InlineData("validuser")]
    [InlineData("my-repo")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsReserved_WhenSlugIsNotReserved_ReturnsFalse(string slug)
    {
        Assert.False(ReservedSlugValidator.IsReserved(slug));
    }
}
