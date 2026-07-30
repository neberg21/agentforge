using AgentForge.Areas.Abstractions;

namespace AgentForge.Host;

public sealed record AreaInfo(string Slug);

public static class HostEndpoints
{
    public static WebApplication MapHostEndpoints(this WebApplication app)
    {
        app.MapGet("/_health", () => TypedResults.Ok(new { status = "ok" }))
            .WithName("Liveness");

        app.MapHealthChecks("/_health/ready");

        app.MapGet("/api/areas", (AreaRegistry registry) =>
                TypedResults.Ok(registry.Areas.Select(area => new AreaInfo(area.Slug)).ToArray()))
            .WithName("Areas");

        return app;
    }
}
