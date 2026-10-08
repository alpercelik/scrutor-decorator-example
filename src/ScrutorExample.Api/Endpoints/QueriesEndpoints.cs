using Microsoft.AspNetCore.Http.HttpResults;
using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Entities;
using ScrutorExample.Core.Queries;

namespace ScrutorExample.Api.Endpoints;

/// <summary>
/// Endpoints that go through the open generic
/// <see cref="IQueryHandler{TQuery,TResult}"/> pipeline instead of calling a service
/// directly.
/// </summary>
/// <remarks>
/// <see cref="GetProductsQuery"/> is annotated with <c>[CacheableQuery]</c> and
/// <see cref="GetProductByIdQuery"/> is not, which makes the two routes a compact
/// demonstration of a per-request-type decision taken inside one generic decorator.
/// </remarks>
internal static class QueriesEndpoints
{
    internal const string GroupName = "Queries";

    /// <summary>
    /// Maps the query pipeline endpoints.
    /// </summary>
    /// <param name="endpoints">The route builder to attach to.</param>
    /// <returns>The same route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapQueriesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/api/queries")
            .WithTags(GroupName);

        group.MapGet("/products", GetProductsAsync)
            .WithName("Queries_GetProducts")
            .WithSummary("List products through the IQueryHandler<,> pipeline")
            .WithDescription("GetProductsQuery carries [CacheableQuery], so CachingQueryHandlerDecorator answers repeat calls.")
            .Produces<IReadOnlyList<Product>>();

        group.MapGet("/products/{id:guid}", GetProductByIdAsync)
            .WithName("Queries_GetProductById")
            .WithSummary("Get one product through the IQueryHandler<,> pipeline")
            .WithDescription("GetProductByIdQuery is not marked cacheable, so only the logging decorator does anything.")
            .Produces<Product>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<Product>>> GetProductsAsync(
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await dispatcher.DispatchAsync(new GetProductsQuery(), cancellationToken));

    private static async Task<Results<Ok<Product>, NotFound>> GetProductByIdAsync(
        Guid id,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var product = await dispatcher.DispatchAsync(new GetProductByIdQuery(id), cancellationToken);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }
}
