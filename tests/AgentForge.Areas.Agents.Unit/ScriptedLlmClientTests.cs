using AgentForge.Areas.Agents.Runtime.Llm;

namespace AgentForge.Areas.Agents.Unit;

public class ScriptedLlmClientTests
{
    [Fact]
    public async Task CompleteAsync_liefert_Ergebnisse_in_Reihenfolge()
    {
        var first = new LlmCompletionResult("eins", [], new LlmUsage(1, 1));
        var second = new LlmCompletionResult("zwei", [], new LlmUsage(2, 2));
        var client = new ScriptedLlmClient();
        client.Enqueue(first);
        client.Enqueue(second);

        var request = new LlmCompletionRequest("m", 0.5, 100, [], []);

        var a = await client.CompleteAsync(request, TestContext.Current.CancellationToken);
        var b = await client.CompleteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("eins", a.Content);
        Assert.Equal("zwei", b.Content);
    }

    [Fact]
    public async Task CompleteAsync_ohne_Vorrat_wirft()
    {
        var client = new ScriptedLlmClient();
        var request = new LlmCompletionRequest("m", 0.5, 100, [], []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CompleteAsync(request, TestContext.Current.CancellationToken));
    }
}
