namespace AgentForge.Areas.Agents.Unit;

public class RunTests
{
    private static Agent NewAgent(TestClock clock) =>
        Agent.Create("owner-1", new AgentDefinition("Builder", null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, []), clock.UtcNow);

    [Fact]
    public void Create_startet_im_Status_Pending()
    {
        var clock = TestClock.AtEpoch();
        var agent = NewAgent(clock);

        var run = Run.Create(agent, "Baue eine Todo-App.", clock.UtcNow);

        Assert.Equal(RunStatus.Pending, run.Status);
        Assert.Equal(agent.Id, run.AgentId);
        Assert.Equal("owner-1", run.OwnerId);
        Assert.Equal("Baue eine Todo-App.", run.Objective);
        Assert.Equal(clock.UtcNow, run.CreatedAt);
        Assert.Null(run.StartedAt);
        Assert.Null(run.CompletedAt);
        Assert.Null(run.Error);
        Assert.Null(run.PromptTokens);
        Assert.Null(run.CompletionTokens);
        Assert.Null(run.CostEstimate);
    }

    [Fact]
    public void Create_legt_System_und_User_Nachricht_an()
    {
        var clock = TestClock.AtEpoch();
        var agent = NewAgent(clock);

        var run = Run.Create(agent, "Baue eine Todo-App.", clock.UtcNow);

        Assert.Equal(2, run.Messages.Count);
        Assert.Equal(0, run.Messages[0].Sequence);
        Assert.Equal(MessageRole.System, run.Messages[0].Role);
        Assert.Equal("Du bist hilfreich.", run.Messages[0].Content);
        Assert.Equal(1, run.Messages[1].Sequence);
        Assert.Equal(MessageRole.User, run.Messages[1].Role);
        Assert.Equal("Baue eine Todo-App.", run.Messages[1].Content);
    }

    [Fact]
    public void Der_Snapshot_bleibt_unberuehrt_wenn_der_Agent_sich_aendert()
    {
        var clock = TestClock.AtEpoch();
        var agent = NewAgent(clock);
        var run = Run.Create(agent, "Baue eine Todo-App.", clock.UtcNow);

        agent.Update(
            new AgentDefinition("Builder", null, "Voellig anderer Prompt.", "another-model", 1.0, 512, 3, ["shell"]),
            clock.Advance(TimeSpan.FromMinutes(1)));

        Assert.Equal("Du bist hilfreich.", run.AgentSnapshot.SystemPrompt);
        Assert.Equal("some-model", run.AgentSnapshot.Model);
        Assert.Empty(run.AgentSnapshot.AllowedTools);
    }

    [Fact]
    public void Cancel_setzt_Status_Abschlusszeit_und_neues_Token()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);
        var tokenBefore = run.ConcurrencyToken;

        run.Cancel(clock.Advance(TimeSpan.FromSeconds(30)));

        Assert.Equal(RunStatus.Cancelled, run.Status);
        Assert.Equal(clock.UtcNow, run.CompletedAt);
        Assert.NotEqual(tokenBefore, run.ConcurrencyToken);
    }

    [Fact]
    public void Ein_abgebrochener_Run_laesst_sich_nicht_erneut_abbrechen()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);
        run.Cancel(clock.UtcNow);

        Assert.False(run.CanTransitionTo(RunStatus.Cancelled));
        Assert.Throws<InvalidOperationException>(() => run.Cancel(clock.UtcNow));
    }

    [Fact]
    public void AppendMessage_vergibt_fortlaufende_Sequenzen()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);

        run.AppendMessage(MessageRole.Assistant, "Alles klar.", clock.UtcNow);

        Assert.Equal([0, 1, 2], run.Messages.Select(m => m.Sequence));
    }

    [Fact]
    public void Eine_Werkzeugnachricht_ohne_ToolCallId_wird_abgelehnt()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);

        Assert.Throws<ArgumentException>(() => run.AppendMessage(MessageRole.Tool, "Ergebnis", clock.UtcNow));
    }

    [Fact]
    public void Nur_Werkzeugnachrichten_duerfen_eine_ToolCallId_tragen()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);

        Assert.Throws<ArgumentException>(
            () => run.AppendMessage(MessageRole.Assistant, "Text", clock.UtcNow, toolCallId: "call_1"));
    }
}
