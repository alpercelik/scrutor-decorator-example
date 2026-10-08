using ScrutorExample.Core.Abstractions;

namespace ScrutorExample.Core.Caching;

/// <summary>
/// Options for the caching decorator, bound from the <c>Caching</c> configuration
/// section. Keeping the policy in configuration means the decorator itself never
/// has to change when the team decides to cache for 5 minutes instead of 30 seconds.
/// </summary>
public sealed class CachingOptions
{
    /// <summary>The configuration section this class binds to.</summary>
    public const string SectionName = "Caching";

    /// <summary>
    /// Default lifetime of a cache entry. Used when <see cref="CacheableAttribute"/>
    /// does not specify an explicit duration.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enables or disables caching globally. Resolved per call from
    /// <c>IOptionsMonitor&lt;CachingOptions&gt;</c>, so it can be toggled at runtime.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Prefix applied to every key so multiple deployments can share one cache.
    /// </summary>
    public string KeyPrefix { get; set; } = "scrutor-example";

    /// <summary>
    /// Optional sliding expiration. When set, every cache hit renews the entry.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }
}
