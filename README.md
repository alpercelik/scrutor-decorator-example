# Scrutor example — assembly scanning + decorators (logging, caching)

A small, complete .NET 10 solution that shows how to use [Scrutor](https://github.com/khellang/Scrutor) to

1. **scan an assembly** and register services by convention, and
2. **decorate the registered implementations** to add cross-cutting concerns (logging, caching) without touching the implementations.

The exact same composition root is consumed by **two applications** (the API and the console app), and
an Aspire AppHost then orchestrates the API on top — so you see the wiring once, consumed twice, and
supervised once:

| Project | Kind | What it shows |
| --- | --- | --- |
| `src/ScrutorExample.Core` | `classlib` | Interfaces, implementations, decorators, and the composition root |
| `src/ScrutorExample.Api` | `Microsoft.NET.Sdk.Web` | Minimal APIs with endpoints structured per feature folder |
| `src/ScrutorExample.Console` | `Exe` | Generic host + the same container, printed as a guided walkthrough |
| `ScrutorExample.AppHost` | `Aspire.AppHost.Sdk` | Aspire orchestration for the API (dashboard, endpoints, telemetry) |

---

## Quick start

```bash
# whole solution -> artifacts/, not per-project bin/obj (see Artifacts output layout)
dotnet build ScrutorExample.slnx

# 1) guided walkthrough of the decorator chain (best starting point)
dotnet run --project src/ScrutorExample.Console

# 2) minimal API — Scalar API reference at /scalar/v1, OpenAPI at /openapi/v1.json
dotnet run --project src/ScrutorExample.Api

# 3) everything under Aspire, with dashboard + telemetry (see section 0)
aspire start
```

Requires the [Aspire CLI](https://aspire.dev) for option 3 only
(`dotnet tool install -g Aspire.Cli`); options 1 and 2 need just the .NET 10 SDK.

With the API running, open **<http://localhost:5251/scalar/v1>** for the interactive API
reference (both are Development-only; `/` redirects to it; under Aspire the port is
Aspire-assigned — use the dashboard link), or drive it with curl:

```bash
BASE=http://localhost:5251          # or whatever the launch profile prints
ID=22222222-2222-2222-2222-222222222222

# cacheable ([Cacheable(30)]) -> 2nd call is a CACHE HIT
curl -s -o /dev/null -w "%{time_total}s\n" $BASE/api/products/$ID
curl -s -o /dev/null -w "%{time_total}s\n" $BASE/api/products/$ID

# NOT cacheable -> every call hits the implementation (~250 ms)
curl -s -o /dev/null -w "%{time_total}s\n" "$BASE/api/products/$ID/price?vatRate=0.21"
curl -s -o /dev/null -w "%{time_total}s\n" "$BASE/api/products/$ID/price?vatRate=0.21"

# open generic handler pipeline
curl -s $BASE/api/queries/products

# inspect what the container actually built
curl -s $BASE/api/diagnostics/decorator-chain
curl -s -X POST $BASE/api/diagnostics/cache/$ID/evict
```

`/api/diagnostics/decorator-chain` returns:

```json
{
  "service": "IProductService",
  "chain": ["LoggingProductServiceDecorator", "CachingProductServiceDecorator", "ProductService"],
  "summary": "2 decorator(s) wrapping the implementation."
}
```

---

## Repository layout

```
scrutor-example/
├── Directory.Build.props          # shared MSBuild settings + UseArtifactsOutput (see below)
├── Directory.Build.targets        # CPM001 guard: rejects a local Version="..." on a PackageReference
├── Directory.Packages.props       # CENTRAL PACKAGE MANAGEMENT: every version, declared once
├── global.json                    # pins the .NET 10 SDK + the Aspire MSBuild SDK
├── ScrutorExample.slnx            # solution (XML solution format)
├── LICENSE                        # MIT
├── README.md                      # this file
├── .editorconfig                  # code style, also enforced during build
├── .gitattributes                 # normalises line endings to LF on commit
├── .gitignore                     # ignores artifacts/ and IDE/OS noise
├── artifacts/                     # ALL build output (git-ignored)
├── ScrutorExample.AppHost/        # Aspire orchestration
│   ├── AppHost.cs                             # the resource graph
│   ├── aspire.config.json                     # tells the Aspire CLI which AppHost to use
│   └── ScrutorExample.AppHost.csproj
└── src/
    ├── ScrutorExample.Core/
    │   ├── ICoreAssemblyMarker.cs           # anchor type for the scan
    │   ├── Abstractions/                    # IProductService, IQueryHandler<,>, [Cacheable], [CacheableQuery]
    │   ├── Caching/                         # CachingOptions, ICacheKeyFactory + default implementation
    │   ├── Entities/Product.cs
    │   ├── Queries/                         # two queries, two handlers, IQueryDispatcher
    │   ├── Services/ProductService.cs       # the real implementation: no logging, no caching
    │   ├── Decorators/                      # the four decorators (see below)
    │   └── Composition/                     # composition root + startup verification
    ├── ScrutorExample.Api/
    │   ├── Program.cs                       # pipeline only, no route definitions
    │   └── Endpoints/                       # ProductsEndpoints, QueriesEndpoints, DiagnosticsEndpoints
    └── ScrutorExample.Console/
        └── Program.cs                       # guided walkthrough with timings and log excerpt
```

---

## Artifacts output layout

There is no `bin/` or `obj/` inside any project. Setting `UseArtifactsOutput` (a .NET 8+ feature)
gathers every project's output into one predictable tree next to `Directory.Build.props`:

```xml
<PropertyGroup>
  <UseArtifactsOutput>true</UseArtifactsOutput>
  <ArtifactsPath>$(MSBuildThisFileDirectory)artifacts</ArtifactsPath>
</PropertyGroup>
```

```
artifacts/
├── bin/<Project>/<pivot>/        # assemblies + deps.json + runtimeconfig.json
├── obj/<Project>/<pivot>/        # intermediate and generated files
├── publish/<Project>/<pivot>/    # dotnet publish output
└── package/<pivot>/              # .nupkg output
```

`<pivot>` is the configuration, plus TFM and RID when they apply — `debug`, `release`,
`release_linux-x64`. Because the TFM is single for this solution, the pivots stay simply `debug`
and `release`:

```
artifacts/bin/ScrutorExample.Console/debug/ScrutorExample.Console.dll
artifacts/publish/ScrutorExample.Console/release/ScrutorExample.Console.dll
```

Worth knowing:

* `ArtifactsPath` resolves against `$(MSBuildThisFileDirectory)`, so this **must** stay the
  repository-root `Directory.Build.props`. A nested copy would move the artifacts root down with it.
* Tools that look for `bin/` per project need pointing at `artifacts/bin/...` instead. `dotnet build`,
  `run`, `publish`, `test` and `clean` are all unaffected.
* The old per-project `bin/`/`obj/` folders are inert once this is on; delete them if you switch an
  existing repo over.

---

## The three files that pin versions and build settings

**`Directory.Build.props`** — applies to the projects under `src/` without being referenced:

```xml
<TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<AnalysisLevel>latest-recommended</AnalysisLevel>
```

> **One deliberate exception:** `Directory.Build.props` is only picked up by a project if it sits in
> that project's folder or an ancestor. The Aspire AppHost lives in `ScrutorExample.AppHost/`, which is
> *not* under the repository root, so it does not inherit the file and sets `TargetFramework`,
> `Nullable` and `ImplicitUsings` itself. It still gets `Directory.Packages.props` and
> `Directory.Build.targets` (MSBuild searches upward for those **separately**) and it still lands its
> output in the shared `artifacts/` tree, because those two output properties are global. Moving the
> AppHost under `src/` would make it inherit the shared settings at the cost of deviating from the
> layout `aspire init` generates.

**`Directory.Packages.props`** — enables Central Package Management, so `.csproj` files reference
packages **without** a `Version`:

```xml
<PackageReference Include="Scrutor" />          <!-- version lives in Directory.Packages.props -->
```

Adding a package means one `PackageVersion` line in `Directory.Packages.props` and one
`PackageReference` (no version) in the project. CPM's own validation (`NU1010`) catches a missing
`PackageVersion`; `Directory.Build.targets` closes the other gap by failing with `CPM001` if a
project sneaks in a local `Version="..."` attribute:

```
error CPM001: PackageReference(s) with a local Version: Scrutor 7.0.0.
All versions must be declared once in Directory.Packages.props (Central Package Management).
```

> **Version pins move as a set.** The `Microsoft.Extensions.*` packages are pinned to `10.0.12`.
> That floor is not arbitrary: `Aspire.Hosting.AppHost 13.6.1` requires `Microsoft.Extensions.*`
> `>= 10.0.12`, and with `CentralPackageTransitivePinningEnabled` an older pin fails the build with
> `NU1109: Detected package downgrade`. Bump that group together when you upgrade Aspire.

**`global.json`** — versions the .NET SDK *and* the MSBuild SDKs. An `<Project Sdk="...">` attribute
is resolved before restore runs, so its version cannot come from `Directory.Packages.props`; the
`msbuild-sdks` section is where it belongs:

```json
{
  "sdk": { "version": "10.0.400", "rollForward": "latestPatch" },
  "msbuild-sdks": { "Aspire.AppHost.Sdk": "13.6.1" }
}
```

The AppHost therefore declares `<Project Sdk="Aspire.AppHost.Sdk">` with no version, and upgrading
Aspire is a one-line edit in `global.json`. NuGet packages (`Directory.Packages.props`) and MSBuild
SDKs (`global.json`) are the only two places a version can live in this repo.

### Checking for updates

```bash
dotnet list package --outdated            # per project; packages are centrally pinned
dotnet list package --outdated --include-transitive
aspire update                             # Aspire integrations/AppHost packages
aspire update --self                      # the Aspire CLI itself
```

At the time of writing every pinned version is the newest stable release compatible with .NET 10:
Scrutor 7.0.0, `Microsoft.Extensions.*` and `Microsoft.AspNetCore.OpenApi` 10.0.12,
`Scalar.AspNetCore` 2.17.14, and the Aspire SDK/`Aspire.Hosting.AppHost` 13.6.1. `11.0.x`
`Microsoft.Extensions.*` / `Microsoft.AspNetCore.OpenApi` builds exist on nuget.org but are
**prerelease only** (and target .NET 11), so they are deliberately not taken while this solution
stays on `net10.0`.

---

## 0. Aspire orchestration

[`ScrutorExample.AppHost`](ScrutorExample.AppHost/AppHost.cs) models the runnable service of this
solution so you get one command, a dashboard with live logs/traces/metrics, and Aspire-managed ports:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject("api", "../src/ScrutorExample.Api/ScrutorExample.Api.csproj")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/");

builder.Build().Run();
```

```bash
aspire start                 # start the AppHost (the dashboard URL is printed)
aspire wait api              # block until the API is healthy
aspire describe              # inspect the resource graph, endpoints, health
aspire stop                  # stop everything
```

Control-plane commands are `aspire`, never `dotnet run` on the AppHost — the CLI owns the lifecycle,
ports, and the dashboard.

Two decisions behind this graph:

* **The console app is deliberately not a resource.** It is a scripted walkthrough that prints its
  demo and exits after a few seconds; the dashboard would only ever show it as a short-lived
  `Exited` resource. Run it directly with `dotnet run --project src/ScrutorExample.Console`.
* **The API keeps its existing composition.** Aspire's `AddProject` injects ports and the
  `OTEL_EXPORTER_OTLP_*` environment variables (verified in `aspire describe`: `OTEL_SERVICE_NAME=api`,
  `ASPNETCORE_ENVIRONMENT=Development`, random `ASPNETCORE_URLS`). The Decorator chain, Scalar UI and
  cache config all behave exactly as they do standalone — no ServiceDefaults project was added, so
  none of the sample code changed.

---

## 1. Assembly scanning

`CompositionRootExtensions.AddProductCatalogue` is the single place that knows how the object graph
is built, and both applications call it. (The Aspire AppHost does not call it — it references the API
project by path and hands it ports and telemetry; see section 0.)

The registrations it performs:

```csharp
services.Scan(scan => scan
    .FromAssemblyOf<ICoreAssemblyMarker>()            // scan the Core assembly

    .AddClasses(classes => classes
        .AssignableTo<IProductService>()
        .NotInNamespaces(DecoratorsNamespace))        // see the warning below
        .As<IProductService>()
        .WithSingletonLifetime()

    .AddClasses(classes => classes
        .AssignableTo(typeof(IQueryHandler<,>))
        .NotInNamespaces(DecoratorsNamespace))
        .AsImplementedInterfaces()
        .WithScopedLifetime());
```

Adding a new `IProductService` or a new `IQueryHandler<,>` is now a zero-registration change: create
the class, and the scan picks it up.

> **The one gotcha that will bite you.** Decorators implement the interface they decorate. Without
> `.NotInNamespaces("...Decorators")` the scan finds `CachingProductServiceDecorator` and
> `LoggingProductServiceDecorator` as `IProductService` implementations, registers them *and* then
> decorates them — an unbounded `Logging → Caching → Logging → …` chain that hangs the first
> resolution. Alternatives: move decorators into a separate assembly, mark implementations with a
> marker interface, or use a dedicated `[Decorator]` attribute as the filter.

## 2. Decoration

```csharp
// Order matters: the LAST Decorate call becomes the OUTERMOST decorator.
services.Decorate<IProductService, CachingProductServiceDecorator>();   // inner
services.Decorate<IProductService, LoggingProductServiceDecorator>();   // outer
// resolved: Logging( Caching( ProductService ) )

// Open generics work the same way: one line decorates every handler in the app.
services.TryDecorate(typeof(IQueryHandler<,>), typeof(CachingQueryHandlerDecorator<,>));
services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryHandlerDecorator<,>));
```

`ProductService` contains no `ILogger`, no `IMemoryCache`, no `try/catch`, and no timers:

```csharp
public sealed class ProductService(SimulatedLatency latency, TimeProvider timeProvider) : IProductService
{
    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(latency.Duration, timeProvider, cancellationToken);   // fake database
        ...
    }
}
```

### The decorators

| Decorator | Worry it owns | Key detail |
| --- | --- | --- |
| `LoggingProductServiceDecorator` | Structured entry/exit/error logs + timings | Uses `[LoggerMessage]` source-generated logging; rethrows, never swallows |
| `CachingProductServiceDecorator` | `IMemoryCache` population | Only intercepts members marked `[Cacheable]`; forwards the rest |
| `LoggingQueryHandlerDecorator<TQuery,TResult>` | Same for the generic pipeline | One registration covers every handler |
| `CachingQueryHandlerDecorator<TQuery,TResult>` | Same for the generic pipeline | Reads `[CacheableQuery]` from the **closed** `TQuery`, so it can decide per request type |

Two details worth copying:

**Selective caching.** The caching decorator finds the interface `MethodInfo` it implements and reads
the attribute from it, so `[Cacheable]` stays on the *contract*:

```csharp
[Cacheable(Seconds = 30)]
Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken = default);

