using System.Globalization;
using System.Reflection;

namespace ScrutorExample.Core.Caching;

/// <summary>
/// Builds the cache key for a decorated method call.
/// </summary>
/// <remarks>
/// The key factory is a first-class service on purpose: the caching decorator and
/// any component that wants to invalidate an entry (an API endpoint, a background
/// job) must agree on the exact same string. Sharing this abstraction prevents the
/// classic "we cached under a different key than we evicted" bug.
/// </remarks>
public interface ICacheKeyFactory
{
    /// <summary>
    /// Creates a cache key for a decorated method invocation.
    /// </summary>
    /// <param name="method">The interface method that was invoked.</param>
    /// <param name="arguments">The arguments the method was invoked with (may include infrastructure parameters).</param>
    /// <returns>A stable, unique cache key.</returns>
    string CreateKey(MethodInfo method, object?[] arguments);
}

/// <summary>
/// Marker interface used by the default key factory implementation so that the
/// caching decorator can tell "no custom factory registered" apart from
/// "a custom factory was registered but returned <see langword="null"/>".
/// </summary>
public interface IDefaultCacheKeyFactory : ICacheKeyFactory;

/// <summary>
/// Decides which method parameters take part in a cache key, and renders the
/// query objects used by the generic handler decorator.
/// </summary>
public static class CacheKeyParameter
{
    /// <summary>
    /// Determines whether a method parameter identifies the request and therefore
    /// belongs in the cache key.
    /// </summary>
    /// <param name="parameter">The parameter to inspect.</param>
    /// <returns>
    /// <see langword="true"/> for identity-carrying parameters; <see langword="false"/>
    /// for infrastructure parameters such as <see cref="CancellationToken"/>.
    /// </returns>
    /// <remarks>
    /// <see cref="CancellationToken"/> is deliberately excluded: every call site
    /// passes its own token and including it in the key would turn every request
    /// into a cache miss.
    /// </remarks>
    public static bool IsKeyParticipant(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        return parameter.ParameterType != typeof(CancellationToken);
    }

    /// <summary>
    /// Renders a value so that it produces a stable cache key.
    /// </summary>
    /// <param name="value">The value to render.</param>
    /// <returns>A culture-invariant representation.</returns>
    /// <remarks>
    /// Records are rendered from their properties instead of <c>ToString()</c>:
    /// the compiler-generated implementation embeds the type name and is not
    /// guaranteed to be stable across refactorings.
    /// </remarks>
    public static string Format(object? value) => value switch
    {
        null => "null",
        string text => text,
        Type type => type.FullName ?? type.Name,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => FormatComplex(value),
    };

    private static string FormatComplex(object value)
    {
        var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        if (properties.Length == 0)
        {
            // A parameterless query (e.g. GetProductsQuery) still needs a stable key.
            return value.GetType().Name.ToLowerInvariant();
        }

        var parts = properties
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name.ToLowerInvariant()}={Format(property.GetValue(value))}");

        return $"{value.GetType().Name.ToLowerInvariant()}[{string.Join(',', parts)}]";
    }
}
