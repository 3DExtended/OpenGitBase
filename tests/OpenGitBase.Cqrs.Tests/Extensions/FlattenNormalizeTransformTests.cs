using System.Reflection;

namespace OpenGitBase.Cqrs.Tests.Extensions;

public class FlattenNormalizeTransformTests
{
    [Fact]
    public void Flatten_WhenOuterNone_ReturnsNone()
    {
        Option<Option<int>> nested = Option<Option<int>>.None;

        Assert.True(nested.Flatten().IsNone);
    }

    [Fact]
    public void Flatten_WhenOuterSome_ReturnsInner()
    {
        Option<Option<int>> nested = Option.From(Option.From(7));

        Assert.Equal(7, nested.Flatten().Get());
    }

    [Fact]
    public void Normalize_WhenNone_ReturnsNone()
    {
        Option<int?> nullable = Option<int?>.None;

        Assert.True(nullable.Normalize().IsNone);
    }

    [Fact]
    public void Normalize_WhenSomeWithoutValue_ReturnsNone()
    {
        var nullable = CreateSomeNullableWithoutValue();

        Assert.True(nullable.Normalize().IsNone);
    }

    [Fact]
    public void Normalize_WhenSomeWithValue_ReturnsSome()
    {
        var nullable = CreateSomeNullableWithValue(4);

        Assert.Equal(4, nullable.Normalize().Get());
    }

    [Fact]
    public void Transform_WhenSome_AppliesSomeSelector()
    {
        var result = Option.From(2).Transform(x => x + 1, () => 0);

        Assert.Equal(3, result.Get());
    }

    [Fact]
    public void Transform_WhenNone_AppliesNoneSelector()
    {
        var result = Option<int>.None.Transform(x => x + 1, () => 9);

        Assert.Equal(9, result.Get());
    }

    [Fact]
    public void Transform_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Option.From(1).Transform<int, int>(null!, () => 0)
        );
        Assert.Throws<ArgumentNullException>(() =>
            Option.From(1).Transform(x => x, (Func<int>)null!)
        );
    }

    private static Option<int?> CreateSomeNullableWithoutValue()
    {
        var constructor = typeof(Option<int?>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(int?)],
            modifiers: null
        );

        return (Option<int?>)constructor!.Invoke([(int?)null]);
    }

    private static Option<int?> CreateSomeNullableWithValue(int value)
    {
        var constructor = typeof(Option<int?>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(int?)],
            modifiers: null
        );

        return (Option<int?>)constructor!.Invoke([(int?)value]);
    }
}
