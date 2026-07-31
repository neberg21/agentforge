using System.Text.Json;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Queue;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Application;

public sealed class ConversationService
{
    private readonly AgentsDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IConversationReplyQueue _replyQueue;
    private readonly IConversationEventBus _events;
    private readonly ILlmClient _llm;

    public ConversationService(
        AgentsDbContext db,
        ICurrentUser currentUser,
        IClock clock,
        IConversationReplyQueue replyQueue,
        IConversationEventBus events,
        ILlmClient llm)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _replyQueue = replyQueue;
        _events = events;
        _llm = llm;
    }

    public async Task<Result<Conversation>> CreateAsync(
        string? title,
        IReadOnlyList<Guid> participantAgentIds,
        CancellationToken ct) =>
        await CreateAsync(title, participantAgentIds, initialSystemMessage: null, ct);

    public async Task<Result<Conversation>> CreateAsync(
        string? title,
        IReadOnlyList<Guid> participantAgentIds,
        string? initialSystemMessage,
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
        var conversation = Conversation.Create(
            _currentUser.OwnerId,
            resolvedTitle,
            TitleMode.Locked,
            agentIds,
            _clock.UtcNow);
        if (!string.IsNullOrWhiteSpace(initialSystemMessage))
        {
            var systemMessage = conversation.AppendMessage(
                MessageRole.System,
                initialSystemMessage.Trim(),
                _clock.UtcNow,
                senderAgentId: null,
                senderName: null,
                mentionsJson: null,
                toolCallsJson: null,
                toolCallId: null);
            _db.ConversationMessages.Add(systemMessage);
        }

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
            var last = conversation.Messages
                .Where(message => message.Role != MessageRole.System)
                .OrderByDescending(message => message.Sequence)
                .FirstOrDefault();
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

    public async Task<Result<Guid>> PostMessageAsync(
        Guid id,
        string content,
        IReadOnlyList<Guid> mentions,
        CancellationToken ct)
    {
        var conversation = await _db.Conversations
            .Include(candidate => candidate.Participants)
            .Include(candidate => candidate.Messages)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (conversation is null)
        {
            return AgentErrors.ConversationNotFound(id);
        }

        if (conversation.IsArchived)
        {
            return AgentErrors.ConversationArchived(id);
        }

        var participantIds = conversation.Participants.Select(participant => participant.AgentId).ToHashSet();
        foreach (var mention in mentions)
        {
            if (!participantIds.Contains(mention))
            {
                return AgentErrors.MentionNotParticipant();
            }
        }

        var mentionsJson = mentions.Count == 0
            ? null
            : JsonSerializer.Serialize(mentions);
        var message = conversation.AppendMessage(
            MessageRole.User,
            content,
            _clock.UtcNow,
            senderAgentId: null,
            senderName: null,
            mentionsJson,
            toolCallsJson: null,
            toolCallId: null);
        _db.ConversationMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        var streamId = Guid.CreateVersion7();
        if (mentions.Count == 0)
        {
            var done = new ConversationEvent(id, RunEventType.Done, "{}");
            _events.Publish(done);
            return streamId;
        }

        var job = new ConversationReplyJob(id, streamId, mentions.ToArray());
        _replyQueue.Enqueue(job);
        return streamId;
    }

    public async Task<Result<DraftRunProposal>> DraftRunAsync(
        Guid id,
        Guid? preferredAgentId,
        CancellationToken ct)
    {
        var detail = await GetAsync(id, ct);
        if (!detail.IsSuccess)
        {
            return detail.Error!.Value;
        }

        var conversation = detail.Value!.Conversation;
        if (conversation.IsArchived)
        {
            return AgentErrors.ConversationArchived(id);
        }

        var participants = detail.Value.Participants;
        ConversationParticipantInfo? chosen = null;
        if (preferredAgentId is { } preferred)
        {
            chosen = participants.FirstOrDefault(participant => participant.AgentId == preferred);
            if (chosen is null)
            {
                return AgentErrors.MentionNotParticipant();
            }
        }
        else
        {
            chosen = participants[0];
        }

        var agent = await _db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == chosen.AgentId, ct);
        if (agent is null)
        {
            return AgentErrors.AgentNotFound(chosen.AgentId);
        }

        var history = await GetMessagesAsync(id, ct);
        if (!history.IsSuccess)
        {
            return history.Error!.Value;
        }

        var transcript = string.Join(
            "\n",
            history.Value!.Select(message => $"{message.Role}: {message.Content}"));
        var system = "Propose a single concrete run objective from the conversation. Reply with only the objective text.";
        var user = "Conversation:\n" + transcript;
        var messages = new List<LlmMessage>
        {
            new("system", system, null, null),
            new("user", user, null, null)
        };
        var request = new LlmCompletionRequest(
            agent.Model,
            agent.Temperature,
            Math.Min(agent.MaxOutputTokens, 2048),
            messages,
            Array.Empty<string>());
        var completion = await _llm.CompleteAsync(request, ct);
        var objective = string.IsNullOrWhiteSpace(completion.Content)
            ? "Complete the planned work from the conversation."
            : completion.Content.Trim();
        var proposal = new DraftRunProposal(objective, chosen.AgentId);
        return proposal;
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
