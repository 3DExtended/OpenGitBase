using System.Net;
using System.Text;

namespace OpenGitBase.Cli.Tests.TestSupport;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public IList<HttpRequestMessage> Requests { get; } = [];

    public void EnqueueResponse(HttpStatusCode statusCode, string body, string contentType = "application/json")
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        });
    }

    public void EnqueueResponse(Func<HttpRequestMessage, HttpResponseMessage> factory) =>
        _responses.Enqueue(factory);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No stubbed HTTP response configured.");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}
