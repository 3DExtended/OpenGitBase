namespace OpenGitBase.Cqrs.Tests.Extensions;

public class SelectExtTests
{
    [Fact]
    public void Select_WhenSome_AppliesSelector()
    {
        var result = Option.From(2).Select(x => x * 3);

        Assert.True(result.IsSome);
        Assert.Equal(6, result.Get());
    }

    [Fact]
    public void Select_WhenNone_ReturnsNone()
    {
        var result = Option<int>.None.Select(x => x * 3);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void Select_NullSelector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option.From(1).Select<int, int>(null!));
    }
}
