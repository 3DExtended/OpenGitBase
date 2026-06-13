namespace OpenGitBase.Cqrs.Tests.Extensions;

public class SwitchDoTests
{
    [Fact]
    public void Switch_WhenFirstIsSome_ReturnsFirst()
    {
        var result = Option.From("first").Switch(Option.From("second"));

        Assert.Equal("first", result.Get());
    }

    [Fact]
    public void Switch_WithParams_WhenFirstIsNone_ReturnsFirstMatchingOption()
    {
        var result = Option<string>.None.Switch(Option<string>.None, Option.From("picked"));

        Assert.Equal("picked", result.Get());
    }

    [Fact]
    public void Switch_WithEnumerable_WhenNoMatch_ReturnsNone()
    {
        var result = Option<string>.None.Switch(
            new[] { Option<string>.None, Option<string>.None }
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Switch_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option<int>.None.Switch((IEnumerable<Option<int>>)null!));
    }

    [Fact]
    public void Do_WhenSome_ExecutesSideEffectAndReturnsOriginal()
    {
        var captured = string.Empty;
        var option = Option.From("value");

        var result = option.Do(value => captured = value);

        Assert.Equal(option, result);
        Assert.Equal("value", captured);
    }

    [Fact]
    public void Do_WhenNone_ReturnsOriginalWithoutExecuting()
    {
        var executed = false;
        var option = Option<string>.None;

        var result = option.Do(_ => executed = true);

        Assert.Equal(option, result);
        Assert.False(executed);
    }

    [Fact]
    public void Do_NullSideEffect_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option.From(1).Do(null!));
    }
}
