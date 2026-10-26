using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Streaming segmented AES-256-GCM for encrypted LFS copies. Runs only in the API, so the Repository Key never
/// leaves it.
/// <para>
/// Layout: a header (<c>"OLFS"</c>, format version, key version, segment size and plaintext size as big-endian
/// integers, and a random 8-byte nonce prefix), then one <c>ciphertext || tag</c> record per plaintext segment.
/// Segment <c>i</c> uses nonce <c>prefix || i</c> (big-endian u32) and associated data
/// <c>{repositoryId}:lfs:{oid}:{i}:{isFinal}</c>, so segments cannot be reordered, dropped, truncated or moved to
/// another object or repository. An empty object is one empty final segment.
/// </para>
/// <para>
/// Both directions hash the plaintext and compare it with the oid before the final segment is written, so a
/// mismatching object never yields a complete output.
/// </para>
/// </summary>
public static class LfsObjectCipher
{
    public const int TagBytes = 16;
    public const int HeaderBytes = 29;
    public const int MinSegmentBytes = 1024;
    public const int MaxSegmentBytes = 64 * 1024 * 1024;
    private const byte FormatVersion = 1;
    private const int NoncePrefixBytes = 8;
    private const int NonceBytes = 12;
    private const int KeyBytes = 32;

    private static ReadOnlySpan<byte> Magic => "OLFS"u8;

    public static long SegmentCount(long plaintextSize, int segmentBytes) =>
        plaintextSize == 0 ? 1 : ((plaintextSize - 1) / segmentBytes) + 1;

    public static long EncryptedLength(long plaintextSize, int segmentBytes) =>
        HeaderBytes + plaintextSize + (SegmentCount(plaintextSize, segmentBytes) * TagBytes);

    /// <summary>
    /// Whether <paramref name="encryptedLength"/> is the length of a copy of a <paramref name="plaintextSize"/>-byte
    /// object at some valid segment size. A cheap on-disk check for the reconciler: it catches truncated and padded
    /// copies without reading them, and still accepts copies written before <c>Lfs:SegmentBytes</c> changed.
    /// </summary>
    public static bool IsPlausibleEncryptedLength(long plaintextSize, long encryptedLength)
    {
        var overhead = encryptedLength - HeaderBytes - plaintextSize;
        if (plaintextSize < 0 || overhead <= 0 || overhead % TagBytes != 0)
        {
            return false;
        }

        var segments = overhead / TagBytes;
        return segments >= SegmentCount(plaintextSize, MaxSegmentBytes)
            && segments <= SegmentCount(plaintextSize, MinSegmentBytes);
    }

