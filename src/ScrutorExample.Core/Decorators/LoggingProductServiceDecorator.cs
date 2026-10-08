using Microsoft.Extensions.Logging;
using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Entities;

namespace ScrutorExample.Core.Decorators;

/// <summary>
/// Logging decorator for <see cref="IProductService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Hand-written decorators are verbose but completely transparent: the compiler
/// checks every signature, there is no dynamic proxy on the call path, and step
/// debugging just works. For a handful of services this is usually a better
/// trade-off than an interception framework.
/// </para>
/// <para>
/// All Scrutor needs is a class that implements the interface and takes that same
/// interface as its first constructor dependency.
/// </para>
/// </remarks>
/// <param name="inner">The cached/real service being wrapped.</param>
/// <param name="logger">Logger for this category.</param>
public sealed class LoggingProductServiceDecorator(
    IProductService inner,
    ILogger<LoggingProductServiceDecorator> logger) : IProductService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        => DecoratorLog.RunAsync(
            logger,
            typeof(IProductService),
            [cancellationToken],
            () => inner.GetAllAsync(cancellationToken));

    /// <inheritdoc />
    public Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => DecoratorLog.RunAsync(
            logger,
            typeof(IProductService),
            [id, cancellationToken],
            () => inner.GetAsync(id, cancellationToken));

    /// <inheritdoc />
    public Task<decimal?> GetPriceWithVatAsync(Guid id, decimal vatRate, CancellationToken cancellationToken = default)
        => DecoratorLog.RunAsync(
            logger,
            typeof(IProductService),
            [id, vatRate, cancellationToken],
            () => inner.GetPriceWithVatAsync(id, vatRate, cancellationToken));
}
