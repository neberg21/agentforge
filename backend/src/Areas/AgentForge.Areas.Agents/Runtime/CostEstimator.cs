namespace AgentForge.Areas.Agents.Runtime;

public static class CostEstimator
{
    public static decimal Estimate(int promptTokens, int completionTokens, AgentsPricingOptions pricing)
    {
        var promptCost = promptTokens / 1_000_000m * pricing.PromptTokenPerMillion;
        var completionCost = completionTokens / 1_000_000m * pricing.CompletionTokenPerMillion;
        return decimal.Round(promptCost + completionCost, 6, MidpointRounding.AwayFromZero);
    }
}
