using System.Reflection;
using ScrutorExample.Core.Abstractions;

namespace ScrutorExample.Core.Decorators;

/// <summary>
/// Reflection helpers shared by the decorators.
/// </summary>
internal static class DecoratorReflection
{
    /// <summary>
    /// Reads the <see cref="CacheableAttribute"/> that applies to an interface method.
    /// </summary>
    /// <param name="method">The interface method.</param>
    /// <returns>The attribute when present; otherwise <see langword="null"/>.</returns>
    public static CacheableAttribute? GetCacheableAttribute(MethodInfo? method)
        => method?.GetCustomAttribute<CacheableAttribute>(inherit: true);

    /// <summary>
    /// Returns <see langword="true"/> when a result is not worth caching.
    /// </summary>
    /// <param name="result">The intercepted result.</param>
    /// <returns><see langword="true"/> when the decorator should skip the cache.</returns>
    public static bool IsEmptyResult(object? result) => result is null;
}
