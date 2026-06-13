namespace OpenGitBase.Cqrs.Tests.Extensions;

public class JoinExtTests
{
    [Fact]
    public void Join_WhenBothSomeAndKeysMatch_ReturnsJoinedValue()
    {
        var left = Option.From(new { Id = 1, Name = "left" });
        var right = Option.From(new { Id = 1, Name = "right" });

        var result = left.Join(
            right,
            l => l.Id,
            r => r.Id,
            (l, r) => Option.From($"{l.Name}-{r.Name}")
        );

        Assert.Equal("left-right", result.Get());
    }

    [Fact]
    public void Join_WhenFirstNone_ReturnsNone()
    {
        var result = Option<int>.None.Join(
            Option.From(1),
            x => x,
            x => x,
            (a, b) => Option.From(a + b)
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Join_WhenSecondNone_ReturnsNone()
    {
        var result = Option
            .From(1)
            .Join(Option<int>.None, x => x, x => x, (a, b) => Option.From(a + b));

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Join_WhenFirstKeyIsNull_ReturnsNone()
    {
        var result = Option
            .From("a")
            .Join(Option.From("b"), _ => (string?)null, x => x, (a, b) => Option.From($"{a}{b}"));

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Join_WhenSecondKeyIsNull_ReturnsNone()
    {
        var result = Option
            .From("a")
            .Join(Option.From("b"), x => x, _ => (string?)null, (a, b) => Option.From($"{a}{b}"));

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Join_WhenKeysDoNotMatch_ReturnsNone()
    {
        var result = Option
            .From(1)
            .Join(Option.From(2), x => x, x => x, (a, b) => Option.From(a + b));

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Join_WithCustomComparer_UsesComparer()
    {
        var result = Option
            .From("Ab")
            .Join(
                Option.From("ab"),
                x => x,
                x => x,
                (a, b) => Option.From($"{a}{b}"),
                StringComparer.OrdinalIgnoreCase
            );

        Assert.Equal("Abab", result.Get());
    }

    [Fact]
    public void Join_NullArguments_Throw()
    {
        var left = Option.From(1);
        var right = Option.From(2);

        Assert.Throws<ArgumentNullException>(() =>
            left.Join(right, null!, x => x, (a, b) => Option.From(a))
        );
        Assert.Throws<ArgumentNullException>(() =>
            left.Join(right, x => x, null!, (a, b) => Option.From(a))
        );
        Assert.Throws<ArgumentNullException>(() =>
            left.Join(right, x => x, x => x, (Func<int, int, Option<int>>)null!)
        );
    }
}
