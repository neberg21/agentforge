namespace AgentForge.Areas.Agents.Application;

public sealed record PageRequest
{
    public const int DefaultTake = 50;
    public const int MaxTake = 200;

    private PageRequest(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }

    public int Skip { get; }

    public int Take { get; }

    public static PageRequest From(int? skip, int? take) =>
        new(Math.Max(0, skip ?? 0), Math.Clamp(take ?? DefaultTake, 1, MaxTake));
}

public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);
