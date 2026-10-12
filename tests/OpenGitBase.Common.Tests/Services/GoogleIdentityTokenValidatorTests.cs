using Google.Apis.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class GoogleIdentityTokenValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenNoClientIdIsConfigured_RejectsWithoutContactingGoogle()
    {
        var validator = new GoogleIdentityTokenValidator(new GoogleAuthOptions());

        await Assert.ThrowsAsync<InvalidJwtException>(() =>
            validator.ValidateAsync("any.google.token")
        );
    }

    [Fact]
    public void CreateValidationSettings_RestrictsAudienceToConfiguredClientIds()
    {
        var validator = new GoogleIdentityTokenValidator(
            new GoogleAuthOptions
            {
                ClientId = "web.apps.googleusercontent.com",
                ClientIds = ["ios.apps.googleusercontent.com", " web.apps.googleusercontent.com ", string.Empty],
            }
        );

        var settings = validator.CreateValidationSettings();

        Assert.NotNull(settings.Audience);
        Assert.Equal(
            ["ios.apps.googleusercontent.com", "web.apps.googleusercontent.com"],
            settings.Audience.Order(StringComparer.Ordinal)
        );
    }
}
