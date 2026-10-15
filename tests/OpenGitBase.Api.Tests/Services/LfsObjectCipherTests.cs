using System.Security.Cryptography;
using System.Text;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class LfsObjectCipherTests
{
    private const int SegmentBytes = 1024;
    private const int KeyVersion = 1;
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    private static readonly Guid RepositoryId = Guid.NewGuid();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(SegmentBytes - 1)]
    [InlineData(SegmentBytes)]
    [InlineData(SegmentBytes + 1)]
    [InlineData((3 * SegmentBytes) + 5)]
    public async Task EncryptThenDecrypt_ReturnsTheOriginalBytes(int size)
    {
        var plaintext = RandomNumberGenerator.GetBytes(size);

        var ciphertext = await EncryptAsync(plaintext);
        var decrypted = await DecryptAsync(ciphertext, Context(plaintext));

        Assert.Equal(plaintext, decrypted);
        Assert.Equal(LfsObjectCipher.EncryptedLength(size, SegmentBytes), ciphertext.Length);
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(5000, 1024)]
    [InlineData(5000, 4096)]
    [InlineData(200_000_000, 1024 * 1024)]
    [InlineData(200_000_000, LfsObjectCipher.MaxSegmentBytes)]
    public void IsPlausibleEncryptedLength_AcceptsTheLengthOfACopyAtAnyValidSegmentSize(long size, int segmentBytes)
    {
        Assert.True(LfsObjectCipher.IsPlausibleEncryptedLength(size, LfsObjectCipher.EncryptedLength(size, segmentBytes)));
    }

    [Theory]
    [InlineData(5000, 0)]
    [InlineData(5000, 100)]
    [InlineData(5000, 5000)]
    [InlineData(5000, 5029)]
    [InlineData(5000, 5046)]
    [InlineData(5000, 5000 + LfsObjectCipher.HeaderBytes + (6 * LfsObjectCipher.TagBytes))]
    [InlineData(5000, 5000 + LfsObjectCipher.HeaderBytes + (5 * LfsObjectCipher.TagBytes) + 1)]
    public void IsPlausibleEncryptedLength_RejectsTruncatedPaddedOrImpossibleLengths(long size, long length)
    {
        Assert.False(LfsObjectCipher.IsPlausibleEncryptedLength(size, length));
    }

    [Fact]
    public async Task Encrypt_DoesNotLeakThePlaintext()
    {
        var plaintext = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("PLAINTEXT-MARKER-", 200)));

        var ciphertext = await EncryptAsync(plaintext);

        Assert.Equal(-1, ciphertext.AsSpan().IndexOf("PLAINTEXT-MARKER"u8));
    }

    [Fact]
    public async Task Decrypt_RejectsATamperedSegment()
    {
        var plaintext = RandomNumberGenerator.GetBytes((2 * SegmentBytes) + 7);
        var ciphertext = await EncryptAsync(plaintext);
        ciphertext[LfsObjectCipher.HeaderBytes + SegmentBytes + LfsObjectCipher.TagBytes + 3] ^= 0x01;

        await Assert.ThrowsAsync<LfsCipherException>(() => DecryptAsync(ciphertext, Context(plaintext)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(LfsObjectCipher.TagBytes + 1)]
    public async Task Decrypt_RejectsATruncatedObject(int bytesCut)
    {
        var plaintext = RandomNumberGenerator.GetBytes((2 * SegmentBytes) + 7);
        var ciphertext = await EncryptAsync(plaintext);

        await Assert.ThrowsAsync<LfsCipherException>(() =>
            DecryptAsync(ciphertext[..^bytesCut], Context(plaintext))
        );
    }

    [Fact]
    public async Task Decrypt_RejectsAnObjectMissingItsFinalSegment()
    {
        var plaintext = RandomNumberGenerator.GetBytes(2 * SegmentBytes);
        var ciphertext = await EncryptAsync(plaintext);
        var firstSegmentOnly = ciphertext[..(LfsObjectCipher.HeaderBytes + SegmentBytes + LfsObjectCipher.TagBytes)];

        await Assert.ThrowsAsync<LfsCipherException>(() => DecryptAsync(firstSegmentOnly, Context(plaintext)));
    }

    [Fact]
    public async Task Decrypt_RejectsReorderedSegments()
    {
        var plaintext = RandomNumberGenerator.GetBytes((2 * SegmentBytes) + 7);
        var ciphertext = await EncryptAsync(plaintext);
        var segment = SegmentBytes + LfsObjectCipher.TagBytes;
        var first = ciphertext.AsSpan(LfsObjectCipher.HeaderBytes, segment).ToArray();
        var second = ciphertext.AsSpan(LfsObjectCipher.HeaderBytes + segment, segment).ToArray();
        second.CopyTo(ciphertext, LfsObjectCipher.HeaderBytes);
        first.CopyTo(ciphertext, LfsObjectCipher.HeaderBytes + segment);

        await Assert.ThrowsAsync<LfsCipherException>(() => DecryptAsync(ciphertext, Context(plaintext)));
    }

    [Fact]
    public async Task Decrypt_RejectsASegmentFromAnotherObject()
    {
        var plaintext = RandomNumberGenerator.GetBytes((2 * SegmentBytes) + 7);
        var other = (byte[])plaintext.Clone();
        other[^1] ^= 0xFF;
        var ciphertext = await EncryptAsync(plaintext);
        var otherCiphertext = await EncryptAsync(other);
        var segment = SegmentBytes + LfsObjectCipher.TagBytes;
        otherCiphertext.AsSpan(LfsObjectCipher.HeaderBytes, segment).CopyTo(ciphertext.AsSpan(LfsObjectCipher.HeaderBytes));

        await Assert.ThrowsAsync<LfsCipherException>(() => DecryptAsync(ciphertext, Context(plaintext)));
    }

    [Fact]
    public async Task Decrypt_RejectsAnObjectOfAnotherRepository()
    {
        var plaintext = RandomNumberGenerator.GetBytes(100);
        var ciphertext = await EncryptAsync(plaintext);

        await Assert.ThrowsAsync<LfsCipherException>(() =>
            DecryptAsync(ciphertext, Context(plaintext) with { RepositoryId = Guid.NewGuid() })
        );
    }

    [Fact]
    public async Task Decrypt_RejectsAWrongKeyVersion()
    {
        var plaintext = RandomNumberGenerator.GetBytes(100);
        var ciphertext = await EncryptAsync(plaintext);

        await Assert.ThrowsAsync<LfsCipherException>(() =>
            DecryptAsync(ciphertext, Context(plaintext) with { KeyVersion = KeyVersion + 1 })
        );
    }

    [Fact]
    public async Task Decrypt_RejectsAWrongKey()
    {
        var plaintext = RandomNumberGenerator.GetBytes(100);
        var ciphertext = await EncryptAsync(plaintext);

        await Assert.ThrowsAsync<LfsCipherException>(() =>
            DecryptAsync(ciphertext, Context(plaintext) with { Key = RandomNumberGenerator.GetBytes(32) })
        );
    }

    [Fact]
    public async Task Encrypt_WhenTheContentDoesNotMatchTheOid_FailsBeforeTheFinalSegment()
    {
        var plaintext = RandomNumberGenerator.GetBytes((2 * SegmentBytes) + 7);
        var context = Context(RandomNumberGenerator.GetBytes(10));
        using var output = new MemoryStream();

        await Assert.ThrowsAsync<LfsCipherException>(() =>
            LfsObjectCipher.EncryptAsync(new MemoryStream(plaintext), plaintext.Length, output, context, SegmentBytes, CancellationToken.None)
        );
        Assert.True(output.Length < LfsObjectCipher.EncryptedLength(plaintext.Length, SegmentBytes));
    }

    [Fact]
    public async Task Encrypt_WhenTheSourceEndsEarly_Fails()
    {
        var plaintext = RandomNumberGenerator.GetBytes(SegmentBytes + 7);
        using var output = new MemoryStream();

        await Assert.ThrowsAsync<LfsCipherException>(() =>
            LfsObjectCipher.EncryptAsync(
                new MemoryStream(plaintext[..SegmentBytes]),
                plaintext.Length,
                output,
                Context(plaintext),
                SegmentBytes,
                CancellationToken.None
            )
        );
    }

    private static LfsCipherContext Context(byte[] plaintext) =>
        new(Key, KeyVersion, RepositoryId, Convert.ToHexStringLower(SHA256.HashData(plaintext)));

    private static async Task<byte[]> EncryptAsync(byte[] plaintext)
    {
        using var output = new MemoryStream();
        await LfsObjectCipher.EncryptAsync(
            new MemoryStream(plaintext),
            plaintext.Length,
            output,
            Context(plaintext),
            SegmentBytes,
            CancellationToken.None
        );
        return output.ToArray();
    }

    private static async Task<byte[]> DecryptAsync(byte[] ciphertext, LfsCipherContext context)
    {
        using var output = new MemoryStream();
        await LfsObjectCipher.DecryptAsync(new MemoryStream(ciphertext), output, context, CancellationToken.None);
        return output.ToArray();
    }
}
