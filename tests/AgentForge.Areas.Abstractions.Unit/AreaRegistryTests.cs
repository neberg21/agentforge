using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Abstractions.Unit;

public class AreaRegistryTests
{
    private sealed class StubArea(string slug) : IArea
    {
        public string Slug { get; } = slug;

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

        public void MapEndpoints(IEndpointRouteBuilder routes) { }

        public Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void Add_nimmt_verschiedene_Bereiche_auf()
    {
        var registry = new AreaRegistry();

        registry.Add(new StubArea("agents"));
        registry.Add(new StubArea("dnd"));

        Assert.Equal(["agents", "dnd"], registry.Areas.Select(a => a.Slug));
    }

    [Fact]
    public void Add_lehnt_doppelte_Slugs_ab()
    {
        var registry = new AreaRegistry();
        registry.Add(new StubArea("agents"));

        var exception = Assert.Throws<InvalidOperationException>(() => registry.Add(new StubArea("agents")));

        Assert.Contains("agents", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_lehnt_ungueltige_Slugs_ab() =>
        Assert.Throws<InvalidOperationException>(() => new AreaRegistry().Add(new StubArea("Agents")));
}
