using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Abstractions.Unit;

public class AreaRegistrationTests
{
    private sealed class StubArea : IArea
    {
        public string Slug => "stub";

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

        public void MapEndpoints(IEndpointRouteBuilder routes) { }

        public Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void AddAreaSupport_vor_AddArea_liefert_genau_eine_Registry()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddAreaSupport();
        builder.AddArea<StubArea>();

        using var app = builder.Build();

        var registries = app.Services.GetServices<AreaRegistry>().ToArray();

        Assert.Single(registries);
        Assert.Equal(["stub"], registries[0].Areas.Select(area => area.Slug));
    }

    [Fact]
    public void AddArea_vor_AddAreaSupport_liefert_genau_eine_Registry()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddArea<StubArea>();
        builder.AddAreaSupport();

        using var app = builder.Build();

        var registries = app.Services.GetServices<AreaRegistry>().ToArray();

        Assert.Single(registries);
        Assert.Equal(["stub"], registries[0].Areas.Select(area => area.Slug));
    }
}
