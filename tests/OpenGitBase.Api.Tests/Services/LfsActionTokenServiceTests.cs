using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

namespace OpenGitBase.Api.Tests.Services;

public class LfsActionTokenServiceTests
{
    private const string Oid = "4d7a214614ab2935c943f9e0ff69d22eadbb8f32b1258daaa5e2ca24d17e2393";
    private const string OtherOid = "0000000000000000000000000000000000000000000000000000000000000001";
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _repositoryId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ISystemClock _clock = Substitute.For<ISystemClock>();

    public LfsActionTokenServiceTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    [Fact]
    public void Validate_WhenTokenMatchesOidAndOperation_ReturnsItsClaims()
    {
        var service = CreateService();
        var issued = service.Issue(_repositoryId, Oid, 5_000_000, LfsTransferOperation.Upload, _userId);

        var validation = service.Validate(issued.Token, Oid, LfsTransferOperation.Upload);

        Assert.True(validation.IsValid);
        var claims = Assert.IsType<LfsActionTokenClaims>(validation.Claims);
        Assert.Equal(_repositoryId, claims.RepositoryId);
        Assert.Equal(Oid, claims.Oid);
        Assert.Equal(5_000_000, claims.Size);
        Assert.Equal(LfsTransferOperation.Upload, claims.Operation);
        Assert.Equal(_userId, claims.UserId);
    }

    [Fact]
    public void Issue_ReportsTheConfiguredLifetime()
    {
        var service = CreateService(lifetimeSeconds: 900);

        var issued = service.Issue(_repositoryId, Oid, 1, LfsTransferOperation.Download, null);

        Assert.Equal(900, issued.ExpiresInSeconds);
        Assert.Equal(Now.AddSeconds(900), issued.ExpiresAt);
    }

    [Fact]
    public void Validate_WhenTokenWasIssuedForAnotherOid_Rejects()
    {
        var service = CreateService();
        var issued = service.Issue(_repositoryId, Oid, 1, LfsTransferOperation.Upload, _userId);

        var validation = service.Validate(issued.Token, OtherOid, LfsTransferOperation.Upload);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Validate_WhenTokenWasIssuedForAnotherOperation_Rejects()
    {
        var service = CreateService();
        var issued = service.Issue(_repositoryId, Oid, 1, LfsTransferOperation.Download, _userId);

        var validation = service.Validate(issued.Token, Oid, LfsTransferOperation.Upload);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Validate_WhenTokenExpired_RejectsWithExpiredMessage()
    {
        var service = CreateService(lifetimeSeconds: 60);
        var issued = service.Issue(_repositoryId, Oid, 1, LfsTransferOperation.Download, _userId);
        _clock.UtcNow.Returns(Now.AddSeconds(61));

        var validation = service.Validate(issued.Token, Oid, LfsTransferOperation.Download);

        Assert.False(validation.IsValid);
        Assert.Contains("expired", validation.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WhenPayloadWasTampered_Rejects()
    {
        var service = CreateService();
        var issued = service.Issue(_repositoryId, Oid, 1, LfsTransferOperation.Download, _userId);
        var other = service.Issue(Guid.NewGuid(), Oid, 1, LfsTransferOperation.Download, _userId);
        var forged = other.Token.Split('.')[0] + "." + issued.Token.Split('.')[1];

        var validation = service.Validate(forged, Oid, LfsTransferOperation.Download);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void Validate_WhenTokenWasSignedWithAnotherKey_Rejects()
    {
        var issuer = CreateService(jwtKey: new string('a', 64));
        var issued = issuer.Issue(_repositoryId, Oid, 1, LfsTransferOperation.Download, _userId);

        var validation = CreateService(jwtKey: new string('b', 64))
            .Validate(issued.Token, Oid, LfsTransferOperation.Download);

        Assert.False(validation.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    public void Validate_WhenTokenIsMalformed_Rejects(string token)
    {
        var validation = CreateService().Validate(token, Oid, LfsTransferOperation.Download);

        Assert.False(validation.IsValid);
    }

    private LfsActionTokenService CreateService(int lifetimeSeconds = 3600, string? jwtKey = null) =>
        new(
            new JwtOptions { Key = jwtKey ?? new string('k', 64) },
            new LfsOptions { ActionTokenLifetimeSeconds = lifetimeSeconds },
            _clock
        );
}
