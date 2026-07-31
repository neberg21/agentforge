using AgentForge.Areas.Agents.Runtime.Billing;

namespace AgentForge.Areas.Agents.Unit;

public class BillingBalanceTests
{
    [Theory]
    [InlineData(4.99, 5.0, true)]
    [InlineData(5.0, 5.0, false)]
    [InlineData(12.0, 5.0, false)]
    public void IsLow_ComparesStrictlyLess(decimal usd, decimal threshold, bool expected) =>
        Assert.Equal(expected, BillingBalance.IsLow(usd, threshold));
}
