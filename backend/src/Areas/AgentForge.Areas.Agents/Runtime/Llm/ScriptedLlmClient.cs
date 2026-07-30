namespace AgentForge.Areas.Agents.Runtime.Llm;

public sealed class ScriptedLlmClient : ILlmClient
{
    private readonly Queue<LlmCompletionResult> _results;

    public ScriptedLlmClient()
    {
        _results = new Queue<LlmCompletionResult>();
    }

    public ScriptedLlmClient(IEnumerable<LlmCompletionResult> results)
    {
        _results = new Queue<LlmCompletionResult>(results);
    }

    public void Enqueue(LlmCompletionResult result) => _results.Enqueue(result);

    public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct)
    {
        if (_results.Count == 0)
        {
            throw new InvalidOperationException("ScriptedLlmClient has no remaining results.");
        }

        var result = _results.Dequeue();
        return Task.FromResult(result);
    }
}
