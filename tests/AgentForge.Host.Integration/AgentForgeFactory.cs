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
                ["Auth:LocalOwnerId"] = "test-owner"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbProvider>();
            services.AddSingleton<IDbProvider>(new SharedConnectionDbProvider(_connection));
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

    private sealed class SharedConnectionDbProvider(SqliteConnection connection) : IDbProvider
    {
        public void Apply(DbContextOptionsBuilder options) => options.UseSqlite(connection);
    }
}
