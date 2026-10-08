using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Caching;
using ScrutorExample.Core.Entities;

namespace ScrutorExample.Core.Decorators;

/// <summary>
/// Caching decorator for <see cref="IProductService"/>.
/// </summary>
/// <remarks>
/// <para>
/// A method is cached only when it is annotated with <see cref="CacheableAttribute"/>.
/// Everything else is forwarded straight to the inner service.
/// </para>
/// <para>
/// The decorator holds the <see cref="MethodInfo"/> of the interface methods it
/// implements, because that is where the attribute lives — no string matching on
/// method names required.
/// </para>
/// </remarks>
/// <param name="inner">The real service being wrapped.</param>
/// <param name="cache">The cache implementation (in-memory here, distributed-ready by abstraction).</param>
/// <param name="keyFactory">Builds cache keys; shared with whoever evicts entries.</param>
/// <param name="options">Cache policy, hot-reloadable from configuration.</param>
/// <param name="logger">Logger for this category.</param>
public sealed partial class CachingProductServiceDecorator(
    IProductService inner,
    IMemoryCache cache,
    ICacheKeyFactory keyFactory,
    IOptionsMonitor<CachingOptions> options,
    ILogger<CachingProductServiceDecorator> logger) : IProductService
{
    private static readonly MethodInfo GetAllMethod = typeof(IProductService).GetMethod(nameof(IProductService.GetAllAsync))!;
    private static readonly MethodInfo GetMethod = typeof(IProductService).GetMethod(nameof(IProductService.GetAsync))!;

    // Intentionally not annotated with [Cacheable]: proves that only selected
    // members of a decorated interface are cached.
    private static readonly MethodInfo GetPriceMethod = typeof(IProductService).GetMethod(nameof(IProductService.GetPriceWithVatAsync))!;

    /// <inheritdoc />
    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        => CacheOrCreateAsync(
            GetAllMethod,
            [cancellationToken],
            () => inner.GetAllAsync(cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => CacheOrCreateAsync(
            GetMethod,
            [id, cancellationToken],
            () => inner.GetAsync(id, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public Task<decimal?> GetPriceWithVatAsync(Guid id, decimal vatRate, CancellationToken cancellationToken = default)
    {
        // No [Cacheable] attribute on this member -> pure pass-through.
        ForwardingUncached(logger, nameof(GetPriceWithVatAsync), typeof(IProductService).Name);

        return inner.GetPriceWithVatAsync(id, vatRate, cancellationToken);
    }

    private async Task<T> CacheOrCreateAsync<T>(
        MethodInfo method,
        object?[] arguments,
        Func<Task<T>> innerCall,
        CancellationToken cancellationToken = default)
    {
        var attribute = DecoratorReflection.GetCacheableAttribute(method);
        if (attribute is null)
        {
            return await innerCall().ConfigureAwait(false);
        }

        var settings = options.CurrentValue;
        if (!settings.Enabled)
        {
            CacheDisabled(logger, method.Name);
            return await innerCall().ConfigureAwait(false);
        }

        var key = $"{settings.KeyPrefix}:{keyFactory.CreateKey(method, arguments)}";

        if (cache.TryGetValue(key, out var cached) && cached is not null)
        {
            CacheHit(logger, key);
            return (T)cached;
        }

        var created = await innerCall().ConfigureAwait(false);

        // Null results (e.g. an unknown product id) are not cached, so a
        // subsequently created record shows up immediately.
        if (DecoratorReflection.IsEmptyResult(created))
        {
            SkippingNullResult(logger, key);
            return created;
        }

        var entryOptions = new MemoryCacheEntryOptions
        {
            // A per-method [Cacheable(Seconds = n)] wins over the configured default.
            AbsoluteExpirationRelativeToNow = attribute.Seconds > 0
                ? TimeSpan.FromSeconds(attribute.Seconds)
                : settings.DefaultExpiration,
        };

        if (settings.SlidingExpiration is { } sliding)
        {
            entryOptions.SlidingExpiration = sliding;
        }

        cache.Set(key, created, entryOptions);
        CacheMiss(logger, key, entryOptions.AbsoluteExpirationRelativeToNow);

        return created;
    }

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "CACHE HIT  {Key}")]
    private static partial void CacheHit(ILogger logger, string key);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "CACHE MISS {Key} — stored for {Expiration}")]
    private static partial void CacheMiss(ILogger logger, string key, TimeSpan? expiration);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Debug, Message = "Not caching <null> result for {Key}.")]
    private static partial void SkippingNullResult(ILogger logger, string key);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Debug, Message = "Caching is disabled; forwarding {Method}.")]
    private static partial void CacheDisabled(ILogger logger, string method);

    [LoggerMessage(EventId = 2105, Level = LogLevel.Debug, Message = "No [Cacheable] attribute on {Method}; forwarding to {Service}.")]
    private static partial void ForwardingUncached(ILogger logger, string method, string service);
}
