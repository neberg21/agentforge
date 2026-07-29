namespace AgentForge.Areas.Agents.Domain;

public sealed record AgentSnapshot(
    string Name,
    string SystemPrompt,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    int MaxTurns,
    string[] AllowedTools);
