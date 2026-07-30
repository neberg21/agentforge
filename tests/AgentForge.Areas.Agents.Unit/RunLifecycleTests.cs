namespace AgentForge.Areas.Agents.Unit;

public class RunLifecycleTests
{
    private static AgentDefinition Definition() =>
        new("Builder", null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, []);

    private static Run NewRun(TestClock clock)
    {
        var agent = Agent.Create("owner-1", Definition(), clock.UtcNow);
        return Run.Create(agent, "Baue etwas.", clock.UtcNow);
    }

    [Fact]
    public void MarkRunning_WhenPending_SetsStatusAndStartedAt()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);
        var started = clock.Advance(TimeSpan.FromSeconds(1));

        run.MarkRunning(started);

        Assert.Equal(RunStatus.Running, run.Status);
        Assert.Equal(started, run.StartedAt);
    }

    [Fact]
    public void Complete_WhenRunning_SetsCompletedAt()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);
        run.MarkRunning(clock.UtcNow);
        var done = clock.Advance(TimeSpan.FromSeconds(5));

        run.Complete(done);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(done, run.CompletedAt);
        Assert.Null(run.Error);
    }

    [Fact]
    public void Fail_WhenRunning_SetsErrorAndCompletedAt()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);
        run.MarkRunning(clock.UtcNow);

        run.Fail("boom", clock.Advance(TimeSpan.FromSeconds(1)));

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("boom", run.Error);
    }

    [Fact]
    public void Cancel_WhenRunning_Succeeds()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);
        run.MarkRunning(clock.UtcNow);

        run.Cancel(clock.Advance(TimeSpan.FromSeconds(1)));

        Assert.Equal(RunStatus.Cancelled, run.Status);
    }

    [Fact]
    public void ApplyUsage_WhenCalled_AccumulatesTokensAndSetsCost()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);

        run.ApplyUsage(10, 20, 0.01m);
        run.ApplyUsage(5, 7, 0.02m);

        Assert.Equal(15, run.PromptTokens);
        Assert.Equal(27, run.CompletionTokens);
        Assert.Equal(0.02m, run.CostEstimate);
    }
}
