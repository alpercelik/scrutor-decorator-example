using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScrutorExample.Core.Abstractions;
using ScrutorExample.Core.Caching;
using ScrutorExample.Core.Composition;
using ScrutorExample.Core.Entities;
using ScrutorExample.Core.Queries;

// =============================================================================
//  Console sample
//  --------------
//  Uses the exact same Scrutor composition as the ASP.NET Core app:
//
//      IProductService -> Logging( Caching( ProductService ) )
//
//  and then proves the decorators are really in the chain by repeating calls and
//  comparing timings and RetrievedAt timestamps.
// =============================================================================

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Keep the host's own chatter down so the decorator output stands out.
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Caching:DefaultExpiration"] = "00:00:30",
    ["Caching:Enabled"] = "true",
});

// The one line that wires assembly scanning + decoration for both applications.
builder.Services.AddProductCatalogue(builder.Configuration);

using var host = builder.Build();

// Fail fast when a scan filter or decorator registration is wrong.
host.Services.VerifyRegistrations();

var products = host.Services.GetRequiredService<IProductService>();
var dispatcher = host.Services.GetRequiredService<IQueryDispatcher>();
var cacheOptions = host.Services.GetRequiredService<IOptionsMonitor<CachingOptions>>().CurrentValue;

Section("1. Scrutor assembly scanning + decoration");
Console.WriteLine($"""
    IProductService resolves to : {Describe(products)}
    Registered as               : singleton (see CompositionRootExtensions.cs)
    Cache key prefix            : {cacheOptions.KeyPrefix}
    Default expiration          : {cacheOptions.DefaultExpiration}

    """);

Section("2. First call -> CACHE MISS, the inner service runs (250 ms simulates a database)");
await CallAsync("GetAllAsync()", () => products.GetAllAsync());

Section("3. Second call -> CACHE HIT, the inner service is bypassed");
await CallAsync("GetAllAsync()", () => products.GetAllAsync());

Section("4. Selective caching: only [Cacheable] members are intercepted");
var first = (await products.GetAllAsync())[0];
await CallAsync($"GetAsync({Short(first.Id)})", () => products.GetAsync(first.Id));
await CallAsync($"GetAsync({Short(first.Id)}) again", () => products.GetAsync(first.Id));
await CallAsync($"GetPriceWithVatAsync({Short(first.Id)}, 0.21)  (no attribute)", () => products.GetPriceWithVatAsync(first.Id, 0.21m));
await CallAsync($"GetPriceWithVatAsync({Short(first.Id)}, 0.21)  again", () => products.GetPriceWithVatAsync(first.Id, 0.21m));

Section("5. Open generic decoration: each IQueryHandler<,> is wrapped by one registration");
await CallAsync("GetProductsQuery       [CacheableQuery]", () => dispatcher.DispatchAsync(new GetProductsQuery()));
await CallAsync("GetProductsQuery again [CacheableQuery]", () => dispatcher.DispatchAsync(new GetProductsQuery()));
await CallAsync("GetProductByIdQuery    [not cacheable]", () => dispatcher.DispatchAsync(new GetProductByIdQuery(first.Id)));
await CallAsync("GetProductByIdQuery again", () => dispatcher.DispatchAsync(new GetProductByIdQuery(first.Id)));

Section("6. Eviction uses the very same ICacheKeyFactory the decorator used");
var keyFactory = host.Services.GetRequiredService<ICacheKeyFactory>();
var cache = host.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
var getMethod = typeof(IProductService).GetMethod(nameof(IProductService.GetAsync))!;
var key = $"{cacheOptions.KeyPrefix}:{keyFactory.CreateKey(getMethod, [first.Id, CancellationToken.None])}";
cache.Remove(key);
Console.WriteLine($"   Evicted '{key}'");
await CallAsync("GetAsync(...) after eviction", () => products.GetAsync(first.Id));

Section("Done");
Console.WriteLine("""
    Every "CACHE HIT" above was answered by the caching decorator without touching
    ProductService. The nesting order is proven by the log output:

        → IProductService.GetAllAsync   (logging decorator, outermost)
        CACHE MISS  scrutor-example:iproductservice.getallasync()   (caching decorator)
        ← IProductService.GetAllAsync completed in 251 ms
    """);

return 0;

static string Short(Guid id) => id.ToString()[..8];

static string Describe(object service)
{
    var type = service.GetType();
    return type.IsGenericType
        ? $"{type.Name}<{string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name))}>"
        : type.Name;
}

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(78, '─'));
}

static async Task CallAsync<T>(string label, Func<Task<T>> call)
{
    var stopwatch = Stopwatch.StartNew();
    var result = await call();
    stopwatch.Stop();

    Console.WriteLine($"   {label,-54} {stopwatch.ElapsedMilliseconds,5} ms  -> {Summarize(result)}");
}

static string Summarize(object? result) => result switch
{
    null => "<null>",
    IReadOnlyList<Product> list => $"{list.Count} products, first retrieved at {list[0].RetrievedAt:HH:mm:ss.fff}",
    Product product => $"{product.Name} (retrieved {product.RetrievedAt:HH:mm:ss.fff})",
    decimal money => money.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
    _ => result.ToString() ?? "<null>",
};
