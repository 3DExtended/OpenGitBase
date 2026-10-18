using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenGitBase.Features.AccountSecurity.Totp;

/// <summary>
/// RFC 6238 time-based one-time passwords with the only parameters authenticator apps agree on:
/// HMAC-SHA1, 6 digits, 30-second steps. Google Authenticator ignores other values and Microsoft
/// Authenticator only works reliably with these, so they are deliberately not configurable.
/// </summary>
public static class TotpAlgorithm
{
    private const int Digits = 6;
    private const int StepSeconds = 30;
    private const int Modulus = 1_000_000;
    private const int SecretBytes = 20;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>A fresh 160-bit secret as unpadded RFC 4648 Base32, the form authenticator apps read.</summary>
    public static string GenerateSecret() => EncodeBase32(RandomNumberGenerator.GetBytes(SecretBytes));

    public static byte[] DecodeSecret(string base32)
    {
        var bytes = new byte[base32.Length * 5 / 8];
        var buffer = 0;
        var bitsInBuffer = 0;
        var index = 0;
        foreach (var character in base32)
        {
            var value = Base32Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (value < 0)
            {
                throw new FormatException("TOTP secrets are upper-case RFC 4648 Base32 without padding.");
            }

            buffer = (buffer << 5) | value;
            bitsInBuffer += 5;
            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                bytes[index++] = (byte)(buffer >> bitsInBuffer);
            }
        }

        return bytes;
    }

    /// <summary>
    /// The key URI an authenticator app scans. The issuer goes both before the label (older Google
    /// Authenticator versions only read that) and into the issuer parameter.
    /// </summary>
    public static string BuildOtpauthUri(string issuer, string accountName, string secret)
    {
        var escapedIssuer = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{escapedIssuer}:{Uri.EscapeDataString(accountName)}?secret={secret}&issuer={escapedIssuer}";
    }

    /// <summary>Accepts the code of the current step and of one step on either side, for clock drift.</summary>
    public static bool TryVerify(byte[] secret, string code, DateTimeOffset at, out long matchedStep)
    {
        matchedStep = 0;
        if (code.Length != Digits || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        var currentStep = at.ToUnixTimeSeconds() / StepSeconds;
        var accepted = false;
        for (var step = currentStep - 1; step <= currentStep + 1; step++)
        {
            // Check every step so the time taken does not reveal which one matched.
            if (FixedTimeEquals(ComputeCode(secret, step), code) && !accepted)
            {
                matchedStep = step;
                accepted = true;
            }
        }

        return accepted;
    }

    /// <summary>The code an authenticator app shows during the given 30-second step.</summary>
    public static string ComputeCode(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, counter, hash);

        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & 0x7FFFFFFF) % Modulus;
        return binary.ToString("D6", CultureInfo.InvariantCulture);
    }

    private static string EncodeBase32(byte[] data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsInBuffer = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                builder.Append(Base32Alphabet[(buffer >> bitsInBuffer) & 0x1F]);
            }
        }

        if (bitsInBuffer > 0)
        {
            builder.Append(Base32Alphabet[(buffer << (5 - bitsInBuffer)) & 0x1F]);
        }

        return builder.ToString();
    }

    private static bool FixedTimeEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(actual)
        );
}
