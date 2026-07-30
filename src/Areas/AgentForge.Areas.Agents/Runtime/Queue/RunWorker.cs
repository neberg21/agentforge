using AgentForge.Areas.Agents.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Runtime.Queue;

public sealed class RunWorker : BackgroundService
{
    private readonly IRunQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AgentsOptions> _options;
    private readonly ILogger<RunWorker> _logger;

    public RunWorker(
        IRunQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<AgentsOptions> options,
        ILogger<RunWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var limiter = new SemaphoreSlim(_options.Value.MaxConcurrentRuns);

        await foreach (var runId in _queue.ReadAllAsync(stoppingToken))
        {
            await limiter.WaitAsync(stoppingToken);

            _ = ProcessAsync(runId, limiter, stoppingToken);
        }
    }

    private async Task ProcessAsync(Guid runId, SemaphoreSlim limiter, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var loop = scope.ServiceProvider.GetRequiredService<RunLoop>();
            await loop.ExecuteAsync(runId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed in worker.", runId);
        }
        finally
        {
            limiter.Release();
        }
    }
}
