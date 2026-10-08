using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Caching;

namespace ScrutorExample.Core.Decorators;

/// <summary>
/// Open generic caching decorator for query handlers whose query type is annotated
/// with <see cref="CacheableQueryAttribute"/>.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>
/// The attribute is read from the <b>closed</b> generic type argument
/// (<typeparamref name="TQuery"/>), which is what makes a per-request-type decision
/// possible inside a single open generic decorator. Every other query passes
/// straight through.
/// </remarks>
/// <param name="inner">The real handler being wrapped.</param>
/// <param name="cache">The cache implementation.</param>
/// <param name="options">Cache policy.</param>
/// <param name="logger">Logger for this category.</param>
public sealed partial class CachingQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IMemoryCache cache,
    IOptionsMonitor<CachingOptions> options,
    ILogger<CachingQueryHandlerDecorator<TQuery, TResult>> logger) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private static readonly CacheableQueryAttribute? Cacheable =
        typeof(TQuery).GetCustomAttribute<CacheableQueryAttribute>(inherit: true);

    /// <inheritdoc />
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = options.CurrentValue;
        if (Cacheable is null)
        {
            NotCacheable(logger, typeof(TQuery).Name);
            return await inner.HandleAsync(query, cancellationToken).ConfigureAwait(false);
        }

        if (!settings.Enabled)
        {
            CacheDisabled(logger, typeof(TQuery).Name);
            return await inner.HandleAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var key = $"{settings.KeyPrefix}:query:{typeof(TQuery).Name.ToLowerInvariant()}:{CacheKeyParameter.Format(query)}";

        // Wrapping the result keeps null values cacheable: TryGetValue alone cannot
        // distinguish "absent" from "present but null".
        if (cache.TryGetValue(key, out CacheableResponse<TResult>? entry) && entry is not null)
        {
            CacheHit(logger, key);
            return entry.Value;
        }

        var result = await inner.HandleAsync(query, cancellationToken).ConfigureAwait(false);

        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Cacheable.Seconds > 0
                ? TimeSpan.FromSeconds(Cacheable.Seconds)
                : settings.DefaultExpiration,
        };

        if (settings.SlidingExpiration is { } sliding)
        {
            entryOptions.SlidingExpiration = sliding;
        }

        cache.Set(key, new CacheableResponse<TResult>(result), entryOptions);
        CacheMiss(logger, key, entryOptions.AbsoluteExpirationRelativeToNow);

        return result;
    }

    [LoggerMessage(EventId = 2201, Level = LogLevel.Information, Message = "CACHE HIT  {Key}")]
    private static partial void CacheHit(ILogger logger, string key);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Information, Message = "CACHE MISS {Key} — stored for {Expiration}")]
    private static partial void CacheMiss(ILogger logger, string key, TimeSpan? expiration);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Debug, Message = "Query {Query} is not marked [CacheableQuery]; forwarding.")]
    private static partial void NotCacheable(ILogger logger, string query);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Debug, Message = "Caching is disabled; forwarding {Query}.")]
    private static partial void CacheDisabled(ILogger logger, string query);

    /// <summary>
    /// Cache envelope that makes <see langword="null"/> results cacheable.
    /// </summary>
    /// <typeparam name="T">The cached result type.</typeparam>
    /// <param name="Value">The cached handler result.</param>
    private sealed record CacheableResponse<T>(T Value);
}
