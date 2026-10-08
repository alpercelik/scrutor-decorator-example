using Scalar.AspNetCore;
using ScrutorExample.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
//  ASP.NET Core minimal API sample
//  -------------------------------
//  AddProductCatalogue() is the *same* method the console app calls, so the
//  assembly scanning and the logging/caching decorator chain are identical in
//  both applications. Everything below is transport-level wiring only.
// =============================================================================

builder.Services.AddProductCatalogue(builder.Configuration);

builder.Services.AddOpenApi();

// Unhandled exceptions become RFC 9457 ProblemDetails instead of a bare 500.
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// The Scalar API reference renders the OpenAPI document, so both endpoints are
// mapped together. They stay Development-only on purpose: the document describes
// the surface, it should not become part of it in production.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Scalar's client resolves its document source URL RELATIVELY against the
    // "/scalar/" base path, so the default "openapi/v1.json" becomes
    // "/scalar/openapi/v1.json" -> 404 -> a reference UI with no endpoints.
    //
    // The supported way to override it is a config module that Scalar loads with
    // `import()`. That means a real JavaScript module that default-exports the
    // config - NOT inline JSON, which is a syntax error, and not inline module
    // source, which is treated as a URL. Scalar absolutises the argument against
    // "/scalar/", so this path is where the module must be served.
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
        // The UI generates ready-to-run requests; default to the .NET HttpClient
        // snippets instead of shell/curl.
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        // Absolute path -> imported as-is, bypassing the base-path join above.
        .WithJavaScriptConfiguration("/scalar/scalar-config.js"));

    // The API reference is the friendliest entry point while developing.
    app.MapGet("/", () => Results.Redirect("/scalar/v1", permanent: false))
       .ExcludeFromDescription();
}

// One call per feature folder: Program.cs stays about the pipeline, not the routes.
app.MapProductsEndpoints();
app.MapQueriesEndpoints();
app.MapDiagnosticsEndpoints();

app.Run();
