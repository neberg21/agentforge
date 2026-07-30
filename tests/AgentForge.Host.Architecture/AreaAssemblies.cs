namespace AgentForge.Host.Architecture;

public static class AreaAssemblies
{
    public static IReadOnlyList<Assembly> All { get; } = Load();

    public static IReadOnlyList<Type> AreaTypes { get; } =
    [
        .. All
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IArea).IsAssignableFrom(type))
    ];

    private static List<Assembly> Load() =>
    [
        .. Directory
            .EnumerateFiles(AppContext.BaseDirectory, "AgentForge.Areas.*.dll")
            .Select(Assembly.LoadFrom)
            .Where(assembly => IsArea(assembly.GetName().Name!))
    ];

    private static bool IsArea(string name) =>
        !name.Equals("AgentForge.Areas.Abstractions", StringComparison.Ordinal)
        && !name.EndsWith(".Contracts", StringComparison.Ordinal);
}
