namespace OpenGitBase.Cqrs.Tests.Extensions;

public class OrElseExtTests
{
    [Fact]
    public void OrElse_WithValue_ReturnsOriginalOrFallback()
    {
        Assert.Equal("keep", Option.From("keep").OrElse("fallback"));
        Assert.Equal("fallback", Option<string>.None.OrElse("fallback"));
    }

    [Fact]
    public void OrElse_WithFunc_ReturnsOriginalOrFuncResult()
    {
        Assert.Equal(4, Option.From(4).OrElse(() => 8));
        Assert.Equal(8, Option<int>.None.OrElse(() => 8));
    }

    [Fact]
    public void OrElse_WithNullFunc_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option<int>.None.OrElse((Func<int>)null!));
    }

    [Fact]
    public void OrElseWith_WithOption_ReturnsOriginalOrFallbackOption()
    {
        Assert.Equal("keep", Option.From("keep").OrElseWith(Option.From("fallback")).Get());
        Assert.Equal("fallback", Option<string>.None.OrElseWith(Option.From("fallback")).Get());
    }

    [Fact]
    public void OrElseWith_WithFunc_ReturnsOriginalOrFuncOption()
    {
        Assert.Equal(1, Option.From(1).OrElseWith(() => Option.From(2)).Get());
        Assert.Equal(2, Option<int>.None.OrElseWith(() => Option.From(2)).Get());
    }

    [Fact]
    public void OrElseWith_WithNullFunc_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Option<int>.None.OrElseWith((Func<Option<int>>)null!)
        );
    }
}
