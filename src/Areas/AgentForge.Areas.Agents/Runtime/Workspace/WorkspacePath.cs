namespace AgentForge.Areas.Agents.Runtime.Workspace;

public static class WorkspacePath
{
    public static bool TryResolve(string root, string relativeOrNested, out string fullPath, out string? error)
    {
        fullPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(root))
        {
            error = "root_required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(relativeOrNested))
        {
            error = "path_required";
            return false;
        }

        if (Path.IsPathRooted(relativeOrNested))
        {
            error = "absolute_path_rejected";
            return false;
        }

        var canonicalRoot = Path.GetFullPath(root);
        if (!canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            && !canonicalRoot.EndsWith(Path.AltDirectorySeparatorChar))
        {
            canonicalRoot += Path.DirectorySeparatorChar;
        }

        var combined = Path.Combine(root, relativeOrNested);
        var candidate = Path.GetFullPath(combined);

        if (!candidate.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            error = "path_outside_workspace";
            fullPath = string.Empty;
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