Task<decimal?> GetPriceWithVatAsync(Guid id, decimal vatRate, CancellationToken cancellationToken = default); // never cached
```

**Shared cache keys.** Writing an entry in the decorator and evicting it elsewhere must use the same
key, so the key builder is a service, not a private method:

```csharp
public interface ICacheKeyFactory
{
    string CreateKey(MethodInfo method, object?[] arguments);
}
```

`/api/diagnostics/cache/{id}/evict` uses `ICacheKeyFactory` to compute the key that the decorator
stored under — and lands on exactly the same string
(`scrutor-example:iproductservice.getasync(2222…)`). `CancellationToken` is excluded from keys
because it is not part of request identity.

Lifetimes are **not** duplicated in the decorator registrations: Scrutor propagates the lifetime of
the decorated registration, so a singleton service keeps exactly one decorator instance.

## 3. Structured endpoints (minimal APIs)

`Program.cs` owns the pipeline; each feature folder owns its routes:

```csharp
app.MapProductsEndpoints();     // /api/products
app.MapQueriesEndpoints();      // /api/queries
app.MapDiagnosticsEndpoints();  // /api/diagnostics
```

Each `Map…Endpoints` extension uses `MapGroup` once, then `TypedResults` and `Results<…>` so the
handlers stay strongly typed and the OpenAPI document carries real response types:

```csharp
var group = endpoints.MapGroup("/api/products").WithTags(GroupName);

