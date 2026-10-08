using Microsoft.Extensions.Logging;
using ScrutorExample.Core.Abstractions;

namespace ScrutorExample.Core.Decorators;

/// <summary>
/// Open generic logging decorator for every <see cref="IQueryHandler{TQuery,TResult}"/>
/// in the application.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>
/// A single registration decorates <b>all</b> handlers:
/// <c>services.TryDecorate(typeof(IQueryHandler&lt;,&gt;), typeof(LoggingQueryHandlerDecorator&lt;,&gt;));</c>
/// Add a new query handler and it is logged automatically — no registration, no
/// base class, no interceptor configuration.
/// </remarks>
/// <param name="inner">The next handler in the chain (the caching decorator or the real handler).</param>
/// <param name="logger">Logger for this category.</param>
public sealed class LoggingQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<LoggingQueryHandlerDecorator<TQuery, TResult>> logger) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <inheritdoc />
    public Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return DecoratorLog.RunAsync(
            logger,
            typeof(IQueryHandler<TQuery, TResult>),
            [query, cancellationToken],
            () => inner.HandleAsync(query, cancellationToken));
    }
}
