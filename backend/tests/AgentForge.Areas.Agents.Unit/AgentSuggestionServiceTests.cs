using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Persistence;

namespace AgentForge.Areas.Agents.Unit;

public class AgentSuggestionServiceTests
{
    private sealed class QueueNames : IAgentNameCandidateSource
    {
        private readonly Queue<string> _names;

        public QueueNames(params string[] names)
        {
            _names = new Queue<string>(names);
        }

        public string NextFirstName() => _names.Dequeue();
    }

    private static AgentDefinition Definition(string name) =>
        new(name, null, "prompt", "model", 0.5, 2048, 10, []);

    private static (
        AgentsDbContext Context,
        AgentService Agents,
        AgentSuggestionService Suggestions) NewServices(
        AgentsDatabase database,
        IClock clock,
        IAgentNameCandidateSource names)
    {
        var context = database.NewContext();
        var agents = new AgentService(context, database.CurrentUser, clock);
        var suggestions = new AgentSuggestionService(agents, names);
        return (context, agents, suggestions);
    }

    [Fact]
    public async Task SuggestNameAsync_WhenCandidateFree_ReturnsCandidate()
    {
        using var database = new AgentsDatabase();
        var names = new QueueNames("Lena");
        var (context, _, suggestions) = NewServices(database, TestClock.AtEpoch(), names);
        await using var _ = context;

        var name = await suggestions.SuggestNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Lena", name);
    }

    [Fact]
    public async Task SuggestNameAsync_WhenFirstTaken_ReturnsNextFree()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var names = new QueueNames("Lena", "Max");
        var (context, agents, suggestions) = NewServices(database, clock, names);
        await using var _ = context;
        await agents.CreateAsync(Definition("Lena"), TestContext.Current.CancellationToken);

        var name = await suggestions.SuggestNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Max", name);
    }

    [Fact]
    public async Task SuggestNameAsync_WhenRandomPoolExhausted_UsesNumericSuffix()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var taken = Enumerable.Repeat("Lena", AgentSuggestionService.MaxRandomAttempts).ToArray();
        var names = new QueueNames(taken);
        var (context, agents, suggestions) = NewServices(database, clock, names);
        await using var _ = context;
        await agents.CreateAsync(Definition("Lena"), TestContext.Current.CancellationToken);

        var name = await suggestions.SuggestNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Lena-2", name);
    }

    [Fact]
    public async Task SuggestNameAsync_WhenArchived_AllowsReuse()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var names = new QueueNames("Lena");
        var (context, agents, suggestions) = NewServices(database, clock, names);
        await using var _ = context;
        var created = await agents.CreateAsync(Definition("Lena"), TestContext.Current.CancellationToken);
        await agents.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var name = await suggestions.SuggestNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Lena", name);
    }
}
