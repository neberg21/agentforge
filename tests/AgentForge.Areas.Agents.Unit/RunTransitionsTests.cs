namespace AgentForge.Areas.Agents.Unit;

public class RunTransitionsTests
{
    [Fact]
    public void Pending_darf_nach_Cancelled() =>
        Assert.True(RunTransitions.IsAllowed(RunStatus.Pending, RunStatus.Cancelled));

    [Theory]
    [InlineData(RunStatus.Pending, RunStatus.Running)]
    [InlineData(RunStatus.Pending, RunStatus.Completed)]
    [InlineData(RunStatus.Pending, RunStatus.Failed)]
    [InlineData(RunStatus.Pending, RunStatus.Pending)]
    [InlineData(RunStatus.Running, RunStatus.Completed)]
    [InlineData(RunStatus.Running, RunStatus.Cancelled)]
    [InlineData(RunStatus.Completed, RunStatus.Cancelled)]
    [InlineData(RunStatus.Failed, RunStatus.Running)]
    [InlineData(RunStatus.Cancelled, RunStatus.Pending)]
    public void Alle_uebrigen_Uebergaenge_sind_in_dieser_Ausbaustufe_gesperrt(RunStatus from, RunStatus to) =>
        Assert.False(RunTransitions.IsAllowed(from, to));
}
