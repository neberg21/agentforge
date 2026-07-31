namespace AgentForge.Areas.Agents.Runtime.Billing;

public static class NanoGptApiRoot
{
    public static string FromLlmBaseUrl(string llmBaseUrl)
    {
        var trimmed = llmBaseUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..^3].TrimEnd('/');
        }

        return trimmed;
    }
}
