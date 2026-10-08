using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScrutorExample.Core;
using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Caching;
using ScrutorExample.Core.Decorators;
using ScrutorExample.Core.Queries;
using ScrutorExample.Core.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The composition root. Both the ASP.NET Core app and the console app call
/// <see cref="AddProductCatalogue"/> so they are guaranteed to be wired identically.
/// </summary>
/// <remarks>
/// This file intentionally lives in the <c>Microsoft.Extensions.DependencyInjection</c>
/// namespace so the registration reads as a natural part of the container API:
/// <c>builder.Services.AddProductCatalogue(builder.Configuration)</c>.
/// </remarks>
public static class CompositionRootExtensions
{
    /// <summary>
    /// Namespace that holds the decorators. Excluded from scanning because the
    /// decorators implement the very interfaces they decorate.
    /// </summary>
    private const string DecoratorsNamespace = "ScrutorExample.Core.Decorators";

    /// <summary>
    /// Registers the product catalogue: implementations are discovered by assembly
    /// scanning and then wrapped in logging and caching decorators, in that order.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">Application configuration, used for cache settings.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddProductCatalogue(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ---------------------------------------------------------------------
        // 1. Infrastructure the decorators themselves depend on.
        // ---------------------------------------------------------------------
        services.AddLogging();

        // IMemoryCache + IOptionsMonitor<T> (bind from "Caching" when configuration
        // was supplied, otherwise fall back to the defaults on CachingOptions).
        services.AddMemoryCache();
        if (configuration is not null)
        {
            services.Configure<CachingOptions>(configuration.GetSection(CachingOptions.SectionName));
        }
        else
        {
            services.AddOptions<CachingOptions>();
        }

        // The key factory is shared between the decorator that writes cache entries
        // and any application code that evicts them.
        services.TryAddSingleton<ICacheKeyFactory, DefaultCacheKeyFactory>();

        // Deterministic clock + simulated "database" latency, so the console demo
        // can use a fake TimeProvider in tests later on.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(SimulatedLatency.Default);

        // ---------------------------------------------------------------------
        // 2. Assembly scanning: register implementations without naming them.
        //
        //    IMPORTANT: the decorators themselves implement IProductService /
        //    IQueryHandler<,>, so a naive AssignableTo filter would register them
        //    as implementations *and* decorate them, producing an unbounded
        //    Logging -> Caching -> Logging -> ... cycle. The
        //    NotInNamespaces(...Decorators) filter is what keeps the scan limited
        //    to real implementations.
        // ---------------------------------------------------------------------
        services.Scan(scan => scan
            // Scan the assembly that contains ICoreAssemblyMarker.
            .FromAssemblyOf<ICoreAssemblyMarker>()

            // -- IProductService implementations ---------------------------------
            .AddClasses(classes => classes
                .AssignableTo<IProductService>()
                .NotInNamespaces(DecoratorsNamespace))
                .As<IProductService>()
                .WithSingletonLifetime()

            // -- Query handlers (open generic, one registration covers them all) --
            .AddClasses(classes => classes
                .AssignableTo(typeof(IQueryHandler<,>))
                .NotInNamespaces(DecoratorsNamespace))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        // ---------------------------------------------------------------------
        // 3. Decoration. Order matters: the LAST Decorate call ends up OUTERMOST.
        //
        //    services.Decorate(IProductService, Caching...)  -> inner
        //    services.Decorate(IProductService, Logging...)  -> outer
        //
        //    Resolved graph: Logging(Caching(ProductService))
        //
        //    So a log line "→ IProductService.GetAsync" is always followed by either
        //    "CACHE HIT" or "CACHE MISS", which is exactly what the samples show.
        // ---------------------------------------------------------------------
        services.Decorate<IProductService, CachingProductServiceDecorator>();
        services.Decorate<IProductService, LoggingProductServiceDecorator>();

        // Open generic decoration: cache the handlers whose query type asks for it,
        // then log every handler.
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(CachingQueryHandlerDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryHandlerDecorator<,>));

        // ---------------------------------------------------------------------
        // 4. Hand-written registrations that are not part of the scan.
        //    The dispatcher is the only one; everything else is discovered or
        //    decorated above.
        // ---------------------------------------------------------------------
        services.TryAddScoped<IQueryDispatcher, QueryDispatcher>();

        return services;
    }
}
