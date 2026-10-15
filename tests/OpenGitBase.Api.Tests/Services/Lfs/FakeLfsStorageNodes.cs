using System.Collections.Concurrent;
using System.Net;
using OpenGitBase.Api.Services;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Services.Lfs;

/// <summary>
/// Storage nodes' internal LFS routes in memory, keyed by host, for tests that drive the real
/// <see cref="StorageLfsClient"/>. Records every request, and lets single hosts fail or hang until cancelled.
/// </summary>
public sealed class FakeLfsStorageNodes : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<RecordedStorageRequest> _requests = new();

    public ISet<string> FailingHosts { get; } = new HashSet<string>(StringComparer.Ordinal);

    public ISet<string> HangingHosts { get; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyCollection<RecordedStorageRequest> Requests => _requests;

    /// <summary>The copy kind each host's inventory reports (its role); hosts not listed hold plaintext.</summary>
    public IDictionary<string, LfsObjectCopyKind> Kinds { get; } = new Dictionary<string, LfsObjectCopyKind>(StringComparer.Ordinal);

    public static StorageNodeEndpoint Endpoint(string host) => new(NodeId(host), host, 8081, $"token-{host}");

    /// <summary>A stable node id per host, so tests can name nodes by host.</summary>
    public static Guid NodeId(string host) =>
        new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(host)).AsSpan(0, 16));

    public StorageLfsClient CreateClient() => new(new HttpClient(this, disposeHandler: false));

    public void Store(string host, Guid repositoryId, string oid, LfsObjectCopyKind kind, byte[] content) =>
        _objects[Key(host, repositoryId, oid, kind)] = content;

    public byte[]? Find(string host, Guid repositoryId, string oid, LfsObjectCopyKind kind) =>
        _objects.TryGetValue(Key(host, repositoryId, oid, kind), out var content) ? content : null;

    public void Remove(string host, Guid repositoryId, string oid, LfsObjectCopyKind kind) =>
        _objects.TryRemove(Key(host, repositoryId, oid, kind), out _);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var uri = request.RequestUri!;
        var body = request.Content is null
            ? []
            : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        _requests.Enqueue(new RecordedStorageRequest(request.Method, uri, request.Headers.ToString(), body));

        if (HangingHosts.Contains(uri.Host))
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (FailingHosts.Contains(uri.Host))
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        var key = uri.Host + uri.AbsolutePath;
        if (request.Method == HttpMethod.Get && uri.AbsolutePath.EndsWith("/inventory", StringComparison.Ordinal))
        {
            return Inventory(uri);
        }

        if (request.Method == HttpMethod.Put)
        {
            if (request.Content?.Headers.ContentLength != body.Length)
            {
                return new HttpResponseMessage(HttpStatusCode.LengthRequired);
            }

            _objects[key] = body;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        if (!_objects.TryGetValue(key, out var stored))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Head)
        {
            var head = new ByteArrayContent([]);
            head.Headers.ContentLength = stored.Length;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = head };
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(stored) };
    }

    private static string Key(string host, Guid repositoryId, string oid, LfsObjectCopyKind kind) =>
        $"{host}/internal/lfs/{repositoryId:D}/{(kind == LfsObjectCopyKind.Encrypted ? "encrypted" : "objects")}/{oid}";

    private HttpResponseMessage Inventory(Uri uri)
    {
        var kind = Kinds.TryGetValue(uri.Host, out var held) ? held : LfsObjectCopyKind.Plaintext;
        var repository = uri.AbsolutePath.Split('/')[3];
        var prefix = $"{uri.Host}/internal/lfs/{repository}/{(kind == LfsObjectCopyKind.Encrypted ? "encrypted" : "objects")}/";
        var objects = _objects
            .Where(entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(entry => new { oid = entry.Key[prefix.Length..], size = entry.Value.Length })
            .OrderBy(item => item.oid, StringComparer.Ordinal);
        var body = System.Text.Json.JsonSerializer.Serialize(
            new { kind = kind == LfsObjectCopyKind.Encrypted ? "encrypted" : "plaintext", objects }
        );
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
