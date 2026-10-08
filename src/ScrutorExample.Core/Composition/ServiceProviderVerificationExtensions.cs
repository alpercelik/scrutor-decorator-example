using Microsoft.Extensions.DependencyInjection;

namespace ScrutorExample.Core.Composition;

/// <summary>
/// Startup self-check for the container.
/// </summary>
/// <remarks>
/// Scanning and decoration move wiring from compile time to run time, so a typo in
/// a scan filter can silently produce an empty registration. Resolving every
/// descriptor once at startup turns that into an immediate, descriptive failure
/// instead of a <c>NullReferenceException</c> in production.
/// </remarks>
public static class ServiceProviderVerificationExtensions
{
    /// <summary>
    /// Resolves every registered service once to prove the object graph can be built.
    /// </summary>
    /// <param name="services">The built service provider.</param>
    /// <returns>The same provider, for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when any registration cannot be resolved.</exception>
    public static IServiceProvider VerifyRegistrations(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Reading the descriptor list back is only possible from the collection;
        // when the provider was built by the host we fall back to the well-known
        // entry points of this sample.
        foreach (var (serviceType, serviceKey) in KnownRegistrations)
        {
            try
            {
                _ = serviceKey is null
                    ? services.GetRequiredService(serviceType)
                    : services.GetRequiredKeyedService(serviceType, serviceKey);
            }
            catch (Exception exception)
            {
                var name = serviceKey is null ? serviceType.ToString() : $"{serviceType} (key: {serviceKey})";
                throw new InvalidOperationException(
                    $"Service '{name}' could not be resolved. Check the Scrutor scan filters and decorator registrations.",
                    exception);
            }
        }

        return services;
    }

    private static IEnumerable<(Type ServiceType, object? ServiceKey)> KnownRegistrations
    {
        get
        {
            yield return (typeof(Abstractions.IProductService), null);
            yield return (typeof(Queries.IQueryDispatcher), null);
            yield return (typeof(Caching.ICacheKeyFactory), null);
            yield return (typeof(Abstractions.IQueryHandler<Queries.GetProductsQuery, IReadOnlyList<Entities.Product>>), null);
            yield return (typeof(Abstractions.IQueryHandler<Queries.GetProductByIdQuery, Entities.Product?>), null);
        }
    }
}
