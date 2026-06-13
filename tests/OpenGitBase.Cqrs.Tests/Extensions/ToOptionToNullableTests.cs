namespace OpenGitBase.Cqrs.Tests.Extensions;

public class ToOptionToNullableTests
{
    [Fact]
    public void ToOption_ReferenceType_WrapsValue()
    {
        Assert.Equal("x", "x".ToOption().Get());
    }

    [Fact]
    public void ToOption_NullableWithoutValue_ReturnsNone()
    {
        int? value = null;

        Assert.True(value.ToOption().IsNone);
    }

    [Fact]
    public void ToOption_NullableWithValue_ReturnsSome()
    {
        int? value = 3;

        Assert.Equal(3, value.ToOption().Get());
    }

    [Fact]
    public void ToOptionMapped_MapsNullableValue()
    {
        int? value = 4;

        Assert.Equal("4", value.ToOptionMapped(x => x.ToString()).Get());
    }

    [Fact]
    public void ToOptionMappedOrNoneIf_WhenEqualToNullValue_ReturnsNone()
    {
        Assert.True(0.ToOptionMappedOrNoneIf(0, x => x + 1).IsNone);
    }

    [Fact]
    public void ToOptionMappedOrNoneIf_WhenNotEqualToNullValue_ReturnsMapped()
    {
        Assert.Equal(6, 5.ToOptionMappedOrNoneIf(0, x => x + 1).Get());
    }

    [Fact]
    public void ToNullable_WhenSome_ReturnsValue()
    {
        Assert.Equal(8, Option.From(8).ToNullable());
    }

    [Fact]
    public void ToNullable_WhenNone_ReturnsNull()
    {
        Assert.Null(Option<int>.None.ToNullable());
    }

    [Fact]
    public void ToNullable_WithSelector_MapsValue()
    {
        Assert.Equal(10, Option.From(5).ToNullable(x => x * 2));
    }
}
