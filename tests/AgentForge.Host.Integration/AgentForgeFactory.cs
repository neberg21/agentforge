using AgentForge.Areas.Agents.Runtime.Queue;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentForge.Host.Integration;

public sealed class AgentForgeFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public AgentForgeFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "sqlite",
                ["Database:ConnectionString"] = "Data Source=:memory:",
                ["Auth:LocalOwnerId"] = "test-owner",
                ["Areas:Agents:Llm:UseFake"] = "true",
                ["Areas:Agents:Llm:BaseUrl"] = "http://localhost",
                ["Areas:Agents:MaxConcurrentRuns"] = "2",
                ["Areas:Agents:Pricing:PromptTokenPerMillion"] = "1",
                ["Areas:Agents:Pricing:CompletionTokenPerMillion"] = "2"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbProvider>();
            services.AddSingleton<IDbProvider>(new SharedConnectionDbProvider(_connection));

            // Keep existing endpoint tests deterministic: create stays Pending until Task 8
            // execution coverage opts into the real channel queue.
            services.RemoveAll<IRunQueue>();
            services.AddSingleton<IRunQueue, NoOpRunQueue>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private sealed class SharedConnectionDbProvider : IDbProvider
    {
        private readonly SqliteConnection _connection;

        public SharedConnectionDbProvider(SqliteConnection connection)
        {
            _connection = connection;
        }

        public void Apply(DbContextOptionsBuilder options) => options.UseSqlite(_connection);
    }
}
