namespace OpenGitBase.Cli.Tests;

public sealed class DiscussionBodySplitterTests
{
    [Fact]
    public void Split_ReturnsNothingForBlankBody()
    {
        Assert.Empty(DiscussionBodySplitter.Split(null));
        Assert.Empty(DiscussionBodySplitter.Split("  \n"));
    }

    [Fact]
    public void Split_KeepsBodyWithinLimitAsSinglePart()
    {
        var body = new string('a', DiscussionBodySplitter.MaxBodyLength);

        Assert.Equal([body], DiscussionBodySplitter.Split($"\n{body}\n"));
    }

    [Fact]
    public void Split_BreaksLongBodyAtParagraphsSoBlankLineJoinRestoresIt()
    {
        var body = string.Join("\n\n", Enumerable.Range(1, 60).Select(Paragraph));

        var parts = DiscussionBodySplitter.Split(body);

        Assert.True(parts.Count >= 3);
        Assert.InRange(parts[0].Length, 1, DiscussionBodySplitter.MaxBodyLength);
        Assert.All(parts.Skip(1), part => Assert.InRange(part.Length, 1, DiscussionBodySplitter.MaxCommentLength));
        Assert.Equal(body, string.Join("\n\n", parts));
    }

    [Fact]
    public void Split_AvoidsParagraphBreaksInsideCodeFences()
    {
        var intro = new string('a', 6000);
        var code = "```text\n" + new string('b', 1000) + "\n\n" + new string('c', 1500) + "\n```";
        var body = $"{intro}\n\n{code}\n\nTail";

        var parts = DiscussionBodySplitter.Split(body);

        Assert.Equal([intro, $"{code}\n\nTail"], parts);
    }

    [Fact]
    public void Split_FallsBackToLineBreaksWithoutBlankLines()
    {
        var body = string.Join("\n", Enumerable.Range(1, 400).Select(index => $"- item {index} {new string('x', 40)}"));

        var parts = DiscussionBodySplitter.Split(body);

        Assert.InRange(parts[0].Length, 1, DiscussionBodySplitter.MaxBodyLength);
        Assert.Equal(body, string.Join("\n", parts));
    }

    [Fact]
    public void Split_HardCutsTextWithoutWhitespace()
    {
        var body = new string('x', 20000);

        var parts = DiscussionBodySplitter.Split(body);

        Assert.Equal([8000, 12000], parts.Select(part => part.Length));
    }

    [Fact]
    public void Split_NeverSeparatesSurrogatePairs()
    {
        var body = "a" + string.Concat(Enumerable.Repeat("😀", 6000));

        var parts = DiscussionBodySplitter.Split(body);

        Assert.All(parts, part => Assert.False(char.IsHighSurrogate(part[^1])));
        Assert.Equal(body, string.Concat(parts));
    }

    private static string Paragraph(int index) =>
        $"## Section {index}\n\n" + string.Join(' ', Enumerable.Repeat($"word{index}", 80));
}
