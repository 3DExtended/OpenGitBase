using System.Net;
using System.Net.Http.Headers;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Request content that encrypts a plaintext LFS stream on the fly with <see cref="LfsObjectCipher"/>. Its length
/// is known up front, so the upload carries a Content-Length and nothing is buffered. Owns the source stream.
/// </summary>
public sealed class LfsEncryptingContent : HttpContent
{
    private readonly Stream _plaintext;
    private readonly long _plaintextSize;
    private readonly LfsCipherContext _cipher;
    private readonly int _segmentBytes;

    public LfsEncryptingContent(Stream plaintext, long plaintextSize, LfsCipherContext cipher, int segmentBytes)
    {
        _plaintext = plaintext;
        _plaintextSize = plaintextSize;
        _cipher = cipher;
        _segmentBytes = segmentBytes;
        Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken
    ) => LfsObjectCipher.EncryptAsync(_plaintext, _plaintextSize, stream, _cipher, _segmentBytes, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        length = LfsObjectCipher.EncryptedLength(_plaintextSize, _segmentBytes);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _plaintext.Dispose();
        }

        base.Dispose(disposing);
    }
}
