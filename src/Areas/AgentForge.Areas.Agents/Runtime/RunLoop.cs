using System.Text.Json;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Tools;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Runtime;

public sealed class RunLoop
{
    private readonly AgentsDbContext _db;
    private readonly ILlmClient _llm;
    private readonly IToolRegistry _tools;
    private readonly IRunEventBus _events;
    private readonly IClock _clock;
    private readonly AgentsOptions _options;

    public RunLoop(
        AgentsDbContext db,
        ILlmClient llm,
        IToolRegistry tools,
        IRunEventBus events,
        IClock clock,
        IOptions<AgentsOptions> options)
    {
        _db = db;
        _llm = llm;
        _tools = tools;
        _events = events;
        _clock = clock;
        _options = options.Value;
    }

    public async Task ExecuteAsync(Guid runId, CancellationToken ct)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run is null || run.Status == RunStatus.Cancelled || run.Status != RunStatus.Pending)
        {
            return;
        }

        run.MarkRunning(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        Publish(run.Id, RunEventType.Status, $"{{\"status\":\"{run.Status}\"}}");

        _tools.EnsureStubs(run.AgentSnapshot.AllowedTools);

        var turns = 0;
        try
        {
            while (turns < run.AgentSnapshot.MaxTurns)
            {
                ct.ThrowIfCancellationRequested();
                if (await IsCancelledAsync(runId, ct))
                {
                    await CancelIfRunningAsync(runId, ct);
                    return;
                }

                run = await LoadRunAsync(runId, ct);
                if (run is null || run.Status != RunStatus.Running)
                {
                    return;
                }

                var history = await LoadHistoryAsync(runId, ct);
                var request = BuildRequest(run, history);
                var completion = await _llm.CompleteAsync(request, ct);

                if (await IsCancelledAsync(runId, ct))
                {
                    await CancelIfRunningAsync(runId, ct);
                    return;
                }

                var toolCallsJson = completion.ToolCalls.Count == 0
                    ? null
                    : SerializeToolCalls(completion.ToolCalls);

                AppendMessage(
                    run,
                    history.Count,
                    MessageRole.Assistant,
                    completion.Content,
                    toolCallsJson,
                    toolCallId: null);
                ApplyUsage(run, completion.Usage);
                await _db.SaveChangesAsync(ct);
                Publish(run.Id, RunEventType.Message, $"{{\"role\":\"Assistant\"}}");
                PublishUsage(run);

                turns++;

                if (completion.ToolCalls.Count == 0)
                {
                    if (_options.Workspace.Enabled)
                    {
                        return;
                    }

                    run.Complete(_clock.UtcNow);
                    await _db.SaveChangesAsync(ct);
                    Publish(run.Id, RunEventType.Status, $"{{\"status\":\"{run.Status}\"}}");
                    Publish(run.Id, RunEventType.Done, "{}");
                    return;
                }

                var sequence = history.Count + 1;
                foreach (var call in completion.ToolCalls)
                {
                    if (await IsCancelledAsync(runId, ct))
                    {
                        await CancelIfRunningAsync(runId, ct);
                        return;
                    }

                    run = await LoadRunAsync(runId, ct);
                    if (run is null || run.Status != RunStatus.Running)
                    {
                        return;
                    }

                    var toolResult = await _tools.ExecuteOrErrorAsync(call.Name, call.ArgumentsJson, ct);
                    AppendMessage(
                        run,
                        sequence,
                        MessageRole.Tool,
                        toolResult,
                        toolCallsJson: null,
                        toolCallId: call.Id);
                    await _db.SaveChangesAsync(ct);
                    Publish(run.Id, RunEventType.Message, $"{{\"role\":\"Tool\",\"toolCallId\":\"{call.Id}\"}}");
                    sequence++;
                }
            }

            run = await LoadRunAsync(runId, ct);
            if (run is null || run.Status != RunStatus.Running)
            {
                return;
            }

            run.Fail("max_turns exceeded without a final assistant message.", _clock.UtcNow);
            await _db.SaveChangesAsync(ct);
            Publish(run.Id, RunEventType.Error, $"{{\"message\":\"{Escape(run.Error!)}\"}}");
            Publish(run.Id, RunEventType.Done, "{}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await CancelIfRunningAsync(runId, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await FailFromExceptionAsync(runId, ex);
        }
    }

    private async Task<Run?> LoadRunAsync(Guid runId, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        return await _db.Runs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == runId, ct);
    }

    private async Task<IReadOnlyList<RunMessage>> LoadHistoryAsync(Guid runId, CancellationToken ct)
    {
        return await _db.RunMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(message => message.RunId == runId)
            .OrderBy(message => message.Sequence)
            .ToListAsync(ct);
    }

    private void AppendMessage(
        Run run,
        int sequence,
        MessageRole role,
        string? content,
        string? toolCallsJson,
        string? toolCallId)
    {
        var message = RunMessage.Create(run, sequence, role, content, _clock.UtcNow, toolCallsJson, toolCallId);
        _db.RunMessages.Add(message);
    }

    private async Task FailFromExceptionAsync(Guid runId, Exception ex)
    {
        var run = await LoadRunAsync(runId, CancellationToken.None);
        if (run is null || run.Status != RunStatus.Running)
        {
            return;
        }

        run.Fail(ex.Message, _clock.UtcNow);
        await _db.SaveChangesAsync(CancellationToken.None);
        Publish(run.Id, RunEventType.Error, $"{{\"message\":\"{Escape(ex.Message)}\"}}");
        Publish(run.Id, RunEventType.Done, "{}");
    }

    private async Task CancelIfRunningAsync(Guid runId, CancellationToken ct)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run is null || run.Status != RunStatus.Running)
        {
            return;
        }

        run.Cancel(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        Publish(run.Id, RunEventType.Status, $"{{\"status\":\"{run.Status}\"}}");
        Publish(run.Id, RunEventType.Done, "{}");
    }

    private async Task<bool> IsCancelledAsync(Guid runId, CancellationToken ct)
    {
        var status = await _db.Runs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => r.Status)
            .FirstAsync(ct);
        return status == RunStatus.Cancelled;
    }

    private void ApplyUsage(Run run, LlmUsage usage)
    {
        var promptTotal = (run.PromptTokens ?? 0) + usage.PromptTokens;
        var completionTotal = (run.CompletionTokens ?? 0) + usage.CompletionTokens;
        var cost = CostEstimator.Estimate(promptTotal, completionTotal, _options.Pricing);
        run.ApplyUsage(usage.PromptTokens, usage.CompletionTokens, cost);
    }

    private void PublishUsage(Run run)
    {
        Publish(
            run.Id,
            RunEventType.Usage,
            $"{{\"promptTokens\":{run.PromptTokens},\"completionTokens\":{run.CompletionTokens},\"costEstimate\":{run.CostEstimate}}}");
    }

    private void Publish(Guid runId, RunEventType type, string payload)
    {
        var ev = new RunEvent(runId, type, payload);
        _events.Publish(ev);
    }

    private static LlmCompletionRequest BuildRequest(Run run, IReadOnlyList<RunMessage> history)
    {
        var messages = history
            .Select(ToLlmMessage)
            .ToList();

        return new LlmCompletionRequest(
            run.AgentSnapshot.Model,
            run.AgentSnapshot.Temperature,
            run.AgentSnapshot.MaxOutputTokens,
            messages,
            run.AgentSnapshot.AllowedTools);
    }

    private static LlmMessage ToLlmMessage(RunMessage message)
    {
        var role = message.Role switch
        {
            MessageRole.System => "system",
            MessageRole.User => "user",
            MessageRole.Assistant => "assistant",
            MessageRole.Tool => "tool",
            _ => "user"
        };

        return new LlmMessage(role, message.Content, message.ToolCallsJson, message.ToolCallId);
    }

    private static string SerializeToolCalls(IReadOnlyList<LlmToolCall> toolCalls)
    {
        var shaped = toolCalls.Select(call => new
        {
            id = call.Id,
            type = "function",
            function = new { name = call.Name, arguments = call.ArgumentsJson }
        });

        return JsonSerializer.Serialize(shaped);
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
