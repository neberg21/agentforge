namespace AgentForge.Areas.Agents.Domain;

public static class RunTransitions
{
    private static readonly Dictionary<RunStatus, RunStatus[]> Allowed = new()
    {
        [RunStatus.Pending] = [RunStatus.Cancelled],
        [RunStatus.Running] = [],
        [RunStatus.Completed] = [],
        [RunStatus.Failed] = [],
        [RunStatus.Cancelled] = []
    };

    public static bool IsAllowed(RunStatus from, RunStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}
