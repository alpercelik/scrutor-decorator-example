namespace ScrutorExample.Core.Abstractions;

/// <summary>
/// Marker interface for an in-process command or query.
/// </summary>
/// <typeparam name="TResult">The result produced by handling the request.</typeparam>
/// <remarks>
/// A tiny CQRS-ish abstraction. It exists to demonstrate Scrutor's ability to
/// scan, register and decorate <b>open generic</b> types — one registration
/// covers every request type in the application.
/// </remarks>
public interface IQuery<out TResult>
{
}

/// <summary>
/// Handles <typeparamref name="TQuery"/> and produces <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TQuery">The request type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The query result.</returns>
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marks a query type as safe to cache.
/// </summary>
/// <remarks>
/// Applied at the <em>query</em> level (not the handler level) because the caching
/// decorator can read it from the closed generic type at runtime. Applying it here
/// is what lets the open generic decorator make a per-request-type decision.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class CacheableQueryAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the cache duration in seconds. <c>0</c> (the default) means
    /// "use <c>CachingOptions.DefaultExpiration</c>".
    /// </summary>
    public int Seconds { get; set; }
}
