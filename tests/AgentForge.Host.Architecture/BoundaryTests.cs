namespace AgentForge.Host.Architecture;

public class BoundaryTests
{
    [Fact]
    public void AreaPresence_WhenAssembliesScanned_FindsAtLeastOne()
    {
        Assert.NotEmpty(AreaAssemblies.All);
        Assert.NotEmpty(AreaAssemblies.AreaTypes);
    }

    [Fact]
    public void AreaIsolation_WhenReferencingHost_IsForbidden()
    {
        foreach (var assembly in AreaAssemblies.All)
        {
            var offenders = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name.Equals("AgentForge.Host", StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"{assembly.GetName().Name} references the host. Areas must not know the host.");
        }
    }

    [Fact]
    public void AreaIsolation_WhenReferencingOtherArea_IsForbiddenOutsideContracts()
    {
        foreach (var assembly in AreaAssemblies.All)
        {
            var ownName = assembly.GetName().Name!;

            var offenders = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name.StartsWith("AgentForge.Areas.", StringComparison.Ordinal))
                .Where(name => !name.Equals(ownName, StringComparison.Ordinal))
                .Where(name => !name.Equals("AgentForge.Areas.Abstractions", StringComparison.Ordinal))
                .Where(name => !name.EndsWith(".Contracts", StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"{ownName} references {string.Join(", ", offenders)}. Areas may only talk through Contracts.");
        }
    }

    [Fact]
    public void AreaContract_WhenAssemblyLoaded_ContainsExactlyOneIArea()
    {
        foreach (var assembly in AreaAssemblies.All)
        {
            var implementations = assembly.GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IArea).IsAssignableFrom(type))
                .ToArray();

            Assert.True(
                implementations.Length == 1,
                $"{assembly.GetName().Name} contains {implementations.Length} IArea implementations; exactly one is required.");
        }
    }

    [Fact]
    public void AreaSlug_WhenAreasRegistered_AreValidAndUnique()
    {
        var slugs = AreaAssemblies.AreaTypes
            .Select(type => ((IArea)Activator.CreateInstance(type)!).Slug)
            .ToArray();

        Assert.All(slugs, slug => Assert.True(AreaSlug.IsValid(slug), $"'{slug}' is not a valid slug."));
        Assert.Equal(slugs.Length, slugs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CorePurity_WhenInspectingReferences_ExcludesAspNetAndEf()
    {
        var offenders = typeof(Result<>).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                        || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .ToArray();

        Assert.True(offenders.Length == 0, $"Core references {string.Join(", ", offenders)}.");
    }
}
