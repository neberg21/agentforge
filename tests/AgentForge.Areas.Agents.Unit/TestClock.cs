namespace AgentForge.Areas.Agents.Unit;

public sealed class TestClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public static TestClock AtEpoch() => new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public DateTimeOffset Advance(TimeSpan by)
    {
        UtcNow = UtcNow.Add(by);
        return UtcNow;
    }
}
