using Microsoft.AspNetCore.Http;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class BasicAuthTokenReaderTests
{
    [Fact]
    public void TryReadAccessToken_ReadsPasswordFromBasicHeader()
    {
        var context = new DefaultHttpContext();
        var encoded = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("git:ogb_test_token_123")
        );
        context.Request.Headers.Authorization = $"Basic {encoded}";

        var parsed = BasicAuthTokenReader.TryReadAccessToken(context.Request, out var token);

        Assert.True(parsed);
        Assert.Equal("ogb_test_token_123", token);
    }

    [Fact]
    public void TryReadAccessToken_RejectsMissingHeader()
    {
        var context = new DefaultHttpContext();

        var parsed = BasicAuthTokenReader.TryReadAccessToken(context.Request, out _);

        Assert.False(parsed);
    }
}
