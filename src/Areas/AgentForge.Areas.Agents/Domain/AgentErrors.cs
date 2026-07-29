using AgentForge.Core;

namespace AgentForge.Areas.Agents.Domain;

public static class AgentErrors
{
    public static Error AgentNotFound(Guid id) =>
        new(ErrorKind.NotFound, "agent_not_found", $"Agent {id} wurde nicht gefunden.");

    public static Error RunNotFound(Guid id) =>
        new(ErrorKind.NotFound, "run_not_found", $"Run {id} wurde nicht gefunden.");

    public static Error NameTaken(string name) =>
        new(ErrorKind.Conflict, "agent_name_taken", $"Es gibt bereits einen Agenten mit dem Namen '{name}'.");

    public static Error AgentArchived(Guid id) =>
        new(ErrorKind.Conflict, "agent_archived", $"Agent {id} ist archiviert und nimmt keine neuen Runs an.");

    public static Error ConcurrencyConflict() =>
        new(ErrorKind.Conflict, "concurrency_conflict",
            "Der Datensatz wurde zwischenzeitlich geaendert. Lies ihn neu ein und versuche es erneut.");

    public static Error InvalidTransition(RunStatus from, RunStatus to) =>
        new(ErrorKind.Conflict, "run_invalid_transition", $"Ein Run im Status {from} kann nicht nach {to} wechseln.");
}
