using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Http;
using AgentForge.Areas.Agents.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Agents;

public sealed class AgentsArea : IArea
{
    public string Slug => "agents";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AgentsDbContext>((provider, options) =>
        {
            var dbProvider = provider.GetRequiredService<IDbProvider>();
            dbProvider.Apply(options);
        });

        services.AddScoped<AgentService>();
        services.AddScoped<RunService>();

        services.AddHealthChecks().AddDbContextCheck<AgentsDbContext>("agents-db");
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        routes.MapAgentEndpoints();
        routes.MapRunEndpoints();
    }

    public Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<AgentsDbContext>().Database.EnsureCreatedAsync(cancellationToken);
}
