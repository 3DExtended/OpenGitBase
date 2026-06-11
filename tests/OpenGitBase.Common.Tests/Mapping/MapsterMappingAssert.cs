using System.Collections;
using System.Reflection;
using MapsterMapper;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Cqrs.EfCore.Abstractions;

namespace OpenGitBase.Common.Tests.Mapping;

public static class MapsterMappingAssert
{
    private static readonly Guid SampleId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static void AssertRoundTrip<TSource, TDest>(
        IMapper mapper,
        IEnumerable<(Type Type, string Name)>? excluded = null
    )
    {
        var excludedSet = BuildExcludedSet(excluded);
        AssertNoUnaccountedReferenceProperties(typeof(TSource), excludedSet);
        AssertNoUnaccountedReferenceProperties(typeof(TDest), excludedSet);

        var source =
            Activator.CreateInstance<TSource>()
            ?? throw new InvalidOperationException(
                $"Could not create an instance of {typeof(TSource).Name}."
            );

        PopulateInstance(source, excludedSet);

        var destination = mapper.Map<TDest>(source);
        var roundTripped = mapper.Map<TSource>(destination!);

        AssertPropertiesMatch(source, roundTripped!, excludedSet, typeof(TSource));
    }

    private static HashSet<(Type Type, string Name)> BuildExcludedSet(
        IEnumerable<(Type Type, string Name)>? excluded
    )
    {
        return excluded?.ToHashSet() ?? [];
    }

    private static void AssertNoUnaccountedReferenceProperties(
        Type type,
        HashSet<(Type Type, string Name)> excluded
    )
    {
        foreach (var property in GetTestableProperties(type))
        {
            if (excluded.Contains((type, property.Name)))
            {
                continue;
            }

            if (!IsStrictReferenceProperty(property.PropertyType))
            {
                continue;
            }

            throw new Xunit.Sdk.XunitException(
                $"Reference property '{type.Name}.{property.Name}' must be mapped or excluded. "
                    + $"Add (typeof({type.Name}), nameof({type.Name}.{property.Name})) to ExcludedProperties."
            );
        }
    }

    private static bool IsStrictReferenceProperty(Type propertyType)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlyingType == typeof(string))
        {
            return false;
        }

        if (underlyingType.IsValueType)
        {
            return false;
        }

        if (IsIdentifierType(underlyingType))
        {
            return false;
        }

        return true;
    }

    private static bool IsIdentifierType(Type type)
    {
        for (
            var current = type;
            current is not null && current != typeof(object);
            current = current.BaseType
        )
        {
            if (
                current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(Identifier<,>)
            )
            {
                return true;
            }
        }

        return type.GetInterfaces()
            .Any(@interface =>
                @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IIdentifier<>)
            );
    }

    private static IEnumerable<PropertyInfo> GetTestableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod is not null && property.SetMethod is not null);

    private static void PopulateInstance(
        object instance,
        HashSet<(Type Type, string Name)> excluded
    )
    {
        foreach (var property in GetTestableProperties(instance.GetType()))
        {
            if (excluded.Contains((instance.GetType(), property.Name)))
            {
                continue;
            }

            property.SetValue(instance, CreateSampleValue(property.Name, property.PropertyType));
        }
    }

    private static object? CreateSampleValue(string propertyName, Type propertyType)
    {
        if (Nullable.GetUnderlyingType(propertyType) is Type underlyingNullableType)
        {
            return CreateSampleValue(propertyName, underlyingNullableType);
        }

        if (propertyType == typeof(string))
        {
            return $"test-{propertyName}";
        }

        if (propertyType == typeof(bool))
        {
            return true;
        }

        if (propertyType == typeof(char))
        {
            return 'x';
        }

        if (propertyType == typeof(Guid))
        {
            return SampleId;
        }

        if (propertyType == typeof(DateTime))
        {
            return new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        }

        if (propertyType == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        }

        if (propertyType == typeof(byte[]))
        {
            return new byte[] { 1, 2, 3 };
        }

        if (propertyType.IsEnum)
        {
            return Enum.GetValues(propertyType).Cast<object>().Last();
        }

        if (propertyType == typeof(decimal))
        {
            return 12.34m;
        }

        if (propertyType == typeof(double))
        {
            return 12.34d;
        }

        if (propertyType == typeof(float))
        {
            return 12.34f;
        }

        if (propertyType == typeof(int))
        {
            return 42;
        }

        if (propertyType == typeof(long))
        {
            return 42L;
        }

        if (propertyType == typeof(short))
        {
            return (short)42;
        }

        if (propertyType == typeof(byte))
        {
            return (byte)7;
        }

        if (IsIdentifierType(propertyType))
        {
            return CreateIdentifierValue(propertyType, SampleId);
        }

        throw new InvalidOperationException(
            $"Cannot populate sample value for property type '{propertyType.Name}'."
        );
    }

    private static object CreateIdentifierValue(Type identifierType, Guid value)
    {
        for (var type = identifierType; type is not null; type = type.BaseType)
        {
            var fromMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    method.Name == "From"
                    && method.GetParameters() is [{ ParameterType: var parameterType }]
                    && parameterType == typeof(Guid)
                );

            if (fromMethod is not null)
            {
                return fromMethod.Invoke(null, [value])
                    ?? throw new InvalidOperationException(
                        $"From(Guid) on '{identifierType.Name}' returned null."
                    );
            }
        }

        throw new InvalidOperationException(
            $"Identifier type '{identifierType.Name}' is missing a From(Guid) method."
        );
    }

    private static void AssertPropertiesMatch(
        object expected,
        object actual,
        HashSet<(Type Type, string Name)> excluded,
        Type sourceType
    )
    {
        foreach (var property in GetTestableProperties(sourceType))
        {
            if (excluded.Contains((sourceType, property.Name)))
            {
                continue;
            }

            var expectedValue = property.GetValue(expected);
            var actualValue = property.GetValue(actual);

            if (!ValuesMatch(expectedValue, actualValue))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Property '{sourceType.Name}.{property.Name}' did not round-trip. "
                        + $"Expected '{FormatValue(expectedValue)}', got '{FormatValue(actualValue)}'."
                );
            }
        }
    }

    private static bool ValuesMatch(object? expected, object? actual)
    {
        if (expected is null || actual is null)
        {
            return expected is null && actual is null;
        }

        if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
        {
            return expectedBytes.SequenceEqual(actualBytes);
        }

        if (IsIdentifierType(expected.GetType()) && IsIdentifierType(actual.GetType()))
        {
            var expectedId = expected
                .GetType()
                .GetProperty(nameof(IIdentifier<Guid>.Value))!
                .GetValue(expected);
            var actualId = actual
                .GetType()
                .GetProperty(nameof(IIdentifier<Guid>.Value))!
                .GetValue(actual);
            return Equals(expectedId, actualId);
        }

        if (
            expected is IEnumerable expectedEnumerable
            && actual is IEnumerable actualEnumerable
            && expected is not string
        )
        {
            return expectedEnumerable
                .Cast<object?>()
                .SequenceEqual(actualEnumerable.Cast<object?>());
        }

        return Equals(expected, actual);
    }

    private static string FormatValue(object? value) => value?.ToString() ?? "null";
}
