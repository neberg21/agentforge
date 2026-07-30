using AgentForge.Areas.Agents.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public sealed class AgentsDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public AgentsDatabase(string ownerId = "owner-1")
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        CurrentUser = new TestCurrentUser(ownerId);

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public TestCurrentUser CurrentUser { get; }

    public AgentsDbContext NewContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgentsDbContext>();
        optionsBuilder.UseSqlite(_connection);
        var options = optionsBuilder.Options;
        return new AgentsDbContext(options, CurrentUser);
    }

    public void Dispose() => _connection.Dispose();
}
