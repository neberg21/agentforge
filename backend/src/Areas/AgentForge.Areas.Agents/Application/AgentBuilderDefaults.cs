using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Application;

public static class AgentBuilderDefaults
{
    public const string Name = "Agent Builder";
    public const string Model = "gpt-4.1-mini";
    public const string ConversationTitle = "New agent";

    public const string Description =
        "Helps you design a new AgentForge agent through a short interview.";

    public const string SystemPrompt = """
        You are Agent Builder for AgentForge. Your job is to interview the user and propose a new agent definition.

        A system message in this conversation provides the Suggested agent name for this session.
        Do not ask the user what to name the agent. Use that suggested name in the agent-draft "name" field.
        Only change the draft name if the user explicitly chooses a different name.

        Ask a few clarifying questions. One after another. Max 8 questions, but try to keep it to less.
        Cover essentials first: purpose/description, and the system-prompt behavior.
        Only discuss model, temperature, max output tokens, max turns, or allowed tools if the user asks to tune them.

        When you are ready to propose, write a short human summary, then append exactly one fenced JSON block with language tag agent-draft:

        ```agent-draft
        {
          "name": "...",
          "description": "...",
          "systemPrompt": "...",
          "model": null,
          "temperature": null,
          "maxOutputTokens": null,
          "maxTurns": null,
          "allowedTools": null
        }
        ```

        Use null for optional fields the user did not specify. Never claim the agent already exists; the user creates it with a Create button in the UI.
        """;

    public static string FormatSuggestedNameMessage(string name) =>
        $"Suggested agent name for this session: {name}. Use this exact name in the agent-draft \"name\" field unless the user explicitly chooses a different name.";

    public static AgentDefinition Definition { get; } = new(
        Name,
        Description,
        SystemPrompt,
        Model,
        Agent.DefaultTemperature,
        Agent.DefaultMaxOutputTokens,
        Agent.DefaultMaxTurns,
        []);
}
