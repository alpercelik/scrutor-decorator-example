// =============================================================================
//  Aspire AppHost
//  --------------
//  Orchestrates the runnable service of this sample. The console app is a
//  scripted walkthrough that prints its demo and exits after a few seconds, so it
//  is deliberately NOT modelled here: in the dashboard it would only ever appear
//  as a short-lived "Exited" resource. Run it directly with
//      dotnet run --project src/ScrutorExample.Console
//
//  Start everything with:  aspire start
//  (never `dotnet run` on the AppHost - the Aspire CLI owns the lifecycle.)
// =============================================================================

var builder = DistributedApplication.CreateBuilder(args);

// The minimal API. Aspire injects the port, so the launch profile's fixed
// http://localhost:5251 is bypassed while orchestrated and the dashboard links to
// the real endpoint.
builder.AddProject("api", "../src/ScrutorExample.Api/ScrutorExample.Api.csproj")
    .WithExternalHttpEndpoints()
    // "/" is excluded from the OpenAPI document and redirects to the Scalar
    // reference in Development, so it makes a cheap liveness probe.
    .WithHttpHealthCheck("/");

builder.Build().Run();
