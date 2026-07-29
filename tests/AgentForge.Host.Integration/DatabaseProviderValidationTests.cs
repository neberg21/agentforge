using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AgentForge.Host.Integration;

public class DatabaseProviderValidationTests(AgentForgeFactory factory) : IClassFixture<AgentForgeFactory>
{
    [Fact]
    public void Postgres_wird_beim_Start_abgelehnt()
    {
        using var postgresFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "postgres"
                })));

        var exception = Assert.Throws<OptionsValidationException>(() => postgresFactory.CreateClient());

        Assert.Contains(
            "Database:Provider unterstuetzt in dieser Ausbaustufe nur 'sqlite'",
            exception.Message,
            StringComparison.Ordinal);
    }
}
