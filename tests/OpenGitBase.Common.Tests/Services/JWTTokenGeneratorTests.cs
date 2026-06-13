using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class JWTTokenGeneratorTests
{
    [Fact]
    public void GetJWTToken_WhenKeyMissing_Throws()
    {
        var generator = new JWTTokenGenerator(
            new SystemClock(),
            new JwtOptions
            {
                Key = null!,
                Issuer = "api",
                Audience = "api",
            }
        );

        Assert.Throws<InvalidOperationException>(() =>
            generator.GetJWTToken("user", Guid.NewGuid().ToString())
        );
    }

    [Fact]
    public void GetJWTToken_ReturnsSignedToken()
    {
        var now = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);
        var generator = new JWTTokenGenerator(
            new FixedSystemClock(now),
            new JwtOptions
            {
                Key = string.Join(string.Empty, Enumerable.Repeat("test-key", 32)),
                Issuer = "issuer",
                Audience = "audience",
                NumberOfSecondsToExpire = 3600,
            }
        );

        var token = generator.GetJWTToken("testuser", "user-id-123");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    private sealed class FixedSystemClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
