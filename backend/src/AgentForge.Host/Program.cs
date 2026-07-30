using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents;
using AgentForge.Core;
using AgentForge.Host;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton<IClock, SystemClock>();

builder.AddAreaSupport();

builder.Services.AddOptions<LocalUserOptions>()
    .Bind(builder.Configuration.GetSection(LocalUserOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<ICurrentUser, LocalSingleUser>();

builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => string.Equals(options.Provider, DatabaseOptions.Sqlite, StringComparison.OrdinalIgnoreCase),
        "Database:Provider unterstuetzt in dieser Ausbaustufe nur 'sqlite'. 'postgres' folgt mit der Umstellung auf Neon.")
    .ValidateOnStart();
builder.Services.AddSingleton<IDbProvider, ConfiguredDbProvider>();

builder.Services.AddAuthorization(options =>
    options.AddPolicy(AreaPolicies.AreaAccess, policy => policy.RequireAssertion(_ => true)));

builder.AddArea<AgentsArea>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHostEndpoints();
app.MapAreas();

app.MapFallback(async (HttpContext context, IWebHostEnvironment environment) =>
{
    if (context.Request.Path.StartsWithSegments("/api")
        || context.Request.Path.StartsWithSegments("/_health")
        || context.Request.Path.StartsWithSegments("/openapi")
        || context.Request.Path.StartsWithSegments("/scalar"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var file = environment.WebRootFileProvider.GetFileInfo("index.html");
    if (!file.Exists)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(file);
});

await app.MigrateAreasAsync();

await app.RunAsync();

public partial class Program;
