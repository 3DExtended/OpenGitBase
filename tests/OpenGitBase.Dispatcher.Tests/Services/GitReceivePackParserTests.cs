using System.Text;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class GitReceivePackParserTests
{
    [Fact]
    public void TryParseRefUpdates_ParsesPktLineRefCommands()
    {
        var payload =
            "1111111111111111111111111111111111111111 "
            + "2222222222222222222222222222222222222222 "
            + "refs/heads/main\0 report-status\n";
        var line = EncodePktLine(payload);
        var flush = Encoding.ASCII.GetBytes("0000");
        var data = line.Concat(flush).ToArray();

        var parsed = GitReceivePackParser.TryParseRefUpdates(data, out var updates);

        Assert.True(parsed);
        var update = Assert.Single(updates);
        Assert.Equal("refs/heads/main", update.RefName);
        Assert.Equal("1111111111111111111111111111111111111111", update.OldSha);
        Assert.Equal("2222222222222222222222222222222222222222", update.NewSha);
    }

    [Fact]
    public async Task ReadPrefixAsync_ReadsThroughFlushPacket()
    {
        var payload =
            "0000000000000000000000000000000000000000 "
            + "3333333333333333333333333333333333333333 "
            + "refs/heads/feature\n";
        var prefixBytes = EncodePktLine(payload).Concat(Encoding.ASCII.GetBytes("0000")).ToArray();
        await using var stream = new MemoryStream(prefixBytes);
        await using var remainder = new MemoryStream([0x01, 0x02, 0x03]);
        await using var combined = new MemoryStream();
        await stream.CopyToAsync(combined);
        await remainder.CopyToAsync(combined);
        combined.Position = 0;

        var (prefix, updates) = await GitReceivePackParser.ReadPrefixAsync(
            combined,
            CancellationToken.None
        );

        Assert.NotEmpty(prefix);
        var update = Assert.Single(updates);
        Assert.Equal("refs/heads/feature", update.RefName);
    }

    private static byte[] EncodePktLine(string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var length = payloadBytes.Length + 4;
        var header = Encoding.ASCII.GetBytes(length.ToString("x4"));
        return header.Concat(payloadBytes).ToArray();
    }
}
