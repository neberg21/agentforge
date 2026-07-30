namespace AgentForge.Areas.Agents.Unit;

public class AgentTests
{
    private static AgentDefinition Definition(string name = "Builder") =>
        new(name, "Baut Dinge.", "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]);

    [Fact]
    public void Create_WhenCalled_AppliesDefinitionAndTimestamps()
    {
        var clock = TestClock.AtEpoch();

        var agent = Agent.Create("owner-1", Definition(), clock.UtcNow);

        Assert.NotEqual(Guid.Empty, agent.Id);
        Assert.Equal("owner-1", agent.OwnerId);
        Assert.Equal("Builder", agent.Name);
        Assert.Equal("Du bist hilfreich.", agent.SystemPrompt);
        Assert.Equal(["read_file"], agent.AllowedTools);
        Assert.Equal(clock.UtcNow, agent.CreatedAt);
        Assert.Equal(clock.UtcNow, agent.UpdatedAt);
        Assert.Null(agent.ArchivedAt);
        Assert.False(agent.IsArchived);
        Assert.NotEqual(Guid.Empty, agent.ConcurrencyToken);
    }

    [Fact]
    public void Update_WhenCalled_ChangesFieldsTimestampAndToken()
    {
        var clock = TestClock.AtEpoch();
        var agent = Agent.Create("owner-1", Definition(), clock.UtcNow);
        var tokenBefore = agent.ConcurrencyToken;
        var createdAt = agent.CreatedAt;

        agent.Update(Definition("Renamed") with { Model = "other-model" }, clock.Advance(TimeSpan.FromMinutes(5)));

        Assert.Equal("Renamed", agent.Name);
        Assert.Equal("other-model", agent.Model);
        Assert.Equal(createdAt, agent.CreatedAt);
        Assert.Equal(clock.UtcNow, agent.UpdatedAt);
        Assert.NotEqual(tokenBefore, agent.ConcurrencyToken);
    }

    [Fact]
    public void Archive_WhenCalled_MarksArchivedWithoutRemoving()
    {
        var clock = TestClock.AtEpoch();
        var agent = Agent.Create("owner-1", Definition(), clock.UtcNow);

        agent.Archive(clock.Advance(TimeSpan.FromHours(1)));

        Assert.True(agent.IsArchived);
        Assert.Equal(clock.UtcNow, agent.ArchivedAt);
        Assert.Equal("Builder", agent.Name);
    }

    [Fact]
    public void ToSnapshot_WhenCalled_CopiesExecutionFields()
    {
        var agent = Agent.Create("owner-1", Definition(), TestClock.AtEpoch().UtcNow);

        var snapshot = agent.ToSnapshot();

        Assert.Equal(agent.Name, snapshot.Name);
        Assert.Equal(agent.SystemPrompt, snapshot.SystemPrompt);
        Assert.Equal(agent.Model, snapshot.Model);
        Assert.Equal(agent.Temperature, snapshot.Temperature);
        Assert.Equal(agent.MaxOutputTokens, snapshot.MaxOutputTokens);
        Assert.Equal(agent.MaxTurns, snapshot.MaxTurns);
        Assert.Equal(agent.AllowedTools, snapshot.AllowedTools);
    }

    [Fact]
    public void Create_WhenDefinitionToolsMutate_KeepsIndependentToolArray()
    {
        var clock = TestClock.AtEpoch();
        var tools = new[] { "read_file" };
        var agent = Agent.Create(
            "owner-1",
            new AgentDefinition("Builder", null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, tools),
            clock.UtcNow);

        tools[0] = "shell";

        Assert.Equal(["read_file"], agent.AllowedTools);
    }
}
