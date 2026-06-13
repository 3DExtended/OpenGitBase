namespace OpenGitBase.Cqrs.Tests.Extensions;

public class WhereRejectTests
{
    [Fact]
    public void Where_WhenNone_ReturnsNone()
    {
        Assert.True(Option<int>.None.Where(_ => true).IsNone);
    }

    [Fact]
    public void Where_WhenPredicateMatches_ReturnsSome()
    {
        Assert.Equal(5, Option.From(5).Where(x => x > 0).Get());
    }

    [Fact]
    public void Where_WhenPredicateFails_ReturnsNone()
    {
        Assert.True(Option.From(5).Where(x => x < 0).IsNone);
    }

    [Fact]
    public void Where_NullPredicate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option.From(1).Where(null!));
    }

    [Fact]
    public void Reject_WhenNone_ReturnsNone()
    {
        Assert.True(Option<int>.None.Reject(_ => true).IsNone);
    }

    [Fact]
    public void Reject_WhenPredicateMatches_ReturnsNone()
    {
        Assert.True(Option.From("bad").Reject(x => x == "bad").IsNone);
    }

    [Fact]
    public void Reject_WhenPredicateFails_ReturnsOriginal()
    {
        Assert.Equal("ok", Option.From("ok").Reject(x => x == "bad").Get());
    }

    [Fact]
    public void Reject_NullPredicate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option.From(1).Reject(null!));
    }
}
