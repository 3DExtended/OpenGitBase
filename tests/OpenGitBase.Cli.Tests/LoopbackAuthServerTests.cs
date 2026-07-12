using OpenGitBase.Cli.Auth;

namespace OpenGitBase.Cli.Tests;

public sealed class LoopbackAuthServerTests
{
    [Fact]
    public void Valid_callback_state_is_accepted()
    {
        Assert.True(LoopbackAuthHelpers.IsValidCallback("expected-state", "expected-state", "jwt-token"));
    }

    [Fact]
    public void Invalid_callback_state_is_rejected()
    {
        Assert.False(LoopbackAuthHelpers.IsValidCallback("expected-state", "wrong-state", "jwt-token"));
        Assert.False(LoopbackAuthHelpers.IsValidCallback("expected-state", "expected-state", null));
    }

    [Fact]
    public void ParseQueryString_reads_token_and_state()
    {
        var query = LoopbackAuthHelpers.ParseQueryString("?token=abc123&state=xyz");
        Assert.Equal("abc123", query["token"]);
        Assert.Equal("xyz", query["state"]);
    }
}
