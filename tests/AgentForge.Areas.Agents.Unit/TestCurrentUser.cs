namespace AgentForge.Areas.Agents.Unit;

public sealed class TestCurrentUser : ICurrentUser
{
    public TestCurrentUser(string ownerId)
    {
        OwnerId = ownerId;
    }

    public string OwnerId { get; set; }
}
