using System.Net;
using System.Net.Http.Headers;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Request content that decrypts an encrypted LFS copy on the fly with <see cref="LfsObjectCipher"/>, to restore
/// the plaintext object on a node. <see cref="LfsObjectCipher.DecryptAsync"/> authenticates every segment and checks
/// the oid before it writes the final one, so a copy that fails either check never becomes a complete upload, and
/// the storage node discards it. Owns the source stream.
/// </summary>
public sealed class LfsDecryptingContent : HttpContent
{
    private readonly Stream _ciphertext;
    private readonly long _plaintextSize;
    private readonly LfsCipherContext _cipher;

    public LfsDecryptingContent(Stream ciphertext, long plaintextSize, LfsCipherContext cipher)
    {
        _ciphertext = ciphertext;
        _plaintextSize = plaintextSize;
        _cipher = cipher;
        Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken
    )
    {
        var size = await LfsObjectCipher.DecryptAsync(_ciphertext, stream, _cipher, cancellationToken).ConfigureAwait(false);
        if (size != _plaintextSize)
        {
            throw new LfsCipherException($"The encrypted LFS object holds {size} bytes, not the recorded {_plaintextSize}.");
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _plaintextSize;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ciphertext.Dispose();
        }

        base.Dispose(disposing);
    }
}
