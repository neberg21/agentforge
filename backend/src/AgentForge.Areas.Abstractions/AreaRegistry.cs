namespace AgentForge.Areas.Abstractions;

public sealed class AreaRegistry
{
    private readonly List<IArea> _areas = [];

    public IReadOnlyList<IArea> Areas => _areas;

    public void Add(IArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        AreaSlug.Validate(area.Slug);

        if (_areas.Any(existing => string.Equals(existing.Slug, area.Slug, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Area slug '{area.Slug}' is already registered.");
        }

        _areas.Add(area);
    }
}
