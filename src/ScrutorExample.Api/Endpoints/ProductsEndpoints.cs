using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Entities;

namespace ScrutorExample.Api.Endpoints;

/// <summary>
/// Product endpoints, grouped with <c>MapGroup</c> so the route prefix, tag and
/// OpenAPI metadata are declared once.
/// </summary>
/// <remarks>
/// The endpoints depend on <see cref="IProductService"/> only. They have no idea
/// that the instance they receive is a decorator chain — that is the whole point.
/// </remarks>
internal static class ProductsEndpoints
{
    internal const string GroupName = "Products";

    /// <summary>
    /// Maps every product endpoint.
    /// </summary>
    /// <param name="endpoints">The route builder to attach to.</param>
    /// <returns>The same route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup("/api/products")
            .WithTags(GroupName);

        group.MapGet("/", GetAllAsync)
            .WithName("Products_GetAll")
            .WithSummary("List every product")
            .WithDescription("Annotated with [Cacheable(60)] on IProductService, so the second call is served by the caching decorator.")
            .Produces<IReadOnlyList<Product>>();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("Products_GetById")
            .WithSummary("Get a single product")
            .WithDescription("Annotated with [Cacheable(30)]. Call it twice with the same id and watch 'CACHE HIT' appear in the log.")
            .Produces<Product>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/price", GetPriceAsync)
            .WithName("Products_GetPrice")
            .WithSummary("Get the gross price")
            .WithDescription("Deliberately NOT annotated with [Cacheable]: the caching decorator forwards this call every time.")
            .Produces<decimal>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<Product>>> GetAllAsync(
        IProductService products,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await products.GetAllAsync(cancellationToken));

    private static async Task<Results<Ok<Product>, NotFound>> GetByIdAsync(
        Guid id,
        IProductService products,
        CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    private static async Task<Results<Ok<decimal>, NotFound>> GetPriceAsync(
        Guid id,
        [FromQuery] decimal vatRate,
        IProductService products,
        CancellationToken cancellationToken)
    {
        var price = await products.GetPriceWithVatAsync(id, vatRate, cancellationToken);
        return price is null ? TypedResults.NotFound() : TypedResults.Ok(price.Value);
    }
}
