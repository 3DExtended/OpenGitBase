using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace OpenGitBase.Api.Services;

/// <summary>
/// The storage node's artifact upload body, <c>{"manifest":…,"bundleBase64":"…"}</c>, written in chunks with a
/// Content-Length. System.Text.Json refuses string values above ~166 million characters, so a bundle above ~83 MB
/// could not be serialised as one value. Despite its name the field carries lowercase hex; the storage node decodes
/// it with <c>bytes.fromhex</c>.
/// </summary>
internal sealed class ReplicationArtifactUploadContent : HttpContent
{
    private const int ChunkBytes = 64 * 1024;

    private static readonly byte[] Suffix = Encoding.ASCII.GetBytes("\"}");

    private readonly byte[] _prefix;
    private readonly byte[] _bundle;

    public ReplicationArtifactUploadContent(string manifestJson, byte[] bundle)
    {
        _prefix = Encoding.UTF8.GetBytes($"{{\"manifest\":{manifestJson},\"bundleBase64\":\"");
        _bundle = bundle;
        Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken
    )
    {
        await stream.WriteAsync(_prefix, cancellationToken).ConfigureAwait(false);

        var chars = new char[ChunkBytes * 2];
        var hex = new byte[ChunkBytes * 2];
        for (var offset = 0; offset < _bundle.Length; offset += ChunkBytes)
        {
            var written = WriteHexLower(_bundle, offset, chars, hex);
            await stream.WriteAsync(hex.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
        }

        await stream.WriteAsync(Suffix, cancellationToken).ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _prefix.Length + (2L * _bundle.Length) + Suffix.Length;
        return true;
    }

    private static int WriteHexLower(byte[] bundle, int offset, char[] chars, byte[] hex)
    {
        var chunk = bundle.AsSpan(offset, Math.Min(ChunkBytes, bundle.Length - offset));
        Convert.TryToHexStringLower(chunk, chars, out var charsWritten);
        return Encoding.ASCII.GetBytes(chars.AsSpan(0, charsWritten), hex);
    }
}
