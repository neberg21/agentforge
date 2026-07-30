using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Application;

public sealed class AgentService
{
    private readonly AgentsDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AgentService(AgentsDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<Agent>> CreateAsync(AgentDefinition definition, CancellationToken ct)
    {
        if (await NameIsTakenAsync(definition.Name, null, ct))
        {
            return AgentErrors.NameTaken(definition.Name);
        }

        var agent = Agent.Create(_currentUser.OwnerId, definition, _clock.UtcNow);
        _db.Agents.Add(agent);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return AgentErrors.NameTaken(definition.Name);
        }

        return agent;
    }

    public async Task<Result<Agent>> GetAsync(Guid id, CancellationToken ct)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        return agent is null ? AgentErrors.AgentNotFound(id) : agent;
    }

    public Task<Agent?> FindActiveByNameAsync(string name, CancellationToken ct) =>
        _db.Agents.FirstOrDefaultAsync(
            agent => agent.Name == name && agent.ArchivedAt == null,
            ct);

    public async Task<Page<Agent>> ListAsync(PageRequest page, string? q, CancellationToken ct)
    {
        var query = _db.Agents.Where(agent => agent.ArchivedAt == null);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(agent => EF.Functions.Like(agent.Name, "%" + EscapeLike(term) + "%"));
        }

        query = query.OrderBy(agent => agent.Name);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.Take).ToListAsync(ct);

        return new Page<Agent>(items, total, page.Skip, page.Take);
    }

    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);

    public async Task<Result<Agent>> UpdateAsync(
        Guid id,
        AgentDefinition definition,
        Guid concurrencyToken,
        CancellationToken ct)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (agent is null)
        {
            return AgentErrors.AgentNotFound(id);
        }

        if (agent.IsArchived)
        {
            return AgentErrors.AgentArchived(id);
        }

        if (agent.ConcurrencyToken != concurrencyToken)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        if (await NameIsTakenAsync(definition.Name, agent.Id, ct))
        {
            return AgentErrors.NameTaken(definition.Name);
        }

        agent.Update(definition, _clock.UtcNow);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentErrors.ConcurrencyConflict();
        }
        catch (DbUpdateException)
        {
            return AgentErrors.NameTaken(definition.Name);
        }

        return agent;
    }

    public async Task<Result<Agent>> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (agent is null)
        {
            return AgentErrors.AgentNotFound(id);
        }

        if (agent.IsArchived)
        {
            return agent;
        }

        agent.Archive(_clock.UtcNow);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        return agent;
    }

    private Task<bool> NameIsTakenAsync(string name, Guid? exceptId, CancellationToken ct) =>
        _db.Agents.AnyAsync(
            agent => agent.Name == name && agent.ArchivedAt == null && (exceptId == null || agent.Id != exceptId),
            ct);
}
