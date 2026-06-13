namespace OpenGitBase.Cqrs.Tests.Extensions;

public class ContainsExtTests
{
    [Fact]
    public void Contains_WhenNone_ReturnsFalse()
    {
        Assert.False(Option<string>.None.Contains("value"));
    }

    [Fact]
    public void Contains_WhenSomeAndEqual_ReturnsTrue()
    {
        Assert.True(Option.From("value").Contains("value"));
    }

    [Fact]
    public void Contains_WhenSomeAndNotEqual_ReturnsFalse()
    {
        Assert.False(Option.From("value").Contains("other"));
    }

    [Fact]
    public void Contains_WithCustomComparer_UsesComparer()
    {
        Assert.True(Option.From("Ab").Contains("ab", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Contains_WithCompareFunction_UsesFunction()
    {
        Assert.True(Option.From(10).Contains(5, (left, right) => left == right * 2));
    }

    [Fact]
    public void Contains_WithNullCompare_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Option.From(1).Contains(1, (Func<int, int, bool>)null!)
        );
    }
}
