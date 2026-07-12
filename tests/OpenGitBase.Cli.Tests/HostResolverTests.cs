using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli.Tests;

public sealed class HostResolverTests
{
    private readonly HostResolver _resolver = new();

    [Fact]
    public void Default_host_is_production()
    {
        Assert.Equal(HostDefaults.ProductionHost, _resolver.DefaultHost);
    }

    [Theory]
    [InlineData("localhost:8089", "https://localhost:8089")]
    [InlineData("https://www.opengitbase.com/", "https://www.opengitbase.com")]
    [InlineData("http://localhost:8089", "http://localhost:8089")]
    public void NormalizeHost_adds_scheme_and_trims_slash(string input, string expected)
    {
        Assert.Equal(expected, _resolver.NormalizeHost(input));
    }

    [Fact]
    public void ResolveHost_prefers_override_over_configured()
    {
        var resolved = _resolver.ResolveHost("localhost:8089", HostDefaults.ProductionHost);
        Assert.Equal("https://localhost:8089", resolved);
    }

    [Fact]
    public void ResolveHost_uses_configured_when_no_override()
    {
        var resolved = _resolver.ResolveHost(null, "https://staging.example.com");
        Assert.Equal("https://staging.example.com", resolved);
    }

    [Fact]
    public void GetApiBaseUrl_appends_api_path()
    {
        Assert.Equal(
            "https://www.opengitbase.com/api",
            _resolver.GetApiBaseUrl(HostDefaults.ProductionHost));
    }
}