    public static async Task EncryptAsync(
        Stream plaintext,
        long plaintextSize,
        Stream destination,
        LfsCipherContext context,
        int segmentBytes,
        CancellationToken cancellationToken
    )
    {
        ValidateKey(context);
        if (segmentBytes is < MinSegmentBytes or > MaxSegmentBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentBytes), segmentBytes, "Unsupported LFS segment size.");
        }

        var segments = CheckedSegmentCount(plaintextSize, segmentBytes);
        var noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixBytes);
        await destination
            .WriteAsync(BuildHeader(context.KeyVersion, segmentBytes, plaintextSize, noncePrefix), cancellationToken)
            .ConfigureAwait(false);

        using var aes = new AesGcm(context.Key, TagBytes);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[segmentBytes];
        var record = new byte[segmentBytes + TagBytes];
        var nonce = new byte[NonceBytes];
        var remaining = plaintextSize;
        for (long index = 0; index < segments; index++)
        {
            var length = (int)Math.Min(segmentBytes, remaining);
            if (await ReadFullyAsync(plaintext, buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false) < length)
            {
                throw new LfsCipherException("The object ended before its declared size.");
            }

            remaining -= length;
            hash.AppendData(buffer, 0, length);
            var isFinal = index == segments - 1;
            if (isFinal)
            {
                await EnsureEndOfStreamAsync(plaintext, cancellationToken).ConfigureAwait(false);
                EnsureOid(hash, context.Oid);
            }

            BuildNonce(noncePrefix, index, nonce);
            aes.Encrypt(
                nonce,
                buffer.AsSpan(0, length),
                record.AsSpan(0, length),
                record.AsSpan(length, TagBytes),
                AssociatedData(context, index, isFinal)
            );
            await destination.WriteAsync(record.AsMemory(0, length + TagBytes), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Decrypts and authenticates a copy written by <see cref="EncryptAsync"/>; returns the plaintext size.</summary>
    public static async Task<long> DecryptAsync(
        Stream ciphertext,
        Stream destination,
        LfsCipherContext context,
        CancellationToken cancellationToken
    )
    {
        ValidateKey(context);
        var header = new byte[HeaderBytes];
        if (await ReadFullyAsync(ciphertext, header, cancellationToken).ConfigureAwait(false) < HeaderBytes)
        {
            throw new LfsCipherException("The encrypted LFS object is truncated.");
        }

        var (segmentBytes, plaintextSize, noncePrefix) = ParseHeader(header, context.KeyVersion);
        var segments = CheckedSegmentCount(plaintextSize, segmentBytes);
        using var aes = new AesGcm(context.Key, TagBytes);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var record = new byte[segmentBytes + TagBytes];
        var buffer = new byte[segmentBytes];
        var nonce = new byte[NonceBytes];
        var remaining = plaintextSize;
        for (long index = 0; index < segments; index++)
        {
            var length = (int)Math.Min(segmentBytes, remaining);
            var recordLength = length + TagBytes;
            if (await ReadFullyAsync(ciphertext, record.AsMemory(0, recordLength), cancellationToken).ConfigureAwait(false)
                < recordLength)
            {
                throw new LfsCipherException("The encrypted LFS object is truncated.");
            }

            var isFinal = index == segments - 1;
            BuildNonce(noncePrefix, index, nonce);
            try
            {
                aes.Decrypt(
                    nonce,
                    record.AsSpan(0, length),
                    record.AsSpan(length, TagBytes),
                    buffer.AsSpan(0, length),
                    AssociatedData(context, index, isFinal)
                );
            }
            catch (AuthenticationTagMismatchException ex)
            {
                throw new LfsCipherException($"Segment {index} of the encrypted LFS object failed authentication.", ex);
            }

            remaining -= length;
            hash.AppendData(buffer, 0, length);
            if (isFinal)
            {
                await EnsureEndOfStreamAsync(ciphertext, cancellationToken).ConfigureAwait(false);
                EnsureOid(hash, context.Oid);
            }

            await destination.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
        }

        return plaintextSize;
    }

    private static long CheckedSegmentCount(long plaintextSize, int segmentBytes)
    {
        if (plaintextSize < 0)
        {
            throw new LfsCipherException("The LFS object size is negative.");
        }

        var segments = SegmentCount(plaintextSize, segmentBytes);
        if (segments > uint.MaxValue)
        {
            throw new LfsCipherException("The LFS object has too many segments.");
        }

        return segments;
    }

    private static byte[] BuildHeader(int keyVersion, int segmentBytes, long plaintextSize, byte[] noncePrefix)
    {
        var header = new byte[HeaderBytes];
        Magic.CopyTo(header);
        header[4] = FormatVersion;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(5), keyVersion);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(9), segmentBytes);
        BinaryPrimitives.WriteInt64BigEndian(header.AsSpan(13), plaintextSize);
        noncePrefix.CopyTo(header.AsSpan(21));
        return header;
    }

    private static (int SegmentBytes, long PlaintextSize, byte[] NoncePrefix) ParseHeader(
        byte[] header,
        int expectedKeyVersion
    )
    {
        if (!header.AsSpan(0, 4).SequenceEqual(Magic) || header[4] != FormatVersion)
        {
            throw new LfsCipherException("Not an encrypted LFS object of a supported format.");
        }

        var keyVersion = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(5));
        if (keyVersion != expectedKeyVersion)
        {
            throw new LfsCipherException(
                $"The encrypted LFS object uses key version {keyVersion}, not {expectedKeyVersion}."
            );
        }

        var segmentBytes = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(9));
        if (segmentBytes is < MinSegmentBytes or > MaxSegmentBytes)
        {
            throw new LfsCipherException("The encrypted LFS object has an unsupported segment size.");
        }

        return (segmentBytes, BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(13)), header[21..HeaderBytes]);
    }

    private static void BuildNonce(byte[] prefix, long index, byte[] nonce)
    {
        prefix.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixBytes), (uint)index);
    }

    private static byte[] AssociatedData(LfsCipherContext context, long index, bool isFinal) =>
        Encoding.UTF8.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{context.RepositoryId:D}:lfs:{context.Oid}:{index}:{(isFinal ? "true" : "false")}"
            )
        );

    private static void EnsureOid(IncrementalHash hash, string oid)
    {
        if (!string.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()), oid, StringComparison.Ordinal))
        {
            throw new LfsCipherException("The LFS object content does not match its oid.");
        }
    }

    private static async Task EnsureEndOfStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        var probe = new byte[1];
        if (await stream.ReadAsync(probe, cancellationToken).ConfigureAwait(false) != 0)
        {
            throw new LfsCipherException("The LFS object is longer than its declared size.");
        }
    }

    private static ValueTask<int> ReadFullyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken) =>
        buffer.Length == 0
            ? ValueTask.FromResult(0)
            : stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

    private static void ValidateKey(LfsCipherContext context)
    {
        if (context.Key.Length != KeyBytes)
        {
            throw new ArgumentException("The Repository Key must be 32 bytes.", nameof(context));
        }
    }
}
