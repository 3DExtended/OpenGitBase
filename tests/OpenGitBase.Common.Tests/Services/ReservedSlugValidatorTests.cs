using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class ReservedSlugValidatorTests
{
    public static TheoryData<string> ReservedSlugs =>
    [
        "__visual__",
        "admin",
        "api",
        "explore",
        "forgot-password",
        "gate",
        "health",
        "invite",
        "opengitbase",
        "orgs",
        "pitch",
        "register",
        "repos",
        "reset-password",
        "settings",
        "sign-in",
        "sign-out",
        "sign-up",
        "status",
        "swagger",
        "verify-email",
        "EXPLORE",
    ];

    [Theory]
    [MemberData(nameof(ReservedSlugs))]
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
