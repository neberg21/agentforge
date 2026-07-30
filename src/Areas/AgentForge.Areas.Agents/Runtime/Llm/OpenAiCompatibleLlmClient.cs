using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Runtime.Llm;

public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly AgentsOptions _options;

    public OpenAiCompatibleLlmClient(HttpClient http, IOptions<AgentsOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct)
    {
        var body = new ChatCompletionRequest
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxOutputTokens,
            Messages = request.Messages.Select(ToApiMessage).ToList(),
            Tools = request.AllowedToolNames.Count == 0
                ? null
                : request.AllowedToolNames.Select(ToTool).ToList()
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        message.Content = JsonContent.Create(body, options: JsonOptions);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Llm.ApiKey);

        using var response = await _http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"LLM request failed with {(int)response.StatusCode}: {errorBody}",
                null,
                response.StatusCode);
        }

        var parsed = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, ct);
        if (parsed?.Choices is null || parsed.Choices.Count == 0)
        {
            throw new InvalidOperationException("LLM response contained no choices.");
        }

        var choice = parsed.Choices[0].Message;
        var toolCalls = (choice.ToolCalls ?? [])
            .Select(call => new LlmToolCall(
                call.Id,
                call.Function?.Name ?? string.Empty,
                call.Function?.Arguments ?? "{}"))
            .ToList();

        var usage = parsed.Usage is null
            ? new LlmUsage(0, 0)
            : new LlmUsage(parsed.Usage.PromptTokens, parsed.Usage.CompletionTokens);

        return new LlmCompletionResult(choice.Content, toolCalls, usage);
    }

    private static ChatMessage ToApiMessage(LlmMessage message)
    {
        List<ChatToolCall>? toolCalls = null;
        if (!string.IsNullOrWhiteSpace(message.ToolCallsJson))
        {
            toolCalls = JsonSerializer.Deserialize<List<ChatToolCall>>(message.ToolCallsJson, JsonOptions);
        }

        return new ChatMessage
        {
            Role = message.Role,
            Content = message.Content,
            ToolCallId = message.ToolCallId,
            ToolCalls = toolCalls
        };
    }

    private static ChatTool ToTool(string name)
    {
        var parameters = JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });
        return new ChatTool
        {
            Type = "function",
            Function = new ChatFunction
            {
                Name = name,
                Parameters = parameters
            }
        };
    }

    private sealed class ChatCompletionRequest
    {
        public string Model { get; set; } = string.Empty;

        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        public List<ChatMessage> Messages { get; set; } = [];

        public List<ChatTool>? Tools { get; set; }
    }

    private sealed class ChatMessage
    {
        public string Role { get; set; } = string.Empty;

        public string? Content { get; set; }

        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<ChatToolCall>? ToolCalls { get; set; }
    }

    private sealed class ChatTool
    {
        public string Type { get; set; } = "function";

        public ChatFunction Function { get; set; } = new();
    }

    private sealed class ChatFunction
    {
        public string Name { get; set; } = string.Empty;

        public JsonElement Parameters { get; set; }
    }

    private sealed class ChatToolCall
    {
        public string Id { get; set; } = string.Empty;

        public string Type { get; set; } = "function";

        public ChatFunctionCall? Function { get; set; }
    }

    private sealed class ChatFunctionCall
    {
        public string Name { get; set; } = string.Empty;

        public string Arguments { get; set; } = "{}";
    }

    private sealed class ChatCompletionResponse
    {
        public List<ChatChoice> Choices { get; set; } = [];

        public ChatUsage? Usage { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage Message { get; set; } = new();
    }

    private sealed class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }
}
