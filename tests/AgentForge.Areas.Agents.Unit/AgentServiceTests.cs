using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public class AgentServiceTests
{
    private static AgentDefinition Definition(string name = "Builder") =>
        new(name, "Baut Dinge.", "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]);

    private static (AgentsDbContext Context, AgentService Service) NewService(AgentsDatabase database, IClock clock)
    {
        var context = database.NewContext();
        var service = new AgentService(context, database.CurrentUser, clock);
        return (context, service);
    }

    [Fact]
    public async Task CreateAsync_legt_einen_Agenten_an()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;

        var result = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Builder", result.Value!.Name);
        Assert.Equal("owner-1", result.Value.OwnerId);
        Assert.Equal(1, await context.Agents.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_lehnt_einen_vergebenen_Namen_ab()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        var result = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error!.Value.Kind);
        Assert.Equal("agent_name_taken", result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateAsync_erlaubt_den_Namen_eines_archivierten_Agenten()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        await service.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var result = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetAsync_meldet_einen_unbekannten_Agenten_als_nicht_gefunden()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;

        var result = await service.GetAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.NotFound, result.Error!.Value.Kind);
        Assert.Equal("agent_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task GetAsync_verbirgt_Agenten_fremder_Besitzer_als_nicht_gefunden()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        database.CurrentUser.OwnerId = "owner-2";
        var (otherContext, otherService) = NewService(database, TestClock.AtEpoch());
        await using var __ = otherContext;

        var result = await otherService.GetAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.NotFound, result.Error!.Value.Kind);
    }

    [Fact]
    public async Task GetAsync_findet_auch_archivierte_Agenten()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        await service.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var result = await service.GetAsync(created.Value.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsArchived);
    }

    [Fact]
    public async Task ListAsync_blendet_archivierte_aus_und_liefert_die_Gesamtzahl()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        await service.CreateAsync(Definition("Charlie"), TestContext.Current.CancellationToken);
        await service.CreateAsync(Definition("Alpha"), TestContext.Current.CancellationToken);
        var archived = await service.CreateAsync(Definition("Bravo"), TestContext.Current.CancellationToken);
        await service.ArchiveAsync(archived.Value!.Id, TestContext.Current.CancellationToken);

        var page = await service.ListAsync(PageRequest.From(0, 10), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.Total);
        Assert.Equal(["Alpha", "Charlie"], page.Items.Select(a => a.Name));
    }

    [Fact]
    public async Task ListAsync_beachtet_Skip_und_Take()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        foreach (var name in (string[])["Alpha", "Bravo", "Charlie"])
        {
            await service.CreateAsync(Definition(name), TestContext.Current.CancellationToken);
        }

        var page = await service.ListAsync(PageRequest.From(1, 1), TestContext.Current.CancellationToken);

        Assert.Equal(3, page.Total);
        Assert.Equal(["Bravo"], page.Items.Select(a => a.Name));
    }

    [Fact]
    public void PageRequest_begrenzt_unsinnige_Werte()
    {
        Assert.Equal(PageRequest.DefaultTake, PageRequest.From(null, null).Take);
        Assert.Equal(0, PageRequest.From(-5, null).Skip);
        Assert.Equal(PageRequest.MaxTake, PageRequest.From(0, 10_000).Take);
        Assert.Equal(1, PageRequest.From(0, 0).Take);
    }

    [Fact]
    public async Task UpdateAsync_aendert_den_Agenten_bei_passendem_Token()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var (context, service) = NewService(database, clock);
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));

        var result = await service.UpdateAsync(
            created.Value!.Id,
            Definition("Umbenannt"),
            created.Value.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Umbenannt", result.Value!.Name);
        Assert.Equal(clock.UtcNow, result.Value.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_lehnt_ein_veraltetes_Token_ab()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(
            created.Value!.Id,
            Definition("Umbenannt"),
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        Assert.Equal("concurrency_conflict", result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateAsync_lehnt_einen_fremden_Namen_ab()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        await service.CreateAsync(Definition("Alpha"), TestContext.Current.CancellationToken);
        var second = await service.CreateAsync(Definition("Bravo"), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(
            second.Value!.Id,
            Definition("Alpha"),
            second.Value.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.Equal("agent_name_taken", result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateAsync_erlaubt_den_eigenen_Namen()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition("Alpha"), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(
            created.Value!.Id,
            Definition("Alpha") with { Model = "another-model" },
            created.Value.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("another-model", result.Value!.Model);
    }

    [Fact]
    public async Task UpdateAsync_lehnt_einen_archivierten_Agenten_ab()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        var archived = await service.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(
            created.Value.Id,
            Definition("Umbenannt"),
            archived.Value!.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.Equal("agent_archived", result.Error!.Value.Code);
    }

    [Fact]
    public async Task ArchiveAsync_ist_wiederholbar()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        var first = await service.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);
        var second = await service.ArchiveAsync(created.Value.Id, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.ArchivedAt, second.Value!.ArchivedAt);
    }
}
