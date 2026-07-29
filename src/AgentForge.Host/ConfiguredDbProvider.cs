using AgentForge.Areas.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgentForge.Host;

public sealed class ConfiguredDbProvider(IOptions<DatabaseOptions> options) : IDbProvider
{
    public void Apply(DbContextOptionsBuilder builder)
    {
        var connectionString = options.Value.ConnectionString;
        EnsureDataDirectoryExists(connectionString);
        builder.UseSqlite(connectionString);
    }

    private static void EnsureDataDirectoryExists(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

        if (string.IsNullOrEmpty(dataSource) || dataSource.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
