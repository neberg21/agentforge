using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Queue;
using AgentForge.Areas.Agents.Runtime.Workspace;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentForge.Host.Integration;

public class AgentForgeFactory : WebApplicationFactory<Program>
{
    private readonly bool _enableRunExecution;
    private readonly ILlmClient? _llmOverride;
    private readonly IGitWorkspace? _gitOverride;
    private readonly IReadOnlyDictionary<string, string?>? _extraConfiguration;
    private readonly string? _tempDbPath;
    private readonly SqliteConnection? _memoryConnection;

    public AgentForgeFactory()
        : this(enableRunExecution: false, llmOverride: null, gitOverride: null, extraConfiguration: null)
    {
    }

    protected AgentForgeFactory(
        bool enableRunExecution,
        ILlmClient? llmOverride,
        IGitWorkspace? gitOverride,
        IReadOnlyDictionary<string, string?>? extraConfiguration)
    {
        _enableRunExecution = enableRunExecution;
        _llmOverride = llmOverride;
        _gitOverride = gitOverride;
        _extraConfiguration = extraConfiguration;

        if (enableRunExecution)
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"agentforge-test-{Guid.NewGuid():N}.db");
        }
        else
        {
            _memoryConnection = new SqliteConnection("Data Source=:memory:");
            _memoryConnection.Open();

            using var pragma = _memoryConnection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
    }

    public static AgentForgeFactory ForRunExecution(ILlmClient? llmOverride = null) =>
        new(enableRunExecution: true, llmOverride, gitOverride: null, extraConfiguration: null);

    public static AgentForgeFactory ForWorkspaceRun(
        ILlmClient llmOverride,
        IGitWorkspace gitOverride,
        IReadOnlyDictionary<string, string?> workspaceConfiguration) =>
        new(enableRunExecution: true, llmOverride, gitOverride, workspaceConfiguration);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "sqlite",
                ["Database:ConnectionString"] = "Data Source=:memory:",
                ["Auth:LocalOwnerId"] = "test-owner",
                ["Areas:Agents:Llm:UseFake"] = "true",
                ["Areas:Agents:Llm:BaseUrl"] = "http://localhost",
                ["Areas:Agents:MaxConcurrentRuns"] = "2",
                ["Areas:Agents:Pricing:PromptTokenPerMillion"] = "1",
                ["Areas:Agents:Pricing:CompletionTokenPerMillion"] = "2"
            };

            if (_extraConfiguration is not null)
            {
                foreach (var pair in _extraConfiguration)
                {
                    values[pair.Key] = pair.Value;
                }
            }

            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbProvider>();

            if (_enableRunExecution)
            {
                var path = _tempDbPath!;
                services.AddSingleton<IDbProvider>(new FileSqliteDbProvider(path));
            }
            else
            {
                services.AddSingleton<IDbProvider>(new SharedConnectionDbProvider(_memoryConnection!));
            }

            if (!_enableRunExecution)
            {
                services.RemoveAll<IRunQueue>();
                services.AddSingleton<IRunQueue, NoOpRunQueue>();
            }

            if (_llmOverride is not null)
            {
                services.RemoveAll<ILlmClient>();
                services.AddSingleton(_llmOverride);
            }
            else if (_enableRunExecution)
            {
                services.RemoveAll<ILlmClient>();
                var result = new LlmCompletionResult("OK", [], new LlmUsage(1, 1));
                var llm = new DelayedScriptedLlmClient([result], TimeSpan.FromMilliseconds(300));
                services.AddSingleton<ILlmClient>(llm);
            }

            if (_gitOverride is not null)
            {
                services.RemoveAll<IGitWorkspace>();
                services.AddSingleton(_gitOverride);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _memoryConnection?.Dispose();
            if (_tempDbPath is not null && File.Exists(_tempDbPath))
            {
                try
                {
                    File.Delete(_tempDbPath);
                }
                catch (IOException)
                {
                }
            }
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

    private sealed class FileSqliteDbProvider : IDbProvider
    {
        private readonly string _path;

        public FileSqliteDbProvider(string path)
        {
            _path = path;
        }

        public void Apply(DbContextOptionsBuilder options)
        {
            var connectionString = $"Data Source={_path};Cache=Shared";
            options.UseSqlite(connectionString);
        }
    }
}
