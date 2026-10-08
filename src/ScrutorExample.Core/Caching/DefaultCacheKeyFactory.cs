using System.Reflection;

namespace ScrutorExample.Core.Caching;

/// <summary>
/// Default <see cref="ICacheKeyFactory"/>:
/// <c>Interface.Method(argument)</c> in lowercase, e.g.
/// <c>iproductservice.getasync(22222222-2222-…)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Parameters that are not part of the request identity (a
/// <see cref="CancellationToken"/>, for example) are skipped, so callers can pass
/// the complete argument list without polluting the key.
/// </para>
/// <para>
/// Registered automatically during composition. Applications replace it with
/// <c>services.AddSingleton&lt;ICacheKeyFactory, MyFactory&gt;()</c> when they need
/// tenant-, user- or version-aware keys.
/// </para>
/// </remarks>
public sealed class DefaultCacheKeyFactory : IDefaultCacheKeyFactory
{
    /// <inheritdoc />
    public string CreateKey(MethodInfo method, object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(arguments);

        // DeclaringType is the interface (IProductService) because the decorator
        // passes the MethodInfo of the interface method it implements.
        var service = method.DeclaringType?.Name ?? "unknown";
        var parameters = method.GetParameters();

        var parts = arguments
            .Select((argument, index) => (Argument: argument, Index: index))
            .Where(item => item.Index >= parameters.Length
                || CacheKeyParameter.IsKeyParticipant(parameters[item.Index]))
            .Select(item => CacheKeyParameter.Format(item.Argument));

        return $"{service}.{method.Name}({string.Join(',', parts)})".ToLowerInvariant();
    }
}
