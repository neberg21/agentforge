using AgentForge.Areas.Abstractions;
using AgentForge.Core;
using AgentForge.Host;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddSingleton(new AreaRegistry());

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

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHostEndpoints();
app.MapAreas();

await app.MigrateAreasAsync();

await app.RunAsync();

public partial class Program;
