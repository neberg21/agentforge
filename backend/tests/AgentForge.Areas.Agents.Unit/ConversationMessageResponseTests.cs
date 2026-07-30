using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Http;

namespace AgentForge.Areas.Agents.Unit;

public class ConversationMessageResponseTests
{
    [Fact]
    public void From_WhenMentionsJsonPresent_MapsGuidArray()
    {
        var ownerId = "owner-1";
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var participantIds = new[] { Guid.CreateVersion7() };
        var conversation = Conversation.Create(ownerId, "c", participantIds, now);
        var agentId = Guid.CreateVersion7();
        var mentionsJson = $"[\"{agentId}\"]";
        var message = conversation.AppendMessage(
            MessageRole.User,
            "hi",
            now,
            senderAgentId: null,
            senderName: null,
            mentionsJson,
            toolCallsJson: null,
            toolCallId: null);

        var response = ConversationMessageResponse.From(message);

        Assert.NotNull(response.Mentions);
        Assert.Equal(agentId, response.Mentions![0]);
    }

    [Fact]
    public void From_WhenMentionsJsonNull_MapsNullMentions()
    {
        var ownerId = "owner-1";
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var participantIds = new[] { Guid.CreateVersion7() };
        var conversation = Conversation.Create(ownerId, "c", participantIds, now);
        var message = conversation.AppendMessage(
            MessageRole.User,
            "note",
            now,
            senderAgentId: null,
            senderName: null,
            mentionsJson: null,
            toolCallsJson: null,
            toolCallId: null);

        var response = ConversationMessageResponse.From(message);

        Assert.Null(response.Mentions);
    }
}
