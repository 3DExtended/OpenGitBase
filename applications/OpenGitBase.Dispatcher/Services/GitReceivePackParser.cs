using System.Globalization;
using System.Text;
using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

public static class GitReceivePackParser
{
    public static bool TryParseRefUpdates(ReadOnlySpan<byte> data, out IReadOnlyList<GitRefUpdate> updates)
    {
        var parsed = new List<GitRefUpdate>();
        var offset = 0;

        while (offset + 4 <= data.Length)
        {
            var lengthHex = Encoding.ASCII.GetString(data.Slice(offset, 4));
            if (lengthHex == "0000")
            {
                break;
            }

            if (!int.TryParse(lengthHex, NumberStyles.HexNumber, null, out var length) || length < 4)
            {
                updates = parsed;
                return false;
            }

            if (offset + length > data.Length)
            {
                updates = parsed;
                return false;
            }

            var payload = data.Slice(offset + 4, length - 4);
            offset += length;

            if (TryParseRefLine(payload, out var update))
            {
                parsed.Add(update);
            }
        }

        updates = parsed;
        return true;
    }

    public static async Task<(byte[] Prefix, IReadOnlyList<GitRefUpdate> RefUpdates)> ReadPrefixAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var buffer = new MemoryStream();
        var header = new byte[4];

        while (true)
        {
            var read = await stream
                .ReadAsync(header.AsMemory(0, 4), cancellationToken)
                .ConfigureAwait(false);
            if (read < 4)
            {
                break;
            }

            await buffer.WriteAsync(header.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
            var lengthHex = Encoding.ASCII.GetString(header);
            if (lengthHex == "0000")
            {
                break;
            }

            if (!int.TryParse(lengthHex, NumberStyles.HexNumber, null, out var length) || length < 4)
            {
                break;
            }

            var payloadLength = length - 4;
            var payload = new byte[payloadLength];
            var payloadRead = 0;
            while (payloadRead < payloadLength)
            {
                read = await stream
                    .ReadAsync(payload.AsMemory(payloadRead, payloadLength - payloadRead), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                payloadRead += read;
            }

            await buffer.WriteAsync(payload.AsMemory(0, payloadRead), cancellationToken)
                .ConfigureAwait(false);
        }

        var prefix = buffer.ToArray();
        TryParseRefUpdates(prefix, out var refUpdates);
        return (prefix, refUpdates);
    }

    private static bool TryParseRefLine(ReadOnlySpan<byte> payload, out GitRefUpdate update)
    {
        update = null!;
        var line = Encoding.UTF8.GetString(payload);
        var nulIndex = line.IndexOf('\0');
        if (nulIndex >= 0)
        {
            line = line[..nulIndex];
        }

        line = line.Trim();
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        update = new GitRefUpdate
        {
            OldSha = parts[0],
            NewSha = parts[1],
            RefName = parts[2],
        };
        return true;
    }
}
