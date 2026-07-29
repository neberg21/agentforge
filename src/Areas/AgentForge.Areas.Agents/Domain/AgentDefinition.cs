namespace AgentForge.Areas.Agents.Domain;

public sealed record AgentDefinition(
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    int MaxTurns,
    string[] AllowedTools);
