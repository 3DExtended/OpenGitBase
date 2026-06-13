using System.Collections;

namespace OpenGitBase.Cqrs.Tests.Core;

public class OptionOfTTests
{
    [Fact]
    public void None_IsNoneAndNotSome()
    {
        Option<string> option = Option<string>.None;

        Assert.True(option.IsNone);
        Assert.False(option.IsSome);
        Assert.Equal("None", option.ToString());
    }

    [Fact]
    public void Some_IsSomeAndToStringContainsValue()
    {
        Option<int> option = 5;

        Assert.True(option.IsSome);
        Assert.False(option.IsNone);
        Assert.Equal("Some(5)", option.ToString());
    }

    [Fact]
    public void ImplicitConversion_FromNone()
    {
        Option<int> option = default(None);

        Assert.True(option.IsNone);
    }

    [Fact]
    public void ImplicitConversion_FromValue()
    {
        Option<string> option = "hello";

        Assert.Equal("hello", option.Get());
    }

    [Fact]
    public void Equality_BothNone_AreEqual()
    {
        var left = Option<int>.None;
        var right = Option<int>.None;

        Assert.True(left == right);
        Assert.False(left != right);
        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
    }

    [Fact]
    public void Equality_BothSomeWithSameValue_AreEqual()
    {
        var left = Option.From("same");
        var right = Option.From("same");

        Assert.True(left == right);
        Assert.True(left.Equals(right));
    }

    [Fact]
    public void Equality_SomeAndNone_AreNotEqual()
    {
        var left = Option.From("value");
        var right = Option<string>.None;

        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    public void Equality_DifferentObjectType_ReturnsFalse()
    {
        var option = Option.From(1);

        Assert.False(((IStructuralEquatable)option).Equals("not-an-option", EqualityComparer<object>.Default));
        Assert.False(option.Equals((object)"not-an-option"));
    }

    [Fact]
    public void GetHashCode_MatchesForEqualOptions()
    {
        var left = Option.From(10);
        var right = Option.From(10);

        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void CompareTo_SomeIsGreaterThanNone()
    {
        IComparable left = Option.From(1);
        var right = Option<int>.None;

        Assert.True(left.CompareTo(right) > 0);
    }

    [Fact]
    public void CompareTo_NoneIsLessThanSome()
    {
        IComparable left = Option<int>.None;
        var right = Option.From(1);

        Assert.True(left.CompareTo(right) < 0);
    }

    [Fact]
    public void CompareTo_BothSome_UsesComparer()
    {
        IComparable<Option<int>> left = Option.From(2);
        var right = Option.From(5);

        Assert.True(left.CompareTo(right) < 0);
    }

    [Fact]
    public void CompareTo_WrongType_ThrowsArgumentException()
    {
        IStructuralComparable option = Option.From(1);

        Assert.Throws<ArgumentException>(() =>
            option.CompareTo("wrong", Comparer<object>.Default)
        );
    }

    [Fact]
    public void IfNone_WhenNone_ExecutesSideEffect()
    {
        var executed = false;
        Option<int>.None.IfNone(() => executed = true);

        Assert.True(executed);
    }

    [Fact]
    public void IfNone_WhenSome_DoesNotExecuteSideEffect()
    {
        var executed = false;
        Option.From(1).IfNone(() => executed = true);

        Assert.False(executed);
    }

    [Fact]
    public void IfNone_NullSideEffect_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option<int>.None.IfNone(null!));
    }

    [Fact]
    public void IfSome_WhenSome_ExecutesSideEffect()
    {
        var captured = 0;
        Option.From(9).IfSome(value => captured = value);

        Assert.Equal(9, captured);
    }

    [Fact]
    public void IfSome_WhenNone_DoesNotExecuteSideEffect()
    {
        var executed = false;
        Option<int>.None.IfSome(_ => executed = true);

        Assert.False(executed);
    }

    [Fact]
    public void IfSome_NullSideEffect_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Option.From(1).IfSome(null!));
    }

    [Fact]
    public void Match_Action_CallsVoidOverloadDirectly()
    {
        Option<int> option = 7;
        var observed = 0;

        option.Match((Action<int>)(value => observed = value), (Action)(() => observed = -1));

        Assert.Equal(7, observed);
    }

    [Fact]
    public void Match_Action_WhenNone_InvokesNoneDelegate()
    {
        var noneInvoked = false;
        Option<int>.None.Match(_ => { }, () => noneInvoked = true);

        Assert.True(noneInvoked);
    }

    [Fact]
    public void Match_Action_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Option.From(1).Match(null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => Option.From(1).Match(_ => { }, null!));
    }

    [Fact]
    public void Match_Func_ExecutesCorrectBranch()
    {
        Assert.Equal(6, Option.From(3).Match(x => x * 2, () => 0));
        Assert.Equal(0, Option<int>.None.Match(x => x * 2, () => 0));
    }

    [Fact]
    public void Match_Func_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Option.From(1).Match<int>(null!, () => 0));
        Assert.Throws<ArgumentNullException>(() => Option.From(1).Match(x => x, null!));
    }
}