group.MapGet("/{id:guid}", GetByIdAsync)
     .WithName("Products_GetById")
     .Produces<Product>()
     .ProducesProblem(StatusCodes.Status404NotFound);

private static async Task<Results<Ok<Product>, NotFound>> GetByIdAsync(
    Guid id, IProductService products, CancellationToken cancellationToken)
{
    var product = await products.GetAsync(id, cancellationToken);
    return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
}
```

## 4. Scalar API reference

`Microsoft.AspNetCore.OpenApi` generates the document; [`Scalar.AspNetCore`](https://scalar.com)
renders it as an interactive reference with a built-in request client.

```csharp
app.MapOpenApi();

// The config module Scalar's client imports. It must be a real ES module
// (default export), served with a JavaScript content type.
app.MapGet("/scalar/scalar-config.js", () => Results.Text(
    """
    export default {
      sources: [
        { title: "v1", url: "/openapi/v1.json" }
      ]
    }
    """,
    "text/javascript"))
   .ExcludeFromDescription();

app.MapScalarApiReference(options => options
    .WithTitle("Scrutor example API")
    .WithTheme(ScalarTheme.BluePlanet)
    .EnableDarkMode()
    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
    // absolute path -> imported as-is, bypassing Scalar's base-path join
    .WithJavaScriptConfiguration("/scalar/scalar-config.js"));
