namespace OpenGitBase.Cqrs.Tests.Extensions;

public class EnumerableExtTests
{
    [Fact]
    public void AggregateOptional_Empty_ReturnsNone()
    {
        var result = Array.Empty<int>().AggregateOptional((a, b) => a + b);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void AggregateOptional_AllNull_ReturnsNone()
    {
        var result = new string?[] { null, null }.AggregateOptional((a, b) => $"{a}{b}");

        Assert.True(result.IsNone);
    }

    [Fact]
    public void AggregateOptional_FoldsValues()
    {
        var result = new[] { 1, 2, 3 }.AggregateOptional((a, b) => a + b);

        Assert.Equal(6, result.Get());
    }

    [Fact]
    public void AggregateOptional_SkipsNullMiddleValues()
    {
        var result = new string?[] { "a", null, "b" }.AggregateOptional((a, b) => $"{a}{b}");

        Assert.Equal("ab", result.Get());
    }

    [Fact]
    public void AggregateOptional_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<int>)null!).AggregateOptional((a, b) => a + b)
        );
        Assert.Throws<ArgumentNullException>(() =>
            new[] { 1 }.AggregateOptional((Func<int, int, int>)null!)
        );
    }

    [Fact]
    public void AggregateOptionalNullable_FoldsAllValues()
    {
        int?[] values = [1, 2, 3];

        var result = values.AggregateOptionalNullable((a, b) => a + b);

        Assert.Equal(6, result.Get());
    }

    [Fact]
    public void AllOrNone_IntNullableOverload_UsesStructConstraintOverload()
    {
        Option<int?>[] items = [CreateSomeNullableWithValue(5), CreateSomeNullableWithValue(6)];

        var result = EnumerableExt.AllOrNone<int>(items);

        Assert.Equal(new[] { 5, 6 }, result.Get());
    }

    [Fact]
    public void AllOrNone_IntNullableOverload_EmptyResults_ReturnsNone()
    {
        Option<int?>[] items = [CreateSomeNullableWithoutValue()];

        var result = EnumerableExt.AllOrNone<int>(items);

        Assert.True(result.IsNone);
    }

    [Fact]
    public void AggregateOptionalNullable_SkipsNullAndFoldsRemaining()
    {
        int?[] values = [1, null, 2];

        var result = values.AggregateOptionalNullable((a, b) => a + b);

        Assert.Equal(3, result.Get());
    }

    [Fact]
    public void AggregateOptionalNullable_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<int?>)null!).AggregateOptionalNullable((a, b) => a + b)
        );
    }

    [Fact]
    public void AllOrNone_Empty_ReturnsNone()
    {
        var result = Array.Empty<Option<int>>().AllOrNone();

        Assert.True(result.IsNone);
    }

    [Fact]
    public void AllOrNone_WhenAnyNone_ReturnsNone()
    {
        var result = new[] { Option.From(1), Option<int>.None }.AllOrNone();

        Assert.True(result.IsNone);
    }

    [Fact]
    public void AllOrNone_WhenAllSome_ReturnsEnumerable()
    {
        var result = new[] { Option.From(1), Option.From(2) }.AllOrNone();

        Assert.Equal(new[] { 1, 2 }, result.Get());
    }

    [Fact]
    public void AllOrNone_StructNullableOverload_WhenAnyOptionIsNone_ReturnsNone()
    {
        var result = new Option<int?>[]
        {
            CreateSomeNullableWithValue(1),
            Option<int?>.None,
        }.AllOrNone();

        Assert.True(result.IsNone);
    }

    [Fact]
    public void AllOrNone_StructNullableOverload_WhenSomeWithoutValue_ReturnsOnlyPresentValues()
    {
        var result = new Option<int?>[]
        {
            CreateSomeNullableWithValue(1),
            CreateSomeNullableWithoutValue(),
        }.AllOrNone();

        Assert.True(result.IsSome);
        Assert.Equal(new[] { 1 }, result.Get());
    }

    [Fact]
    public void AllOrNone_StructNullableOverload_WhenAllHaveValues_ReturnsEnumerable()
    {
        var result = new Option<int?>[]
        {
            CreateSomeNullableWithValue(1),
            CreateSomeNullableWithValue(2),
        }.AllOrNone();

        Assert.Equal(new[] { 1, 2 }, result.Get());
    }

    [Fact]
    public void AllOrNone_StructNullableOverload_NullEnumerable_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<Option<int?>>)null!).AllOrNone());
    }

    [Fact]
    public void AllOrNone_Nullable_WhenAllHaveValues_ReturnsEnumerable()
    {
        var result = new[] { Option.From((int?)1), Option.From((int?)2) }.AllOrNone();

        Assert.Equal(new[] { 1, 2 }, result.Get());
    }

    [Fact]
    public void AllOrNone_NullEnumerable_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<Option<int>>)null!).AllOrNone());
    }

    [Fact]
    public void Exchange_IEnumerable_WhenNone_ReturnsEmpty()
    {
        Option<IEnumerable<int>> source = Option<IEnumerable<int>>.None;

        Assert.Empty(source.Exchange());
    }

    [Fact]
    public void Exchange_IEnumerable_WhenSome_ReturnsOptions()
    {
        Option<IEnumerable<int>> source = Option.From(new[] { 1, 2 }.AsEnumerable());

        Assert.Equal(new[] { 1, 2 }, source.Exchange().Select(x => x.Get()));
    }

    [Fact]
    public void Exchange_IEnumerableNullable_WhenSome_ReturnsOptions()
    {
        Option<IEnumerable<int?>> source = Option.From(new int?[] { 1, null }.AsEnumerable());

        var result = source.Exchange().ToList();
        Assert.True(result[0].IsSome);
        Assert.True(result[1].IsNone);
    }

    [Fact]
    public void Exchange_Array_WhenNone_ReturnsEmptyArray()
    {
        Option<int[]> source = Option<int[]>.None;

        Assert.Empty(source.Exchange());
    }

    [Fact]
    public void Exchange_Array_WhenSome_ReturnsOptions()
    {
        Option<int[]> source = Option.From(new[] { 3, 4 });

        Assert.Equal(new[] { 3, 4 }, source.Exchange().Select(x => x.Get()));
    }

    [Fact]
    public void Exchange_ArrayNullable_WhenSome_ReturnsOptions()
    {
        Option<int?[]> source = Option.From(new int?[] { 1, null });

        var result = source.Exchange();
        Assert.True(result[0].IsSome);
        Assert.True(result[1].IsNone);
    }

    [Fact]
    public void FirstOptional_NullableEnumerable_ReturnsFirstValue()
    {
        int?[] values = [9, 10];

        Assert.Equal(9, values.FirstOptional().Get());
    }

    [Fact]
    public void LastOptional_NullableEnumerable_ReturnsLastValue()
    {
        int?[] values = [9, 10];

        Assert.Equal(10, values.LastOptional().Get());
    }

    [Fact]
    public void FirstOptional_Empty_ReturnsNone()
    {
        Assert.True(Array.Empty<string>().FirstOptional().IsNone);
    }

    [Fact]
    public void FirstOptional_WithNull_ReturnsNone()
    {
        Assert.True(
            new string?[] { null }
                .FirstOptional()
                .IsNone
        );
    }

    [Fact]
    public void FirstOptional_WithValue_ReturnsSome()
    {
        Assert.Equal("a", new[] { "a", "b" }.FirstOptional().Get());
    }

    [Fact]
    public void FirstOptional_Nullable_ReturnsSome()
    {
        int?[] values = [7];

        Assert.Equal(7, values.FirstOptional().Get());
    }

    [Fact]
    public void LastOptional_Empty_ReturnsNone()
    {
        Assert.True(Array.Empty<string>().LastOptional().IsNone);
    }

    [Fact]
    public void LastOptional_WithNull_ReturnsNone()
    {
        Assert.True(
            new string?[] { null }
                .LastOptional()
                .IsNone
        );
    }

    [Fact]
    public void LastOptional_WithValue_ReturnsLast()
    {
        Assert.Equal("b", new[] { "a", "b" }.LastOptional().Get());
    }

    [Fact]
    public void LastOptional_Nullable_ReturnsLast()
    {
        int?[] values = [1, 2];

        Assert.Equal(2, values.LastOptional().Get());
    }

    [Fact]
    public void SelectValues_ReturnsOnlySomeValues()
    {
        var result = new[] { Option.From(1), Option<int>.None, Option.From(3) }.SelectValues();

        Assert.Equal(new[] { 1, 3 }, result);
    }

    [Fact]
    public void SelectValues_Nullable_ReturnsOnlyValuesWithHasValue()
    {
        var result = new[]
        {
            Option.From((int?)1),
            Option.From((int?)null),
            Option.From((int?)3),
        }.SelectValues();

        Assert.Equal(new[] { 1, 3 }, result);
    }

    [Fact]
    public void SelectValues_NullEnumerable_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<Option<int>>)null!).SelectValues()
        );
    }

    [Fact]
    public void SingleOptional_Empty_ReturnsNone()
    {
        Assert.True(Array.Empty<int>().SingleOptional().IsNone);
    }

    [Fact]
    public void SingleOptional_SingleValue_ReturnsSome()
    {
        Assert.Equal(9, new[] { 9 }.SingleOptional().Get());
    }

    [Fact]
    public void SingleOptional_MultipleValues_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new[] { 1, 2 }.SingleOptional());
    }

    [Fact]
    public void SingleOptional_Nullable_SingleValue_ReturnsSome()
    {
        int?[] values = [4];

        Assert.Equal(4, values.SingleOptional().Get());
    }

    [Fact]
    public void SingleOptional_WithPredicate_ReturnsMatchingValue()
    {
        Assert.Equal(5, new[] { 1, 5, 9 }.SingleOptional(x => x == 5).Get());
    }

    [Fact]
    public void SingleOptional_WithPredicate_NoMatch_ReturnsNone()
    {
        Assert.True(
            new string?[] { "a", "b" }
                .SingleOptional(x => x == "z")
                .IsNone
        );
    }

    [Fact]
    public void SingleOptional_WithPredicate_MultipleMatches_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new[] { 5, 5 }.SingleOptional(x => x == 5));
    }

    [Fact]
    public void SingleOptionalNullable_WithPredicate_ReturnsMatchingValue()
    {
        int?[] values = [1, 2, 3];

        Assert.Equal(2, values.SingleOptionalNullable(x => x == 2).Get());
    }

    [Fact]
    public void SingleOptionalNullable_WithPredicate_NoMatch_ReturnsNone()
    {
        int?[] values = [1, 3];

        Assert.True(values.SingleOptionalNullable(x => x == 2).IsNone);
    }

    [Fact]
    public void FirstLastSingle_NullEnumerable_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).FirstOptional());
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).LastOptional());
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).SingleOptional());
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<int>)null!).SingleOptional(_ => true)
        );
    }

    [Fact]
    public void SingleOptional_WithNullPredicate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new[] { 1 }.SingleOptional((Func<int, bool>)null!)
        );
    }

    [Fact]
    public void SingleOptionalNullable_WithNullPredicate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new int?[] { 1 }.SingleOptionalNullable((Func<int, bool>)null!)
        );
    }

    [Fact]
    public void SelectValues_StructNullableOverload_ReturnsOnlyValuesWithHasValue()
    {
        var result = new Option<int?>[]
        {
            CreateSomeNullableWithValue(1),
            CreateSomeNullableWithoutValue(),
            CreateSomeNullableWithValue(3),
        }.SelectValues();

        Assert.Equal(new[] { 1, 3 }, result);
    }

    [Fact]
    public void SelectValues_StructNullableOverload_NullEnumerable_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<Option<int?>>)null!).SelectValues()
        );
    }

    private static Option<int?> CreateSomeNullableWithValue(int value)
    {
        var constructor = typeof(Option<int?>).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            [typeof(int?)],
            modifiers: null
        );

        return (Option<int?>)constructor!.Invoke([(int?)value]);
    }

    private static Option<int?> CreateSomeNullableWithoutValue()
    {
        var constructor = typeof(Option<int?>).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            [typeof(int?)],
            modifiers: null
        );

        return (Option<int?>)constructor!.Invoke([(int?)null]);
    }
}
