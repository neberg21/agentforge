using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Queue;
using AgentForge.Areas.Agents.Runtime.Workspace;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentForge.Areas.Agents.Runtime.Queue;

public sealed class ConversationReplyWorker : BackgroundService
{
    private readonly IConversationReplyQueue _queue;
    private readonly IConversationTitleQueue _titleQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConversationReplyWorker> _logger;

    public ConversationReplyWorker(
        IConversationReplyQueue queue,
        IConversationTitleQueue titleQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<ConversationReplyWorker> logger)
    {
        _queue = queue;
        _titleQueue = titleQueue;
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
                    "Conversation reply job for {ConversationId} failed.",
                    job.ConversationId);
            }
        }
    }

    private async Task ProcessAsync(ConversationReplyJob job, CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IConversationReadSession>();
        var loop = scope.ServiceProvider.GetRequiredService<ConversationLoop>();
        var events = scope.ServiceProvider.GetRequiredService<IConversationEventBus>();
        var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        await session.BeginAsync(stoppingToken);
        session.Bind();
        try
        {
            foreach (var agentId in job.AgentIds)
            {
                await loop.ExecuteReplyAsync(job.ConversationId, agentId, job.StreamId, stoppingToken);
                await RecordTurnAndMaybeEnqueueTitleAsync(db, clock, job.ConversationId, stoppingToken);
            }
        }
        finally
        {
            session.Unbind();
            var done = new ConversationEvent(job.ConversationId, RunEventType.Done, "{}");
            events.Publish(done);
        }
    }

    private async Task RecordTurnAndMaybeEnqueueTitleAsync(
        AgentsDbContext db,
        IClock clock,
        Guid conversationId,
        CancellationToken ct)
    {
        var conversation = await db.Conversations.FirstOrDefaultAsync(
            candidate => candidate.Id == conversationId,
            ct);
        if (conversation is null || conversation.IsArchived)
        {
            return;
        }

        conversation.RecordCompletedTurn(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        if (conversation.ShouldSuggestTitle())
        {
            var titleJob = new ConversationTitleJob(conversationId);
            _titleQueue.TryEnqueue(titleJob);
        }
    }
}
