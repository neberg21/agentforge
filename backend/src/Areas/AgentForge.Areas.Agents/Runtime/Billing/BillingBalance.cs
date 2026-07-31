namespace AgentForge.Areas.Agents.Runtime.Billing;

public static class BillingBalance
{
    public static bool IsLow(decimal usdBalance, decimal threshold) =>
        usdBalance < threshold;
}
