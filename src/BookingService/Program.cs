using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Course rule #7: the OpenAPI document and Scalar only in Development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");

await app.RunAsync().ConfigureAwait(false);

/// <summary>The entry point. Public so the integration tests can start the app with WebApplicationFactory.</summary>
public partial class Program
{
    /// <summary>Only the top-level statements create the app.</summary>
    protected Program() { }
}
