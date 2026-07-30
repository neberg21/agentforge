namespace AgentForge.Areas.Agents.Application;

public sealed class AgentSuggestionService
{
    public const int MaxRandomAttempts = 32;

    private readonly AgentService _agents;
    private readonly IAgentNameCandidateSource _names;

    public AgentSuggestionService(AgentService agents, IAgentNameCandidateSource names)
    {
        _agents = agents;
        _names = names;
    }

    public async Task<string> SuggestNameAsync(CancellationToken ct)
    {
        string? last = null;
        for (var attempt = 0; attempt < MaxRandomAttempts; attempt++)
        {
            var candidate = _names.NextFirstName();
            last = candidate;
            if (!await _agents.IsNameTakenAsync(candidate, ct))
            {
                return candidate;
            }
        }

        var baseName = last ?? _names.NextFirstName();
        var suffix = 2;
        while (true)
        {
            var candidate = $"{baseName}-{suffix}";
            if (!await _agents.IsNameTakenAsync(candidate, ct))
            {
                return candidate;
            }

            suffix++;
        }
    }
}
