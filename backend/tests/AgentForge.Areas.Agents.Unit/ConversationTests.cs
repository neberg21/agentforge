using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Unit;

public class ConversationTests
{
    [Fact]
    public void Create_WhenNoParticipants_Throws()
    {
        var now = DateTimeOffset.Parse("2026-07-30T12:00:00Z");

        var empty = Array.Empty<Guid>();
        Assert.Throws<ArgumentException>(() =>
            Conversation.Create("owner-1", "Chat", empty, now));
    }

    [Fact]
    public void Create_WhenCalled_SetsOwnerTitleParticipantsAndToken()
    {
        var now = DateTimeOffset.Parse("2026-07-30T12:00:00Z");
        var agentId = Guid.CreateVersion7();

        var conversation = Conversation.Create("owner-1", "D&D-Team", [agentId], now);

        Assert.Equal("owner-1", conversation.OwnerId);
        Assert.Equal("D&D-Team", conversation.Title);
        Assert.Equal(now, conversation.CreatedAt);
        Assert.Equal(now, conversation.UpdatedAt);
        Assert.Null(conversation.ArchivedAt);
        Assert.NotEqual(Guid.Empty, conversation.ConcurrencyToken);
        Assert.Single(conversation.Participants);
        Assert.Equal(agentId, conversation.Participants[0].AgentId);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void Archive_WhenCalled_SetsArchivedAtAndNewToken()
    {
        var now = DateTimeOffset.Parse("2026-07-30T12:00:00Z");
        var conversation = Conversation.Create("owner-1", "Chat", [Guid.CreateVersion7()], now);
        var tokenBefore = conversation.ConcurrencyToken;
        var archivedAt = now.AddMinutes(5);

        conversation.Archive(archivedAt);

        Assert.Equal(archivedAt, conversation.ArchivedAt);
        Assert.Equal(archivedAt, conversation.UpdatedAt);
        Assert.NotEqual(tokenBefore, conversation.ConcurrencyToken);
    }

    [Fact]
    public void AppendMessage_WhenCalled_IncrementsSequence()
    {
        var now = DateTimeOffset.Parse("2026-07-30T12:00:00Z");
        var conversation = Conversation.Create("owner-1", "Chat", [Guid.CreateVersion7()], now);

        var first = conversation.AppendMessage(
            MessageRole.User,
            "Hallo",
            now,
            senderAgentId: null,
            senderName: null,
            mentionsJson: "[\"a\"]",
            toolCallsJson: null,
            toolCallId: null);
        var second = conversation.AppendMessage(
            MessageRole.Assistant,
            "Hi",
            now.AddSeconds(1),
            senderAgentId: Guid.CreateVersion7(),
            senderName: "leo",
            mentionsJson: null,
            toolCallsJson: null,
            toolCallId: null);

        Assert.Equal(0, first.Sequence);
        Assert.Equal(1, second.Sequence);
        Assert.Equal(2, conversation.Messages.Count);
        Assert.Equal("Hallo", conversation.Messages[0].Content);
        Assert.Equal("leo", conversation.Messages[1].SenderName);
    }

    [Fact]
    public void Update_WhenTokenMismatch_Throws()
    {
        var now = DateTimeOffset.Parse("2026-07-30T12:00:00Z");
        var conversation = Conversation.Create("owner-1", "Chat", [Guid.CreateVersion7()], now);

        var wrongToken = Guid.CreateVersion7();
        var newParticipants = new[] { Guid.CreateVersion7() };
        Action act = () => conversation.Update("Neu", newParticipants, wrongToken, now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Update_WhenTokenMatches_ReplacesTitleAndParticipants()
    {
        var now = DateTimeOffset.Parse("2026-07-30T12:00:00Z");
        var firstAgent = Guid.CreateVersion7();
        var secondAgent = Guid.CreateVersion7();
        var conversation = Conversation.Create("owner-1", "Alt", [firstAgent], now);
        var token = conversation.ConcurrencyToken;
        var later = now.AddMinutes(1);

        conversation.Update("Neu", [secondAgent], token, later);

        Assert.Equal("Neu", conversation.Title);
        Assert.Equal(later, conversation.UpdatedAt);
        Assert.Single(conversation.Participants);
        Assert.Equal(secondAgent, conversation.Participants[0].AgentId);
        Assert.NotEqual(token, conversation.ConcurrencyToken);
    }
}
