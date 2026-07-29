using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Abstractions;

public static class AreaPolicies
{
    public const string AreaAccess = "area-access";
}

public static class AreaRegistration
{
    public static WebApplicationBuilder AddAreaSupport(this WebApplicationBuilder builder)
    {
        GetOrCreateRegistry(builder.Services);
        return builder;
    }

    public static WebApplicationBuilder AddArea<TArea>(this WebApplicationBuilder builder)
        where TArea : IArea, new()
    {
        var registry = GetOrCreateRegistry(builder.Services);
        var area = new TArea();
        registry.Add(area);
        area.ConfigureServices(builder.Services, builder.Configuration);
        return builder;
    }

    public static WebApplication MapAreas(this WebApplication app)
    {
        foreach (var area in app.Services.GetRequiredService<AreaRegistry>().Areas)
        {
            var group = app.MapGroup($"/api/{area.Slug}")
                .RequireAuthorization(AreaPolicies.AreaAccess)
                .WithTags(area.Slug);

            area.MapEndpoints(group);
        }

        return app;
    }

    public static async Task MigrateAreasAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();

        foreach (var area in app.Services.GetRequiredService<AreaRegistry>().Areas)
        {
            await area.MigrateAsync(scope.ServiceProvider, cancellationToken);
        }
    }

    private static AreaRegistry GetOrCreateRegistry(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(AreaRegistry))?.ImplementationInstance is AreaRegistry existing)
        {
            return existing;
        }

        var registry = new AreaRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