```

* `/scalar/v1` is the UI, `/openapi/v1.json` the document, `/` redirects to the UI.
* Assets (`scalar.js`, favicon) are served from the **package**, not a CDN — verified with no external
  requests in the page, so the UI also works offline. The `CdnUrl`/`BundleUrl` options exist if you
  would rather use a CDN.
* Both endpoints are Development-only. The document describes the API surface; it should not become
  part of that surface in production.

> **Why the config module is needed.** Scalar's client resolves its document source URL *relatively*
> against the `/scalar/` base path, so the default `"openapi/v1.json"` becomes
> `/scalar/openapi/v1.json` → **404**, and the reference UI renders empty (no endpoint list). Two
> further traps on the way to fixing it:
>
> * `AddDocument(..., routePattern)` and `WithOpenApiRoutePattern(...)` do **not** help. The route
>   pattern is stored correctly (verified: `ScalarDocument.RoutePattern == "/openapi/v1.json"`), but
>   the integration ignores it when it builds the page config.
> * `WithJavaScriptConfiguration` takes a **URL**, not inline code — the client does
>   `await import(new URL(value, basePath + '/'))`. Passing inline JSON produces invalid JavaScript
>   inside a single-quoted string, which fails at *page parse* time with
>   `Uncaught SyntaxError: Invalid or unexpected token` and leaves a blank page. Passing inline
>   `export default {...}` text also fails: it becomes a URL and 404s.
>
> Hence a small real module is served and imported. If you upgrade Scalar, check the
> `initialize(...)` call in `/scalar/v1` — that fourth argument is the module path.

## 5. Configuration

Cache policy is data, not code (`CachingOptions` bound from the `Caching` section), and is resolved
through `IOptionsMonitor<T>` on every call — so flipping `Caching:Enabled` to `false` disables
caching without a restart:

```json
"Caching": {
  "DefaultExpiration": "00:00:30",
  "Enabled": true,
  "KeyPrefix": "scrutor-example"
}
```

## 6. Failing fast

Scanning and decoration move wiring from compile time to run time, so the container is verified at
startup instead of hoping:

```csharp
host.Services.VerifyRegistrations();   // resolves IProductService, IQueryDispatcher, both handlers
```

The console app calls it before its first request; a broken scan filter or a missing decorator
dependency produces a descriptive `InvalidOperationException` at boot.

---

## What the console app prints

```
── 2. First call -> CACHE MISS, the inner service runs (250 ms simulates a database) ──
info: LoggingProductServiceDecorator[2001] → IProductService.GetAllAsync(CancellationToken)
info: CachingProductServiceDecorator[2102] CACHE MISS scrutor-example:iproductservice.getallasync() — stored for 00:01:00
info: LoggingProductServiceDecorator[2002] ← IProductService.GetAllAsync completed in 261.484 ms with List`1 with 3 item(s)
   GetAllAsync()                                            277 ms  -> 3 products, first retrieved at 23:07:36.499

── 3. Second call -> CACHE HIT, the inner service is bypassed ──
info: LoggingProductServiceDecorator[2001] → IProductService.GetAllAsync(CancellationToken)
info: CachingProductServiceDecorator[2101] CACHE HIT  scrutor-example:iproductservice.getallasync()
info: LoggingProductServiceDecorator[2002] ← IProductService.GetAllAsync completed in 1.261 ms with List`1 with 3 item(s)
   GetAllAsync()                                              1 ms  -> 3 products, first retrieved at 23:07:36.499
