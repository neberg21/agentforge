namespace AgentForge.Areas.Agents.Runtime.Llm;

public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

public sealed record LlmUsage(int PromptTokens, int CompletionTokens);

public sealed record LlmMessage(string Role, string? Content, string? ToolCallsJson, string? ToolCallId);

public sealed class LlmCompletionRequest
{
    public LlmCompletionRequest(
        string model,
        double temperature,
        int maxOutputTokens,
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<string> allowedToolNames)
    {
        Model = model;
        Temperature = temperature;
        MaxOutputTokens = maxOutputTokens;
        Messages = messages;
        AllowedToolNames = allowedToolNames;
    }

    public string Model { get; }

    public double Temperature { get; }

    public int MaxOutputTokens { get; }

    public IReadOnlyList<LlmMessage> Messages { get; }

    public IReadOnlyList<string> AllowedToolNames { get; }
}

public sealed class LlmCompletionResult
{
    public LlmCompletionResult(string? content, IReadOnlyList<LlmToolCall> toolCalls, LlmUsage usage)
    {
        Content = content;
        ToolCalls = toolCalls;
        Usage = usage;
    }

    public string? Content { get; }

    public IReadOnlyList<LlmToolCall> ToolCalls { get; }

    public LlmUsage Usage { get; }
}

public interface ILlmClient
{
    Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct);
}
