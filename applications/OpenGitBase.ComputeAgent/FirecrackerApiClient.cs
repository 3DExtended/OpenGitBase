using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace OpenGitBase.ComputeAgent;

public sealed class FirecrackerApiClient : IDisposable
{
    private readonly HttpClient _client;
    public FirecrackerApiClient(string socketPath)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket
                    .ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };
        _client = new HttpClient(handler, disposeHandler: true) { BaseAddress = new Uri("http://localhost/") };
    }

    public async Task PutAsync(string path, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await _client.PutAsync(path, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Firecracker API PUT {path} failed ({(int)response.StatusCode}): {body}"
            );
        }
    }

    public Task StartInstanceAsync(CancellationToken cancellationToken) =>
        PutAsync(
            "actions",
            new { action_type = "InstanceStart" },
            cancellationToken
        );

    public void Dispose() => _client.Dispose();
}
