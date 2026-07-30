using AgentForge.Areas.Agents.Runtime;

namespace AgentForge.Areas.Agents.Unit;

public class CostEstimatorTests
{
    [Fact]
    public void Estimate_rechnet_anteilig_pro_Million()
    {
        var pricing = new AgentsPricingOptions
        {
            PromptTokenPerMillion = 1.0m,
            CompletionTokenPerMillion = 2.0m
        };

        var estimate = CostEstimator.Estimate(500_000, 250_000, pricing);

        Assert.Equal(1.0m, estimate);
    }
}
