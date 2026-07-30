using AgentForge.Areas.Agents.Runtime.Llm;

namespace AgentForge.Host.Integration;

internal sealed class DelayedScriptedLlmClient : ILlmClient
{
    private readonly ScriptedLlmClient _inner;
    private readonly TimeSpan _delay;

    public DelayedScriptedLlmClient(IEnumerable<LlmCompletionResult> results, TimeSpan delay)
    {
        _inner = new ScriptedLlmClient(results);
        _delay = delay;
    }

    public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct)
    {
        await Task.Delay(_delay, ct);
        return await _inner.CompleteAsync(request, ct);
    }
}

internal sealed class GateLlmClient : ILlmClient
{
    private readonly TaskCompletionSource _gate;
    private readonly LlmCompletionResult _result;

    public GateLlmClient(TaskCompletionSource gate, LlmCompletionResult result)
    {
        _gate = gate;
        _result = result;
    }

    public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct)
    {
        await _gate.Task.WaitAsync(ct);
        return _result;
    }
}
