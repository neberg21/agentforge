using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Http;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Queue;
using AgentForge.Areas.Agents.Runtime.Tools;
using AgentForge.Areas.Agents.Runtime.Workspace;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

        services.AddOptions<AgentsOptions>()
            .Bind(configuration.GetSection(AgentsOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.MaxConcurrentRuns >= 1,
                "Areas:Agents:MaxConcurrentRuns must be at least 1.")
            .Validate<IHostEnvironment>(
                (options, environment) =>
                    environment.IsEnvironment("Testing")
                    || options.Llm.UseFake
                    || !string.IsNullOrWhiteSpace(options.Llm.ApiKey),
                "Areas:Agents:Llm:ApiKey is required when UseFake is false.")
            .Validate(
                options => !options.Workspace.Enabled
                    || (!string.IsNullOrWhiteSpace(options.Workspace.RemoteUrl)
                        && !string.IsNullOrWhiteSpace(options.Workspace.LocalPath)
                        && !string.IsNullOrWhiteSpace(options.Workspace.WorktreesRoot)),
                "Areas:Agents:Workspace requires RemoteUrl, LocalPath, and WorktreesRoot when Enabled.")
            .ValidateOnStart();

        services.AddSingleton<IRunEventBus, InProcessRunEventBus>();
        services.AddSingleton<IConversationEventBus, InProcessConversationEventBus>();
        services.AddSingleton<IRunQueue, ChannelRunQueue>();
        services.AddSingleton<IConversationReplyQueue, ChannelConversationReplyQueue>();
        services.AddSingleton<IGitWorkspace, GitCliWorkspace>();
        services.AddScoped<IRunWorkspaceSession, RunWorkspaceSession>();
        services.AddScoped<IConversationReadSession, ConversationReadSession>();
        services.AddSingleton<IToolRegistry>(provider =>
        {
            var registry = new ToolRegistry();
            var options = provider.GetRequiredService<IOptions<AgentsOptions>>();
            if (options.Value.Workspace.Enabled)
            {
                var readFile = new ReadFileTool();
                var writeFile = new WriteFileTool();
                var runShell = new RunShellTool(options);
                registry.Register(readFile);
                registry.Register(writeFile);
                registry.Register(runShell);
            }

            return registry;
        });
        services.AddScoped<RunLoop>();
        services.AddScoped<ConversationLoop>();
        services.AddHostedService<RunWorker>();
        services.AddHostedService<ConversationReplyWorker>();

        RegisterLlmClient(services, configuration);

        services.AddScoped<AgentService>();
        services.AddSingleton<IAgentNameCandidateSource, BogusGermanFirstNameSource>();
        services.AddScoped<AgentSuggestionService>();
        services.AddScoped<RunService>();
        services.AddScoped<ConversationService>();
        services.AddScoped<BuilderSessionService>();

        services.AddHealthChecks().AddDbContextCheck<AgentsDbContext>("agents-db");
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        routes.MapAgentEndpoints();
        routes.MapRunEndpoints();
        routes.MapConversationEndpoints();
    }

    public Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<AgentsDbContext>().Database.MigrateAsync(cancellationToken);

    private static void RegisterLlmClient(IServiceCollection services, IConfiguration configuration)
    {
        var useFake = configuration.GetValue($"{AgentsOptions.SectionName}:Llm:UseFake", false);
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var isTesting = string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

        if (useFake || isTesting)
        {
            services.AddSingleton<ILlmClient>(_ =>
            {
                var result = new LlmCompletionResult("OK", [], new LlmUsage(1, 1));
                return new ScriptedLlmClient([result]);
            });
            return;
        }

        services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<AgentsOptions>>().Value;
            var baseUrl = options.Llm.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = options.Llm.Timeout;
        });
    }
}
