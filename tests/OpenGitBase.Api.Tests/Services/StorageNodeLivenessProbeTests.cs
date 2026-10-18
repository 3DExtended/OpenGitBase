using System.Net;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class StorageNodeLivenessProbeTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AnswersAsync_WhenTheNodeRespondsWithAnyStatus_ReturnsTrue(HttpStatusCode status)
    {
        HttpRequestMessage? sent = null;
        var probe = CreateProbe((request, _) =>
        {
            sent = request;
            return Task.FromResult(new HttpResponseMessage(status));
        });

        var answers = await probe.AnswersAsync("storage-1", 8081, CancellationToken.None);

        Assert.True(answers);
        Assert.NotNull(sent);
        Assert.Equal("storage-1", sent.RequestUri?.Host);
        Assert.Equal(8081, sent.RequestUri?.Port);
        Assert.Null(sent.Headers.Authorization);
    }

    [Fact]
    public async Task AnswersAsync_WhenTheConnectionFails_ReturnsFalse()
    {
        var probe = CreateProbe((_, _) => throw new HttpRequestException("Connection refused"));

        Assert.False(await probe.AnswersAsync("storage-1", 8081, CancellationToken.None));
    }

    [Fact]
    public async Task AnswersAsync_WhenTheNodeDoesNotAnswerInTime_ReturnsFalse()
    {
        var probe = CreateProbe(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
            TimeSpan.FromMilliseconds(50)
        );

        Assert.False(await probe.AnswersAsync("storage-1", 8081, CancellationToken.None));
    }

    private static StorageNodeLivenessProbe CreateProbe(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
        TimeSpan? timeout = null
    ) => new(new HttpClient(new StubHandler(respond)), timeout ?? StorageNodeLivenessProbe.DefaultTimeout);

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => respond(request, cancellationToken);
    }
}
