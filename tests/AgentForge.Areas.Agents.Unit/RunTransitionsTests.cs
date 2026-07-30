namespace AgentForge.Areas.Agents.Unit;

public class RunTransitionsTests
{
    [Theory]
    [InlineData(RunStatus.Pending, RunStatus.Running)]
    [InlineData(RunStatus.Pending, RunStatus.Cancelled)]
    [InlineData(RunStatus.Running, RunStatus.Completed)]
    [InlineData(RunStatus.Running, RunStatus.Failed)]
    [InlineData(RunStatus.Running, RunStatus.Cancelled)]
    public void IsAllowed_WhenSupported_ReturnsTrue(RunStatus from, RunStatus to) =>
        Assert.True(RunTransitions.IsAllowed(from, to));

    [Theory]
    [InlineData(RunStatus.Pending, RunStatus.Completed)]
    [InlineData(RunStatus.Pending, RunStatus.Failed)]
    [InlineData(RunStatus.Pending, RunStatus.Pending)]
    [InlineData(RunStatus.Running, RunStatus.Pending)]
    [InlineData(RunStatus.Running, RunStatus.Running)]
    [InlineData(RunStatus.Completed, RunStatus.Cancelled)]
    [InlineData(RunStatus.Failed, RunStatus.Running)]
    [InlineData(RunStatus.Cancelled, RunStatus.Pending)]
    public void IsAllowed_WhenUnsupported_ReturnsFalse(RunStatus from, RunStatus to) =>
        Assert.False(RunTransitions.IsAllowed(from, to));
}
