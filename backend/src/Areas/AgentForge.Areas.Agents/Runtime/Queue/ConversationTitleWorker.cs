using AgentForge.Areas.Agents.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentForge.Areas.Agents.Runtime.Queue;

public sealed class ConversationTitleWorker : BackgroundService
{
    private readonly IConversationTitleQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConversationTitleWorker> _logger;

    public ConversationTitleWorker(
        IConversationTitleQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ConversationTitleWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Conversation title job for {ConversationId} failed.",
                    job.ConversationId);
            }
            finally
            {
                _queue.MarkCompleted(job.ConversationId);
            }
        }
    }

    private async Task ProcessAsync(ConversationTitleJob job, CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ConversationTitleService>();
        await service.SuggestAndApplyAsync(job.ConversationId, stoppingToken);
    }
}
