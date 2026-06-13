namespace OpenGitBase.Cqrs.Tests.Extensions;

public class GetExtTests
{
    [Fact]
    public void Get_WhenSome_ReturnsValue()
    {
        Assert.Equal("value", Option.From("value").Get());
    }

    [Fact]
    public void Get_WhenNone_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => Option<string>.None.Get());
    }

    [Fact]
    public void GetOrElse_WithFallbackValue_ReturnsValueOrFallback()
    {
        Assert.Equal("actual", Option.From("actual").GetOrElse("fallback"));
        Assert.Equal("fallback", Option<string>.None.GetOrElse("fallback"));
    }

    [Fact]
    public void GetOrRequiredElse_ReturnsValueOrFallback()
    {
        Assert.Equal(1, Option.From(1).GetOrRequiredElse(2));
        Assert.Equal(2, Option<int>.None.GetOrRequiredElse(2));
    }

    [Fact]
    public void GetOrElse_WithFunc_ReturnsValueOrFuncResult()
    {
        Assert.Equal(3, Option.From(3).GetOrElse(() => 9));
        Assert.Equal(9, Option<int>.None.GetOrElse(() => 9));
    }

    [Fact]
    public void GetOrElse_WithNullFunc_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option<int>.None.GetOrElse((Func<int>)null!));
    }
}
