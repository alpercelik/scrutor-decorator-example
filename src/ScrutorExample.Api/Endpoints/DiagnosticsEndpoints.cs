using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Caching;

namespace ScrutorExample.Api.Endpoints;

/// <summary>
/// Ops/inspection endpoints that expose what the container actually built.
/// </summary>
/// <remarks>
/// Decorators hide the object graph by design, so being able to see the chain is
/// valuable when onboarding or debugging. This file is a good one to read together
/// with <c>CompositionRootExtensions</c>.
/// </remarks>
internal static class DiagnosticsEndpoints
{
    internal const string GroupName = "Diagnostics";

    /// <summary>
    /// Maps the diagnostics endpoints.
    /// </summary>
    /// <param name="endpoints">The route builder to attach to.</param>
    /// <returns>The same route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/api/diagnostics")
            .WithTags(GroupName);

        group.MapGet("/decorator-chain", GetDecoratorChain)
            .WithName("Diagnostics_DecoratorChain")
            .WithSummary("Show the decorator chain that IProductService resolves to")
            .WithDescription("Walks the inner-service references of the resolved object graph: Logging -> Caching -> ProductService.")
            .Produces<DecoratorChainResponse>();

        group.MapPost("/cache/{id:guid}/evict", EvictCacheEntry)
            .WithName("Diagnostics_EvictCacheEntry")
            .WithSummary("Evict the cached IProductService.GetAsync(id) entry")
            .WithDescription("Uses the shared ICacheKeyFactory, which proves application code and the caching decorator agree on the key.")
            .Produces<EvictionResponse>();

        return endpoints;
    }

    private static Ok<DecoratorChainResponse> GetDecoratorChain(IServiceProvider serviceProvider)
    {
        var service = serviceProvider.GetRequiredService<IProductService>();
        var chain = DescribeChain(service, typeof(IProductService));

        return TypedResults.Ok(new DecoratorChainResponse(
            typeof(IProductService).Name,
            chain,
            chain.Count <= 1
                ? "No decorators applied."
                : $"{chain.Count - 1} decorator(s) wrapping the implementation."));
    }

    /// <summary>
    /// Walks the decorator chain of a resolved service.
    /// </summary>
    /// <param name="service">The outermost resolved instance.</param>
    /// <param name="serviceType">The interface being decorated.</param>
    /// <returns>Decorators outermost-first, followed by the implementation.</returns>
    /// <remarks>
    /// Each decorator holds the next service in a field typed as the decorated
    /// interface. Only those fields are followed, and only while the instance is a
    /// decorator: the implementation itself has other dependencies (a clock, a
    /// latency setting) that are not part of the chain.
    /// </remarks>
    private static List<string> DescribeChain(object service, Type serviceType)
    {
        var chain = new List<string>();

        object? current = service;
        while (current is not null)
        {
            chain.Add(current.GetType().Name);

            // Real implementations are not named "...Decorator"; stop there.
            if (!current.GetType().Name.EndsWith("Decorator", StringComparison.Ordinal))
            {
                break;
            }

            var next = current
                .GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.FieldType == serviceType)
                .Select(field => field.GetValue(current))
                .FirstOrDefault(value => value is not null);

            current = ReferenceEquals(next, current) ? null : next;
        }

        return chain;
    }

    private static Ok<EvictionResponse> EvictCacheEntry(
        Guid id,
        IServiceProvider serviceProvider)
    {
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        var keyFactory = serviceProvider.GetRequiredService<ICacheKeyFactory>();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<CachingOptions>>().CurrentValue;

        var method = typeof(IProductService).GetMethod(nameof(IProductService.GetAsync))!;
        var key = $"{options.KeyPrefix}:{keyFactory.CreateKey(method, [id, CancellationToken.None])}";

        var existed = cache.TryGetValue(key, out _);
        cache.Remove(key);

        return TypedResults.Ok(new EvictionResponse(key, existed, "The next GET for this id is a CACHE MISS and hits ProductService again."));
    }

    /// <summary>Response body for the decorator chain endpoint.</summary>
    /// <param name="Service">The decorated abstraction.</param>
    /// <param name="Chain">Outermost decorator first, implementation last.</param>
    /// <param name="Summary">Human readable explanation.</param>
    internal sealed record DecoratorChainResponse(string Service, IReadOnlyList<string> Chain, string Summary);

    /// <summary>Response body for the eviction endpoint.</summary>
    /// <param name="Key">The cache key that was removed.</param>
    /// <param name="Existed">Whether an entry was present.</param>
    /// <param name="NextStep">What to expect on the next request.</param>
    internal sealed record EvictionResponse(string Key, bool Existed, string NextStep);
}
