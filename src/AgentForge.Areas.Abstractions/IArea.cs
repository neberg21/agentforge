using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Abstractions;

public interface IArea
{
    string Slug { get; }

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder routes);

    Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken);
}
