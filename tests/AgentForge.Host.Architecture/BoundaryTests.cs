namespace AgentForge.Host.Architecture;

public class BoundaryTests
{
    [Fact]
    public void Es_gibt_mindestens_einen_Bereich()
    {
        Assert.NotEmpty(AreaAssemblies.All);
        Assert.NotEmpty(AreaAssemblies.AreaTypes);
    }

    [Fact]
    public void Kein_Bereich_referenziert_den_Host()
    {
        foreach (var assembly in AreaAssemblies.All)
        {
            var offenders = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name.Equals("AgentForge.Host", StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"{assembly.GetName().Name} referenziert den Host. Bereiche kennen den Host nicht.");
        }
    }

    [Fact]
    public void Kein_Bereich_referenziert_einen_anderen_Bereich_ausserhalb_von_Contracts()
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
                $"{ownName} referenziert {string.Join(", ", offenders)}. Bereiche sprechen nur ueber Contracts miteinander.");
        }
    }

    [Fact]
    public void Jede_Bereichs_Assembly_enthaelt_genau_eine_IArea_Implementierung()
    {
        foreach (var assembly in AreaAssemblies.All)
        {
            var implementations = assembly.GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IArea).IsAssignableFrom(type))
                .ToArray();

            Assert.True(
                implementations.Length == 1,
                $"{assembly.GetName().Name} enthaelt {implementations.Length} IArea-Implementierungen, erwartet ist genau eine.");
        }
    }

    [Fact]
    public void Alle_Slugs_sind_formgueltig_und_eindeutig()
    {
        var slugs = AreaAssemblies.AreaTypes
            .Select(type => ((IArea)Activator.CreateInstance(type)!).Slug)
            .ToArray();

        Assert.All(slugs, slug => Assert.True(AreaSlug.IsValid(slug), $"'{slug}' ist kein gueltiger Slug."));
        Assert.Equal(slugs.Length, slugs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Core_kennt_weder_AspNetCore_noch_EntityFramework()
    {
        var offenders = typeof(Result<>).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                        || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .ToArray();

        Assert.True(offenders.Length == 0, $"Core referenziert {string.Join(", ", offenders)}.");
    }
}
