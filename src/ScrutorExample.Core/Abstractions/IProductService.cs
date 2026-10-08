using ScrutorExample.Core.Entities;

namespace ScrutorExample.Core.Abstractions;

/// <summary>
/// Read access to the product catalogue.
/// </summary>
/// <remarks>
/// This is the "decorated" abstraction. Nothing in this interface knows about
/// logging or caching — those concerns are attached from the outside by
/// <c>ServiceCollectionExtensions</c> using Scrutor's <c>Decorate</c>.
/// </remarks>
public interface IProductService
{
    /// <summary>
    /// Gets every product in the catalogue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All products, ordered by name.</returns>
    [Cacheable(Seconds = 60)]
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single product.
    /// </summary>
    /// <param name="id">The product identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The product, or <see langword="null"/> when it does not exist.</returns>
    [Cacheable(Seconds = 30)]
    Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the current price including VAT.
    /// </summary>
    /// <param name="id">The product identifier.</param>
    /// <param name="vatRate">VAT rate, e.g. <c>0.21m</c> for 21%.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The gross price, or <see langword="null"/> when the product does not exist.</returns>
    /// <remarks>
    /// Intentionally <b>not</b> annotated with <see cref="CacheableAttribute"/>:
    /// this method has two parameters, which the sample's key factory does not
    /// support. It demonstrates that selective decoration works — the cache
    /// decorator simply forwards the call.
    /// </remarks>
    Task<decimal?> GetPriceWithVatAsync(Guid id, decimal vatRate, CancellationToken cancellationToken = default);
}
