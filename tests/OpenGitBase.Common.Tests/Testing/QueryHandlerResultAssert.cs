using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Cqrs.EfCore.Abstractions;

namespace OpenGitBase.Common.Tests.Testing;

public static class QueryHandlerResultAssert
{
    public static T AssertSome<T>(Option<T> result, Action<T>? assert = null)
    {
        Assert.True(result.IsSome, "Expected Option to be Some.");
        var value = result.Get();
        assert?.Invoke(value);
        return value;
    }

    public static void AssertNone<T>(Option<T> result) =>
        Assert.True(result.IsNone, "Expected Option to be None.");

    public static void AssertUnit(Option<Unit> result) =>
        AssertSome(result, unit => Assert.Equal(Unit.Value, unit));

    public static void AssertIdentifierNonEmpty<TIdentifierValue>(
        IIdentifier<TIdentifierValue> identifier
    ) => Assert.NotEqual(default(TIdentifierValue), identifier.Value);
}
