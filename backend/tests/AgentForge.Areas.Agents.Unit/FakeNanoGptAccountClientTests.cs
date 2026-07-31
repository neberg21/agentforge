using AgentForge.Areas.Agents.Runtime.Billing;

namespace AgentForge.Areas.Agents.Unit;

public class FakeNanoGptAccountClientTests
{
    [Fact]
    public async Task CreateThenGet_ReturnsSameTx()
    {
        var client = new FakeNanoGptAccountClient();
        var amount = 0.00002m;

        var created = await client.CreateBtcLnDepositAsync(amount, CancellationToken.None);
        var loaded = await client.GetBtcLnDepositAsync(created.TxId, CancellationToken.None);

        Assert.Equal(created.TxId, loaded.TxId);
        Assert.Equal(amount, loaded.Amount);
        Assert.Equal("New", loaded.Status);
    }
}
