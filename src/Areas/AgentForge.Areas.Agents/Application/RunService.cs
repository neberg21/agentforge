using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Queue;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Application;

public sealed class RunService
{
    private readonly AgentsDbContext _db;
    private readonly IClock _clock;
    private readonly IRunQueue _queue;

    public RunService(AgentsDbContext db, IClock clock, IRunQueue queue)
    {
        _db = db;
        _clock = clock;
        _queue = queue;
    }

    public async Task<Result<Run>> CreateAsync(Guid agentId, string objective, CancellationToken ct)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == agentId, ct);

        if (agent is null)
        {
            return AgentErrors.AgentNotFound(agentId);
        }

        if (agent.IsArchived)
        {
            return AgentErrors.AgentArchived(agentId);
        }

        var run = Run.Create(agent, objective, _clock.UtcNow);
        _db.Runs.Add(run);
        await _db.SaveChangesAsync(ct);
        _queue.Enqueue(run.Id);

        return run;
    }

    public async Task<Result<Run>> GetAsync(Guid id, CancellationToken ct)
    {
        var run = await _db.Runs.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        return run is null ? AgentErrors.RunNotFound(id) : run;
    }

    public async Task<Page<Run>> ListAsync(Guid? agentId, RunStatus? status, PageRequest page, CancellationToken ct)
    {
        var query = _db.Runs.AsQueryable();

        if (agentId is { } id)
        {
            query = query.Where(run => run.AgentId == id);
        }

        if (status is { } wanted)
        {
            query = query.Where(run => run.Status == wanted);
        }

        // Guid v7 ids are time-ordered; SQLite cannot ORDER BY DateTimeOffset directly.
        var ordered = query.OrderByDescending(run => run.Id);

        var total = await ordered.CountAsync(ct);
        var items = await ordered.Skip(page.Skip).Take(page.Take).ToListAsync(ct);

        return new Page<Run>(items, total, page.Skip, page.Take);
    }

    public async Task<Result<Run>> CancelAsync(Guid id, Guid concurrencyToken, CancellationToken ct)
    {
        var run = await _db.Runs.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (run is null)
        {
            return AgentErrors.RunNotFound(id);
        }

        if (run.ConcurrencyToken != concurrencyToken)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        if (!run.CanTransitionTo(RunStatus.Cancelled))
        {
            return AgentErrors.InvalidTransition(run.Status, RunStatus.Cancelled);
        }

        run.Cancel(_clock.UtcNow);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        return run;
    }

    public async Task<Result<IReadOnlyList<RunMessage>>> GetMessagesAsync(Guid runId, CancellationToken ct)
    {
        if (!await _db.Runs.AnyAsync(run => run.Id == runId, ct))
        {
            return AgentErrors.RunNotFound(runId);
        }

        var messages = await _db.RunMessages
            .Where(message => message.RunId == runId)
            .OrderBy(message => message.Sequence)
            .ToListAsync(ct);

        return Result<IReadOnlyList<RunMessage>>.Success(messages);
    }
}
