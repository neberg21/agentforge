namespace AgentForge.Areas.Agents.Unit;

public class RunTransitionsTests
{
    [Fact]
    public void IsAllowed_WhenPendingToCancelled_ReturnsTrue() =>
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
    public void IsAllowed_WhenTransitionNotYetSupported_ReturnsFalse(RunStatus from, RunStatus to) =>
        Assert.False(RunTransitions.IsAllowed(from, to));
}
