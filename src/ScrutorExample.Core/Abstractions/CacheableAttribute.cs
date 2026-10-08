using ScrutorExample.Core.Caching;

namespace ScrutorExample.Core.Abstractions;

/// <summary>
/// Marks a method whose result may be cached by <c>CachingProductServiceDecorator</c>.
/// </summary>
/// <remarks>
/// <para>
/// Expressing "cache this" as an attribute keeps the caching policy next to the
/// service contract instead of hard-coding method names inside the decorator.
/// </para>
/// <para>
/// <b>Constraint for this sample:</b> a decorated method may have one
/// identity-carrying parameter (a <see cref="Guid"/>, <see cref="string"/>, number
/// or <see cref="Enum"/>); a trailing <see cref="CancellationToken"/> is ignored
/// automatically by <see cref="CacheKeyParameter.IsKeyParticipant"/>. This keeps
/// <see cref="ICacheKeyFactory"/> dependency-free. In a real system you would pass
/// a strongly typed cache-key request object or plug in a smarter
/// <see cref="ICacheKeyFactory"/>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class CacheableAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheableAttribute"/> class.
    /// </summary>
    public CacheableAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheableAttribute"/> class
    /// with an explicit cache duration.
    /// </summary>
    /// <param name="seconds">How long the entry should stay fresh.</param>
    public CacheableAttribute(int seconds) => Seconds = seconds;

    /// <summary>
    /// Gets or sets the cache duration in seconds. <c>0</c> (the default) means
    /// "use <c>CachingOptions.DefaultExpiration</c>".
    /// </summary>
    /// <remarks>
    /// An <see cref="int"/> rather than <see cref="Nullable{T}"/> because attribute
    /// arguments cannot be nullable value types.
    /// </remarks>
    public int Seconds { get; set; }
}
