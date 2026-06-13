namespace OpenGitBase.Cqrs.Tests.Extensions;

public class ZipExtTests
{
    [Fact]
    public void Zip_WhenBothSome_ReturnsSelectedValue()
    {
        var result = Option.From(2).Zip(Option.From(3), (a, b) => a + b);

        Assert.Equal(5, result.Get());
    }

    [Fact]
    public void Zip_WhenSelectReturnsNull_ReturnsNone()
    {
        var result = Option.From("a").Zip(Option.From("b"), (_, _) => (string?)null);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Zip_WhenFirstNone_ReturnsNone()
    {
        var result = Option<int>.None.Zip(Option.From(1), (a, b) => a + b);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Zip_WhenSecondNone_ReturnsNone()
    {
        var result = Option.From(1).Zip(Option<int>.None, (a, b) => a + b);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Zip_NullSelect_ThrowsNullReferenceException()
    {
        Assert.Throws<NullReferenceException>(() =>
            Option.From(1).Zip(Option.From(2), (Func<int, int, int>)null!)
        );
    }

    [Fact]
    public void ZipWith_WhenSelectorReturnsSome_ReturnsValue()
    {
        var result = Option.From(1).ZipWith(Option.From(2), (a, b) => Option.From(a + b));

        Assert.Equal(3, result.Get());
    }

    [Fact]
    public void ZipWith_WhenSelectorReturnsNone_ReturnsNone()
    {
        var result = Option.From(1).ZipWith(Option.From(2), (_, _) => Option<int>.None);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void ZipWith_NullSelect_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Option.From(1).ZipWith(Option.From(2), (Func<int, int, Option<int>>)null!)
        );
    }
}
