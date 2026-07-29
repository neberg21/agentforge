namespace AgentForge.Areas.Agents.Domain;

public sealed class Agent
{
    public const double DefaultTemperature = 0.7;
    public const int DefaultMaxOutputTokens = 4096;
    public const int DefaultMaxTurns = 20;

    private Agent()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string SystemPrompt { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public double Temperature { get; private set; }

    public int MaxOutputTokens { get; private set; }

    public int MaxTurns { get; private set; }

    public string[] AllowedTools { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public static Agent Create(string ownerId, AgentDefinition definition, DateTimeOffset now)
    {
        var agent = new Agent
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            CreatedAt = now
        };

        agent.Apply(definition, now);
        return agent;
    }

    public void Update(AgentDefinition definition, DateTimeOffset now) => Apply(definition, now);

    public void Archive(DateTimeOffset now)
    {
        ArchivedAt = now;
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public AgentSnapshot ToSnapshot() =>
        new(Name, SystemPrompt, Model, Temperature, MaxOutputTokens, MaxTurns, [.. AllowedTools]);

    private void Apply(AgentDefinition definition, DateTimeOffset now)
    {
        Name = definition.Name;
        Description = definition.Description;
        SystemPrompt = definition.SystemPrompt;
        Model = definition.Model;
        Temperature = definition.Temperature;
        MaxOutputTokens = definition.MaxOutputTokens;
        MaxTurns = definition.MaxTurns;
        AllowedTools = [.. definition.AllowedTools];
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }
}
