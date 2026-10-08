using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Entities;

namespace ScrutorExample.Core.Queries;

/// <summary>
/// Query: return every product.
/// </summary>
/// <remarks>
/// Annotated with <see cref="CacheableQueryAttribute"/> so the open generic
/// caching decorator will cache this specific request type.
/// </remarks>
[CacheableQuery(Seconds = 60)]
public sealed record GetProductsQuery : IQuery<IReadOnlyList<Product>>;

/// <summary>
/// Handles <see cref="GetProductsQuery"/>.
/// </summary>
public sealed class GetProductsQueryHandler(IProductService products)
    : IQueryHandler<GetProductsQuery, IReadOnlyList<Product>>
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Product>> HandleAsync(
        GetProductsQuery query,
        CancellationToken cancellationToken = default)
        => products.GetAllAsync(cancellationToken);
}

/// <summary>
/// Query: return a single product by id.
/// </summary>
/// <remarks>
/// Deliberately <b>not</b> annotated as cacheable, to show that the generic
/// caching decorator only intercepts request types it is told to intercept.
/// </remarks>
public sealed record GetProductByIdQuery(Guid Id) : IQuery<Product?>;

/// <summary>
/// Handles <see cref="GetProductByIdQuery"/>.
/// </summary>
public sealed class GetProductByIdQueryHandler(IProductService products)
    : IQueryHandler<GetProductByIdQuery, Product?>
{
    /// <inheritdoc />
    public Task<Product?> HandleAsync(
        GetProductByIdQuery query,
        CancellationToken cancellationToken = default)
        => products.GetAsync(query.Id, cancellationToken);
}
