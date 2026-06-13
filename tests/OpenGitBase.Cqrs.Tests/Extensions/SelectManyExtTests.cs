namespace OpenGitBase.Cqrs.Tests.Extensions;

public class SelectManyExtTests
{
    [Fact]
    public void SelectMany_WhenNone_ReturnsNone()
    {
        var result = Option<int>.None.SelectMany(x => Option.From(x.ToString()));

        Assert.True(result.IsNone);
    }

    [Fact]
    public void SelectMany_WhenSomeAndSelectorReturnsSome_ReturnsMapped()
    {
        var result = Option.From(2).SelectMany(x => Option.From(x * 2));

        Assert.Equal(4, result.Get());
    }

    [Fact]
    public void SelectMany_WhenSomeAndSelectorReturnsNone_ReturnsNone()
    {
        var result = Option.From(2).SelectMany(_ => Option<int>.None);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void SelectMany_NullSelector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Option.From(1).SelectMany<int, string>(null!)
        );
    }

    [Fact]
    public void SelectMany_WithResultSelector_WhenOuterNone_ReturnsNone()
    {
        var result = Option<int>.None.SelectMany(x => Option.From("a"), (x, y) => $"{x}{y}");

        Assert.True(result.IsNone);
    }

    [Fact]
    public void SelectMany_WithResultSelector_WhenSome_ReturnsCombined()
    {
        var result = Option.From(2).SelectMany(x => Option.From("a"), (x, y) => $"{x}{y}");

        Assert.Equal("2a", result.Get());
    }

    [Fact]
    public void SelectMany_WithResultSelector_WhenInnerNone_ReturnsNone()
    {
        var result = Option.From(2).SelectMany(_ => Option<string>.None, (x, y) => $"{x}{y}");

        Assert.True(result.IsNone);
    }

    [Fact]
    public void SelectMany_WithResultSelector_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Option.From(1).SelectMany<int, string, string>(null!, (a, b) => string.Empty)
        );
        Assert.Throws<ArgumentNullException>(() =>
            Option.From(1).SelectMany(x => Option.From("a"), (Func<int, string, string>)null!)
        );
    }
}
