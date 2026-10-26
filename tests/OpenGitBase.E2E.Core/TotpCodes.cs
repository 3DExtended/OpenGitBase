using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OpenGitBase.E2E.Core;

/// <summary>
/// What an authenticator app shows (RFC 6238: HMAC-SHA1, 6 digits, 30-second steps), so a scenario
/// can sign in with two-factor authentication without depending on the API's own implementation.
/// </summary>
public static class TotpCodes
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / 30;

    public static string ForStep(string base32Secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(DecodeBase32(base32Secret), counter);
        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7FFFFFFF) % 1_000_000;
        return binary.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string input)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in input.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | Base32Alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }

        return [.. bytes];
    }
}
