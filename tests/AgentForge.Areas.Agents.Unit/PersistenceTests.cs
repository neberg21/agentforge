using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public class PersistenceTests
{
    private static AgentDefinition Definition(string name = "Builder") =>
        new(name, "Baut Dinge.", "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file", "write_file"]);

    private static Agent NewAgent(AgentsDatabase database, string name = "Builder") =>
        Agent.Create(database.CurrentUser.OwnerId, Definition(name), TestClock.AtEpoch().UtcNow);

    [Fact]
    public async Task Ein_Agent_ueberlebt_den_Rundlauf_durch_die_Datenbank()
    {
        using var database = new AgentsDatabase();
        var agent = NewAgent(database);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            var loaded = await context.Agents.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(agent.Id, loaded.Id);
            Assert.Equal("Builder", loaded.Name);
            Assert.Equal(["read_file", "write_file"], loaded.AllowedTools);
            Assert.Equal(agent.ConcurrencyToken, loaded.ConcurrencyToken);
        }
    }

    [Fact]
    public async Task Der_Snapshot_eines_Runs_ueberlebt_den_Rundlauf()
    {
        using var database = new AgentsDatabase();
        var agent = NewAgent(database);
        var run = Run.Create(agent, "Baue eine Todo-App.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            var loaded = await context.Runs.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Du bist hilfreich.", loaded.AgentSnapshot.SystemPrompt);
            Assert.Equal(["read_file", "write_file"], loaded.AgentSnapshot.AllowedTools);
            Assert.Equal(RunStatus.Pending, loaded.Status);
        }
    }

    [Fact]
    public async Task Zwei_aktive_Agenten_duerfen_nicht_denselben_Namen_tragen()
    {
        using var database = new AgentsDatabase();

        await using var context = database.NewContext();
        context.Agents.Add(NewAgent(database));
        context.Agents.Add(NewAgent(database));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Nach_dem_Archivieren_ist_der_Name_wieder_frei()
    {
        using var database = new AgentsDatabase();
        var first = NewAgent(database);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            first.Archive(TestClock.AtEpoch().UtcNow);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            context.Agents.Add(NewAgent(database));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, await context.Agents.CountAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Agenten_fremder_Besitzer_sind_unsichtbar()
    {
        using var database = new AgentsDatabase();

        await using (var context = database.NewContext())
        {
            context.Agents.Add(Agent.Create("owner-1", Definition("Meiner"), TestClock.AtEpoch().UtcNow));
            context.Agents.Add(Agent.Create("owner-2", Definition("Fremder"), TestClock.AtEpoch().UtcNow));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            var visible = await context.Agents.Select(a => a.Name).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(["Meiner"], visible);
        }

        database.CurrentUser.OwnerId = "owner-2";

        await using (var context = database.NewContext())
        {
            var visible = await context.Agents.Select(a => a.Name).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(["Fremder"], visible);
        }
    }

    [Fact]
    public async Task Nachrichten_verschwinden_mit_ihrem_Run()
    {
        using var database = new AgentsDatabase();
        var agent = NewAgent(database);
        var run = Run.Create(agent, "Baue eine Todo-App.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            context.Runs.Remove(await context.Runs.SingleAsync(TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Empty(await context.RunMessages.ToListAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Ein_Agent_mit_Runs_laesst_sich_nicht_loeschen()
    {
        using var database = new AgentsDatabase();
        var agent = NewAgent(database);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(Run.Create(agent, "Baue eine Todo-App.", TestClock.AtEpoch().UtcNow));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            context.Agents.Remove(await context.Agents.SingleAsync(TestContext.Current.CancellationToken));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Ein_veraltetes_Token_verhindert_das_Speichern()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();

        await using (var context = database.NewContext())
        {
            context.Agents.Add(NewAgent(database));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var first = database.NewContext();
        await using var second = database.NewContext();

        var fromFirst = await first.Agents.SingleAsync(TestContext.Current.CancellationToken);
        var fromSecond = await second.Agents.SingleAsync(TestContext.Current.CancellationToken);

        fromFirst.Update(Definition("Zuerst"), clock.Advance(TimeSpan.FromMinutes(1)));
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);

        fromSecond.Update(Definition("Danach"), clock.Advance(TimeSpan.FromMinutes(1)));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
