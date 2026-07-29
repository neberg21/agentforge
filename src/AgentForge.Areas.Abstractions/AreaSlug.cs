using System.Text.RegularExpressions;

namespace AgentForge.Areas.Abstractions;

public static partial class AreaSlug
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Pattern();

    public static bool IsValid(string slug) => !string.IsNullOrEmpty(slug) && Pattern().IsMatch(slug);

    public static void Validate(string slug)
    {
        if (!IsValid(slug))
        {
            throw new InvalidOperationException(
                $"Area slug '{slug}' is invalid: expected lowercase alphanumeric segments separated by single hyphens.");
        }
    }
}
