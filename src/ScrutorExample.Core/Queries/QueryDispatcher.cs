using Microsoft.Extensions.DependencyInjection;
using ScrutorExample.Core.Abstractions;

namespace ScrutorExample.Core.Queries;

/// <summary>
/// Resolves and invokes the <see cref="IQueryHandler{TQuery,TResult}"/> registered
/// for a query.
/// </summary>
/// <remarks>
/// The dispatcher only ever sees <see cref="IQueryHandler{TQuery,TResult}"/>; it has
/// no idea whether the instance it receives is the bare handler, the logging
/// decorator, the caching decorator, or the logging+caching chain. That is exactly
/// the point of decorating via the container instead of hand-written wrappers.
/// </remarks>
public interface IQueryDispatcher
{
    /// <summary>
    /// Handles a query by resolving its handler from the container.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The query result.</returns>
    Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class QueryDispatcher(IServiceProvider serviceProvider) : IQueryDispatcher
{
    /// <inheritdoc />
    public Task<TResult> DispatchAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = serviceProvider.GetRequiredService(handlerType);

        // The handlerType is only known at runtime, hence the dynamic invocation.
        // A source generator or an explicit dispatch table removes this reflection
        // in production code; it is irrelevant for the DI/decorator demonstration.
        return (Task<TResult>)handlerType
            .GetMethod(nameof(IQueryHandler<IQuery<TResult>, TResult>.HandleAsync))!
            .Invoke(handler, [query, cancellationToken])!;
    }
}