```

The nesting order is visible directly in the log: the outer logging decorator announces the call,
the inner caching decorator answers it, and `RetrievedAt` proves the implementation was never
touched on the second call.

---

## Notes and trade-offs

* **Solution format.** `ScrutorExample.slnx` is the XML solution format that `dotnet new sln`
  produces on .NET 10. Generate a classic `.sln` with `dotnet new sln --format sln` if a tool in your
  chain needs one.
* **Hand-written decorators vs. dynamic proxies.** These decorators are explicit classes: the
  compiler checks every signature, there is no proxy on the call path, and step debugging works.
  Libraries such as `Scrutor` + `Castle.Core` can generate decorators for you, at the cost of a
  reflection layer and less obvious stack traces. For a handful of services, explicit usually wins.
* **`[LoggerMessage]` everywhere.** With `TreatWarningsAsErrors=true` and
  `AnalysisLevel=latest-recommended`, CA1848/CA1873 force allocation-free structured logging. That is
  a feature: the sample compiles with zero warnings.
* **`NU1900` is suppressed** in `Directory.Build.props`. It only means "the vulnerability data feed
  was unreachable" (offline builds, restricted CI) and says nothing about the packages. Real
  advisories (`NU1901`–`NU1904`) still fail the build.
* **In-memory cache.** `IMemoryCache` is per-process; behind a load balancer each instance has its
  own cache. The decorator only depends on `IMemoryCache`, so swapping in a distributed
  implementation is a composition-root change.
* **Running the built DLL directly.** `dotnet artifacts/bin/ScrutorExample.Api/debug/ScrutorExample.Api.dll`
  takes its *content root* from the current working directory, not from the DLL's location. Started
  from the repository root it will not find `appsettings.json` and silently falls back to defaults
  (`KeyPrefix: scrutor-example` instead of the Development value). Use
  `dotnet run --project src/ScrutorExample.Api`, or set
  `ASPNETCORE_CONTENTROOT=<repo>/src/ScrutorExample.Api`. `dotnet run` resolves the content root from
  the project file, which is why it is unaffected by the artifacts layout.

---

## License

[MIT](LICENSE) © 2026 Alper Çelik. The same expression is declared as `PackageLicenseExpression`
in `Directory.Build.props`, so any package built from this repository carries it too.
