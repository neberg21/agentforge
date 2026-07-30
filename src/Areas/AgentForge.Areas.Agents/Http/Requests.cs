using System.ComponentModel.DataAnnotations;
using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Http;

public sealed record CreateAgentRequest(
    [property: Required][property: StringLength(100, MinimumLength = 1)] string Name,
    [property: StringLength(1000)] string? Description,
    [property: Required][property: StringLength(20_000, MinimumLength = 1)] string SystemPrompt,
    [property: Required][property: StringLength(100, MinimumLength = 1)] string Model,
    [property: Range(0.0, 2.0)] double? Temperature,
    [property: Range(1, 200_000)] int? MaxOutputTokens,
    [property: Range(1, 200)] int? MaxTurns,
    string[]? AllowedTools);

public sealed record UpdateAgentRequest(
    [property: Required][property: StringLength(100, MinimumLength = 1)] string Name,
    [property: StringLength(1000)] string? Description,
    [property: Required][property: StringLength(20_000, MinimumLength = 1)] string SystemPrompt,
    [property: Required][property: StringLength(100, MinimumLength = 1)] string Model,
    [property: Range(0.0, 2.0)] double? Temperature,
    [property: Range(1, 200_000)] int? MaxOutputTokens,
    [property: Range(1, 200)] int? MaxTurns,
    string[]? AllowedTools,
    Guid ConcurrencyToken);

public sealed record CreateRunRequest(
    Guid AgentId,
    [property: Required][property: StringLength(20_000, MinimumLength = 1)] string Objective);

public sealed record CancelRunRequest(Guid ConcurrencyToken);

public static class RequestMapping
{
    public static AgentDefinition ToDefinition(this CreateAgentRequest request) =>
        Build(request.Name, request.Description, request.SystemPrompt, request.Model,
            request.Temperature, request.MaxOutputTokens, request.MaxTurns, request.AllowedTools);

    public static AgentDefinition ToDefinition(this UpdateAgentRequest request) =>
        Build(request.Name, request.Description, request.SystemPrompt, request.Model,
            request.Temperature, request.MaxOutputTokens, request.MaxTurns, request.AllowedTools);

    private static AgentDefinition Build(
        string name,
        string? description,
        string systemPrompt,
        string model,
        double? temperature,
        int? maxOutputTokens,
        int? maxTurns,
        string[]? allowedTools) =>
        new(name,
            description,
            systemPrompt,
            model,
            temperature ?? Agent.DefaultTemperature,
            maxOutputTokens ?? Agent.DefaultMaxOutputTokens,
            maxTurns ?? Agent.DefaultMaxTurns,
            allowedTools ?? []);
}
