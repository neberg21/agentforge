using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Unit;

public class ConversationTitleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-31T12:00:00Z");

    private static Conversation NewAuto()
    {
        var ids = new[] { Guid.CreateVersion7() };
        return Conversation.Create("owner-1", Conversation.DefaultAutoTitle, TitleMode.Auto, ids, Now);
    }

    [Fact]
    public void Create_WhenAuto_SetsModeAndZeroTurns()
    {
        var conversation = NewAuto();
        Assert.Equal(TitleMode.Auto, conversation.TitleMode);
        Assert.Equal(0, conversation.CompletedTurnCount);
        Assert.Null(conversation.TitleGeneratedAtTurn);
        Assert.Equal(Conversation.DefaultAutoTitle, conversation.Title);
    }

    [Fact]
    public void ShouldSuggestTitle_AfterFirstTurn_IsTrue()
    {
        var conversation = NewAuto();
        conversation.RecordCompletedTurn(Now);
        Assert.True(conversation.ShouldSuggestTitle());
    }

    [Fact]
    public void ShouldSuggestTitle_AtTurns2And3_IsFalse_ThenTrueAt4()
    {
        var conversation = NewAuto();
        conversation.RecordCompletedTurn(Now);
        Assert.True(conversation.ApplySuggestedTitle("First", Now));
        conversation.RecordCompletedTurn(Now);
        Assert.False(conversation.ShouldSuggestTitle());
        conversation.RecordCompletedTurn(Now);
        Assert.False(conversation.ShouldSuggestTitle());
        conversation.RecordCompletedTurn(Now);
        Assert.True(conversation.ShouldSuggestTitle());
    }

    [Fact]
    public void SetTitle_WhenAuto_Pauses()
    {
        var conversation = NewAuto();
        var token = conversation.ConcurrencyToken;
        conversation.SetTitle("Manual", token, Now);
        Assert.Equal("Manual", conversation.Title);
        Assert.Equal(TitleMode.Paused, conversation.TitleMode);
    }

    [Fact]
    public void LockTitle_WhenAuto_Locks()
    {
        var conversation = NewAuto();
        conversation.LockTitle(conversation.ConcurrencyToken, Now);
        Assert.Equal(TitleMode.Locked, conversation.TitleMode);
    }

    [Fact]
    public void ResumeAutoTitle_FromPaused_SetsAuto()
    {
        var conversation = NewAuto();
        conversation.SetTitle("Manual", conversation.ConcurrencyToken, Now);
        conversation.ResumeAutoTitle(conversation.ConcurrencyToken, Now);
        Assert.Equal(TitleMode.Auto, conversation.TitleMode);
    }

    [Fact]
    public void ApplySuggestedTitle_WhenPaused_ReturnsFalse()
    {
        var conversation = NewAuto();
        conversation.SetTitle("Manual", conversation.ConcurrencyToken, Now);
        Assert.False(conversation.ApplySuggestedTitle("Ignored", Now));
        Assert.Equal("Manual", conversation.Title);
    }

    [Fact]
    public void SetTitle_WhenLocked_StaysLocked()
    {
        var ids = new[] { Guid.CreateVersion7() };
        var conversation = Conversation.Create("owner-1", "Named", TitleMode.Locked, ids, Now);
        conversation.SetTitle("Renamed", conversation.ConcurrencyToken, Now);
        Assert.Equal("Renamed", conversation.Title);
        Assert.Equal(TitleMode.Locked, conversation.TitleMode);
    }
}
