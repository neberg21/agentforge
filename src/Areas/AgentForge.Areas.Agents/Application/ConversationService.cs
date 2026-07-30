using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Application;

public sealed class ConversationService
{
    private readonly AgentsDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ConversationService(AgentsDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<Conversation>> CreateAsync(
        string? title,
        IReadOnlyList<Guid> participantAgentIds,
        CancellationToken ct)
    {
        var agentsResult = await LoadActiveParticipantsAsync(participantAgentIds, ct);
        if (!agentsResult.IsSuccess)
        {
            return agentsResult.Error!.Value;
        }

        var agents = agentsResult.Value!;
        var orderedNames = participantAgentIds
            .Distinct()
            .Select(id => agents.First(agent => agent.Id == id).Name);
        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? string.Join(", ", orderedNames)
            : title.Trim();

        var agentIds = participantAgentIds.Distinct().ToArray();
        var conversation = Conversation.Create(_currentUser.OwnerId, resolvedTitle, agentIds, _clock.UtcNow);
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(ct);
        return conversation;
    }

    public async Task<Result<ConversationDetail>> GetAsync(Guid id, CancellationToken ct)
    {
        var conversation = await _db.Conversations
            .Include(candidate => candidate.Participants)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (conversation is null)
        {
            return AgentErrors.ConversationNotFound(id);
        }

        var participants = await ResolveParticipantNamesAsync(conversation, ct);
        var detail = new ConversationDetail(conversation, participants);
        return detail;
    }

    public async Task<Page<ConversationListItem>> ListAsync(PageRequest page, CancellationToken ct)
    {
        var query = _db.Conversations
            .Include(conversation => conversation.Participants)
            .Include(conversation => conversation.Messages)
            .Where(conversation => conversation.ArchivedAt == null)
            .OrderByDescending(conversation => conversation.Id);

        var total = await query.CountAsync(ct);
        var conversations = await query.Skip(page.Skip).Take(page.Take).ToListAsync(ct);

        var items = new List<ConversationListItem>(conversations.Count);
        foreach (var conversation in conversations)
        {
            var participants = await ResolveParticipantNamesAsync(conversation, ct);
            var last = conversation.Messages.OrderByDescending(message => message.Sequence).FirstOrDefault();
            var excerpt = last?.Content is null
                ? null
                : last.Content.Length <= 120
                    ? last.Content
                    : last.Content[..120];
            var item = new ConversationListItem(conversation, participants, excerpt, last?.CreatedAt);
            items.Add(item);
        }

        return new Page<ConversationListItem>(items, total, page.Skip, page.Take);
    }

    public async Task<Result<Conversation>> UpdateAsync(
        Guid id,
        string title,
        IReadOnlyList<Guid> participantAgentIds,
        Guid concurrencyToken,
        CancellationToken ct)
    {
        var conversation = await _db.Conversations
            .Include(candidate => candidate.Participants)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (conversation is null)
        {
            return AgentErrors.ConversationNotFound(id);
        }

        if (conversation.IsArchived)
        {
            return AgentErrors.ConversationArchived(id);
        }

        if (conversation.ConcurrencyToken != concurrencyToken)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        var agentsResult = await LoadActiveParticipantsAsync(participantAgentIds, ct);
        if (!agentsResult.IsSuccess)
        {
            return agentsResult.Error!.Value;
        }

        var agentIds = agentsResult.Value!.Select(agent => agent.Id).ToArray();
        try
        {
            conversation.Update(title.Trim(), agentIds, concurrencyToken, _clock.UtcNow);
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException)
        {
            return AgentErrors.ConcurrencyConflict();
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        return conversation;
    }

    public async Task<Result<Conversation>> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var conversation = await _db.Conversations.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (conversation is null)
        {
            return AgentErrors.ConversationNotFound(id);
        }

        if (conversation.IsArchived)
        {
            return conversation;
        }

        conversation.Archive(_clock.UtcNow);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        return conversation;
    }

    public async Task<Result<IReadOnlyList<ConversationMessage>>> GetMessagesAsync(Guid id, CancellationToken ct)
    {
        var exists = await _db.Conversations.AnyAsync(candidate => candidate.Id == id, ct);
        if (!exists)
        {
            return AgentErrors.ConversationNotFound(id);
        }

        var messages = await _db.ConversationMessages
            .Where(message => message.ConversationId == id)
            .OrderBy(message => message.Sequence)
            .ToListAsync(ct);

        return messages;
    }

    private async Task<Result<IReadOnlyList<Agent>>> LoadActiveParticipantsAsync(
        IReadOnlyList<Guid> participantAgentIds,
        CancellationToken ct)
    {
        if (participantAgentIds.Count == 0)
        {
            return new Error(ErrorKind.Validation, "validation", "At least one participant is required.");
        }

        var distinctIds = participantAgentIds.Distinct().ToArray();
        var agents = await _db.Agents
            .Where(agent => distinctIds.Contains(agent.Id))
            .ToListAsync(ct);

        foreach (var agentId in distinctIds)
        {
            var agent = agents.FirstOrDefault(candidate => candidate.Id == agentId);
            if (agent is null)
            {
                return AgentErrors.AgentNotFound(agentId);
            }

            if (agent.IsArchived)
            {
                return AgentErrors.AgentArchived(agentId);
            }
        }

        return agents;
    }

    private async Task<IReadOnlyList<ConversationParticipantInfo>> ResolveParticipantNamesAsync(
        Conversation conversation,
        CancellationToken ct)
    {
        var agentIds = conversation.Participants.Select(participant => participant.AgentId).ToArray();
        var agents = await _db.Agents
            .IgnoreQueryFilters()
            .Where(agent => agentIds.Contains(agent.Id))
            .ToDictionaryAsync(agent => agent.Id, ct);

        var infos = new List<ConversationParticipantInfo>(agentIds.Length);
        foreach (var agentId in agentIds)
        {
            var name = agents.TryGetValue(agentId, out var agent) ? agent.Name : agentId.ToString("N");
            var info = new ConversationParticipantInfo(agentId, name);
            infos.Add(info);
        }

        return infos;
    }
}
