namespace OpenGitBase.Cqrs.Tests.Core;

public class OptionTests
{
    [Fact]
    public void None_ReturnsDefaultNone()
    {
        Assert.Equal(default(None), Option.None);
    }

    [Fact]
    public void From_ReferenceTypeNull_ReturnsNone()
    {
        var result = Option.From<string>(null);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void From_ReferenceTypeValue_ReturnsSome()
    {
        var result = Option.From("value");

        Assert.True(result.IsSome);
        Assert.Equal("value", result.Get());
    }

    [Fact]
    public void From_NullableWithoutValue_ReturnsNone()
    {
        int? value = null;

        var result = Option.From(value);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void From_NullableWithValue_ReturnsSome()
    {
        int? value = 7;

        var result = Option.From(value);

        Assert.True(result.IsSome);
        Assert.Equal(7, result.Get());
    }
}
