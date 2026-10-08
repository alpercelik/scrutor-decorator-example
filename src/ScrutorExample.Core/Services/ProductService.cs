using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Entities;

namespace ScrutorExample.Core.Services;

/// <summary>
/// The real implementation. Registered by Scrutor assembly scanning (it is
/// discovered through <see cref="IProductService"/>, not by name).
/// </summary>
/// <remarks>
/// Notice what this class does <b>not</b> contain: no <c>_logger.LogInformation</c>
/// calls, no <c>IMemoryCache</c> lookups. Cross-cutting concerns are attached by
/// the decorators during composition.
/// </remarks>
public sealed class ProductService(SimulatedLatency latency, TimeProvider timeProvider) : IProductService
{
    private static readonly Guid WidgetId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GadgetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GizmoId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly List<Product> _products =
    [
        new(WidgetId, "SKU-001", "Widget", 9.99m),
        new(GadgetId, "SKU-002", "Gadget", 24.50m),
        new(GizmoId, "SKU-003", "Gizmo", 149.00m),
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(latency.Duration, timeProvider, cancellationToken);

        // A fresh snapshot each time, so callers can tell (via RetrievedAt) whether
        // they received a cached instance or a newly materialised one.
        return _products
            .Select(product => product with { RetrievedAt = timeProvider.GetUtcNow() })
            .OrderBy(product => product.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await Task.Delay(latency.Duration, timeProvider, cancellationToken);

        var product = _products.FirstOrDefault(candidate => candidate.Id == id);
        return product is null ? null : product with { RetrievedAt = timeProvider.GetUtcNow() };
    }

    /// <inheritdoc />
    public async Task<decimal?> GetPriceWithVatAsync(Guid id, decimal vatRate, CancellationToken cancellationToken = default)
    {
        var product = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        return product is null ? null : decimal.Round(product.Price * (1 + vatRate), 2);
    }
}
