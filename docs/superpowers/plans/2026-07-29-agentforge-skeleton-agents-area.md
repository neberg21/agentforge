# AgentForge — Monorepo-Skelett und Agents-Bereich: Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein lauffähiges .NET-10-Monorepo mit erzwungener Area-Konvention und einem Agents-Bereich, in dem Agent-Definitionen verwaltet und Runs als Datensätze geführt werden — ohne Sprachmodell, ohne Container.

**Architecture:** Ein einziger ASP.NET-Core-Host als Composition Root. Fachliche Bereiche sind Klassenbibliotheken, die genau ein `IArea` implementieren, sich beim Host explizit registrieren und unter `/api/{slug}` gemountet werden. Bereiche kennen einander nicht; ein Architekturtest erzwingt das. Persistenz je Bereich über einen eigenen `DbContext` mit Slug-präfigierten Tabellen und globalem `OwnerId`-Filter. Den konkreten Datenbankprovider reicht der Host über `IDbProvider` durch, sodass kein Bereich ihn kennt.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core mit SQLite, xUnit v3, Scalar für die OpenAPI-Oberfläche.

**Spec:** `docs/superpowers/specs/2026-07-29-agentforge-skeleton-agents-area-design.md`

## Global Constraints

Diese gelten für jede Aufgabe, auch wenn sie dort nicht wiederholt werden.

- Repo-Wurzel: `C:\Users\NEWA002\source\repos\agentforge`. Alle Pfade sind relativ dazu, alle Befehle werden von dort ausgeführt.
- Auf Repo-Ebene existieren nur `src`, `tests`, `docs` sowie `AgentForge.sln`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `NuGet.config` und `.gitignore`. Kein weiteres Verzeichnis wird angelegt.
- Pakete kommen ausschließlich von nuget.org. Die repo-lokale `NuGet.config` löscht geerbte Quellen und bildet alle Pakete auf nuget.org ab. Ohne sie schlägt bei mehreren registrierten Quellen jeder Restore mit `NU1507` fehl, sobald Central Package Management aktiv ist.
- Zielframework `net10.0` für alle Projekte.
- `Directory.Build.props` setzt für alle Projekte: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`.
- Central Package Management ist aktiv. Paketversionen werden **niemals von Hand geraten**, sondern ausschließlich über `dotnet add package <Name>` hinzugefügt; das SDK trägt die aufgelöste Version in `Directory.Packages.props` ein.
- Testprojekte heißen `<Projekt>.<Testart>`: `AgentForge.Core.Unit`, `AgentForge.Areas.Agents.Unit`, `AgentForge.Host.Integration`, `AgentForge.Host.Architecture`.
- Tests laufen auf xUnit v3. Keine Assertion-Bibliothek, keine Mocking-Bibliothek. Wo eine Attrappe nötig ist, wird eine kleine handgeschriebene Klasse im Testprojekt verwendet.
- Alle Ids entstehen über `Guid.CreateVersion7()`.
- Alle Zeitstempel sind `DateTimeOffset` in UTC und stammen aus `IClock`. Außerhalb von `SystemClock` wird `DateTimeOffset.UtcNow` nicht verwendet.
- Tabellennamen tragen den Slug ihres Bereichs als Präfix: `agents_agent`, `agents_run`, `agents_run_message`.
- Alle Fehlerantworten sind ProblemDetails nach RFC 9457.
- Nach jeder Aufgabe wird committet. **Commit-Nachrichten auf Englisch**, Präfix `feat:`, `test:` oder `chore:`.

## Abweichungen von der Spec

Zwei Stellen sind gegenüber der Spec bewusst enger gefasst; beide sind unten in den Aufgaben so umgesetzt und hier zusammengefasst, damit sie niemanden überraschen.

1. **Provider-Umschaltung.** `Database:Provider` existiert als Konfiguration, akzeptiert in dieser Ausbaustufe aber nur `sqlite`. Der Wert `postgres` bricht den Start mit einer klaren Meldung ab, statt ungetesteten Code auszuführen. Das Npgsql-Paket wird noch nicht hinzugefügt.
2. **Eingabevalidierung.** Statt der in .NET 10 eingebauten Minimal-API-Validierung kommt ein eigener, dreißig Zeilen langer Endpoint-Filter über `System.ComponentModel.DataAnnotations.Validator` zum Einsatz. Ergebnis und Fehlerformat sind identisch, die Umsetzung hängt aber an keiner Quellcodegenerierung.
3. **Bereichskonfiguration.** Die Spec verlangt, dass jeder Bereich einen eigenen Abschnitt `Areas:<Name>:*` auf eine typisierte Options-Klasse bindet. Der Agents-Bereich hat in dieser Ausbaustufe nichts zu konfigurieren, deshalb entsteht keine leere Options-Klasse. Die erste echte Einstellung kommt mit Teilprojekt 3 (Basisadresse, Modell, Zeitlimits); die Regel gilt ab dann.
4. **Fünf statt vier Testprojekte.** Zusätzlich zu den in der Spec genannten Projekten entsteht `AgentForge.Areas.Abstractions.Unit` für Slug-Prüfung, Registry und Fehlerabbildung. Das Namensmuster `<Projekt>.<Testart>` bleibt gewahrt; die Alternative wäre gewesen, diese Prüfungen in ein Integrationsprojekt zu schieben, wo sie nicht hingehören.

## File Structure

**`src/AgentForge.Core/`** — keine Paketabhängigkeiten, kennt weder ASP.NET noch EF.
- `Result.cs` — `Result<T>`, `Error`, `ErrorKind`. Trägt fachliche Fehler ohne Ausnahmen.
- `IClock.cs`, `SystemClock.cs` — kontrollierbare Zeit.
- `ICurrentUser.cs` — liefert die `OwnerId`. Die Implementierung liegt bewusst im Host.

**`src/AgentForge.Areas.Abstractions/`** — Framework-Referenz auf ASP.NET Core, Paketabhängigkeit auf EF Core.
- `IArea.cs` — der Bereichsvertrag.
- `AreaSlug.cs` — Slug-Validierung.
- `AreaRegistry.cs` — Liste der registrierten Bereiche, prüft Eindeutigkeit.
- `AreaRegistration.cs` — `AddArea<T>`, `MapAreas`, `MigrateAreasAsync`, `AreaPolicies`.
- `IDbProvider.cs` — der Host reicht die Providerwahl an die Bereiche durch.
- `ResultExtensions.cs` — Übersetzung `Result<T>` → HTTP.
- `ValidationFilter.cs` — DataAnnotations-Prüfung als Endpoint-Filter.

**`src/Areas/AgentForge.Areas.Agents/`** — referenziert nur `Core` und `Abstractions`.
- `AgentsArea.cs`, `AgentsOptions.cs`
- `Domain/` — `Agent.cs`, `Run.cs`, `RunMessage.cs`, `RunStatus.cs`, `MessageRole.cs`, `AgentSnapshot.cs`, `RunTransitions.cs`, `AgentErrors.cs`
- `Persistence/` — `AgentsDbContext.cs`, `EntityConfigurations.cs`
- `Application/` — `AgentService.cs`, `RunService.cs`, `Commands.cs`
- `Http/` — `Requests.cs`, `Responses.cs`, `AgentEndpoints.cs`, `RunEndpoints.cs`

**`src/AgentForge.Host/`** — die einzige Stelle, die alles kennt.
- `Program.cs`, `HostEndpoints.cs`, `GlobalExceptionHandler.cs`, `DatabaseOptions.cs`, `ConfiguredDbProvider.cs`, `LocalUserOptions.cs`, `LocalSingleUser.cs`, `appsettings.json`, `appsettings.Development.json`

Die Aufteilung folgt der Verantwortung, nicht der technischen Schicht: Domäne, Persistenzabbildung, Anwendungsfälle und HTTP-Oberfläche des Agents-Bereichs liegen im selben Projekt, weil sie sich gemeinsam ändern.

---

### Task 1: Repo-Grundgerüst und Core-Bausteine

**Files:**
- Create: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `NuGet.config`, `.gitignore`, `AgentForge.sln`
- Create: `src/AgentForge.Core/Result.cs`, `src/AgentForge.Core/Clock.cs`, `src/AgentForge.Core/ICurrentUser.cs`
- Test: `tests/AgentForge.Core.Unit/ResultTests.cs`

**Interfaces:**
- Consumes: nichts.
- Produces: `AgentForge.Core.Result<T>` mit `IsSuccess`, `T? Value`, `Error? Error`, `Match<TOut>(Func<T,TOut>, Func<Error,TOut>)` und impliziten Konvertierungen aus `T` und `Error`. `AgentForge.Core.Error` als `readonly record struct Error(ErrorKind Kind, string Code, string Message)`. `AgentForge.Core.ErrorKind` mit `NotFound`, `Conflict`, `Validation`. `AgentForge.Core.IClock` mit `DateTimeOffset UtcNow { get; }` und `SystemClock`. `AgentForge.Core.ICurrentUser` mit `string OwnerId { get; }`.

- [ ] **Step 1: Solution und Ignorierliste anlegen**

```bash
dotnet new sln -n AgentForge
dotnet new gitignore
printf '\n# AgentForge\n.data/\nworkspaces/\n' >> .gitignore
dotnet new install xunit.v3.templates
```

`workspaces/` wird nicht angelegt, nur vorsorglich ignoriert.

- [ ] **Step 2: `global.json` schreiben**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 3: `Directory.Build.props` schreiben**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: `Directory.Packages.props` schreiben**

Die `ItemGroup` bleibt leer; `dotnet add package` füllt sie.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
  </ItemGroup>
</Project>
```

- [ ] **Step 4b: `NuGet.config` schreiben**

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

Auf diesem Rechner sind mehrere NuGet-Quellen registriert. Zusammen mit Central Package Management verlangt NuGet dann Package Source Mapping und bricht sonst jeden Restore mit `NU1507` ab — auch bei Projekten ohne eine einzige Paketreferenz. Das `<clear />` entfernt die geerbten Quellen für dieses Repo, die Abbildung macht die Herkunft ausdrücklich. Ohne diese Datei schlägt jeder `dotnet build`, `dotnet test` und `dotnet add package` in allen zehn Aufgaben fehl.

- [ ] **Step 5: Projekte anlegen und verdrahten**

```bash
dotnet new classlib -o src/AgentForge.Core
rm src/AgentForge.Core/Class1.cs
dotnet new xunit3 -o tests/AgentForge.Core.Unit
dotnet sln add src/AgentForge.Core tests/AgentForge.Core.Unit
dotnet add tests/AgentForge.Core.Unit reference src/AgentForge.Core
dotnet build
```

Erwartet: Build erfolgreich. Schlägt er fehl, weil die xUnit-Vorlage `Version`-Attribute direkt ins `.csproj` geschrieben hat und Central Package Management das verbietet: entferne die `Version`-Attribute aus `tests/AgentForge.Core.Unit/AgentForge.Core.Unit.csproj` und trage die Pakete über `dotnet add package xunit.v3`, `dotnet add package xunit.runner.visualstudio` und `dotnet add package Microsoft.NET.Test.Sdk` im Testprojekt nach, damit die Versionen in `Directory.Packages.props` landen. Dieselbe Korrektur gilt für jedes weitere Testprojekt in diesem Plan.

- [ ] **Step 6: Den fehlschlagenden Test schreiben**

`tests/AgentForge.Core.Unit/ResultTests.cs`:

```csharp
namespace AgentForge.Core.Unit;

public class ResultTests
{
    [Fact]
    public void Success_traegt_den_Wert_und_keinen_Fehler()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_traegt_den_Fehler_und_keinen_Wert()
    {
        Result<int> result = new Error(ErrorKind.NotFound, "agent_not_found", "Nicht gefunden.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Value.Kind);
        Assert.Equal("agent_not_found", result.Error!.Value.Code);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void Match_waehlt_den_passenden_Zweig()
    {
        Result<int> ok = 7;
        Result<int> bad = new Error(ErrorKind.Conflict, "conflict", "Konflikt.");

        Assert.Equal("7", ok.Match(v => v.ToString(), e => e.Code));
        Assert.Equal("conflict", bad.Match(v => v.ToString(), e => e.Code));
    }
}
```

Ergänze in `tests/AgentForge.Core.Unit/AgentForge.Core.Unit.csproj` ein globales Using, damit `AgentForge.Core` ohne `using`-Zeile sichtbar ist:

```xml
  <ItemGroup>
    <Using Include="AgentForge.Core" />
  </ItemGroup>
```

- [ ] **Step 7: Test laufen lassen und Fehlschlag prüfen**

Run: `dotnet test tests/AgentForge.Core.Unit`
Erwartet: FAIL — Kompilierfehler, `Result<>`, `Error` und `ErrorKind` sind unbekannt.

- [ ] **Step 8: `src/AgentForge.Core/Result.cs` schreiben**

```csharp
namespace AgentForge.Core;

public enum ErrorKind
{
    NotFound,
    Conflict,
    Validation
}

public readonly record struct Error(ErrorKind Kind, string Code, string Message);

public readonly struct Result<T>
{
    private Result(T value)
    {
        Value = value;
        Error = null;
    }

    private Result(Error error)
    {
        Value = default;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        Error is { } error ? onFailure(error) : onSuccess(Value!);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
```

- [ ] **Step 9: `src/AgentForge.Core/Clock.cs` und `src/AgentForge.Core/ICurrentUser.cs` schreiben**

`Clock.cs`:

```csharp
namespace AgentForge.Core;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

`ICurrentUser.cs`:

```csharp
namespace AgentForge.Core;

public interface ICurrentUser
{
    string OwnerId { get; }
}
```

- [ ] **Step 10: Tests laufen lassen**

Run: `dotnet test tests/AgentForge.Core.Unit`
Erwartet: PASS, drei Tests.

- [ ] **Step 11: Committen**

```bash
git add -A
git commit -m "feat: repo scaffolding and core building blocks"
```

---

### Task 2: Area-Abstraktion

**Files:**
- Create: `src/AgentForge.Areas.Abstractions/{IArea,AreaSlug,AreaRegistry,AreaRegistration,IDbProvider,ResultExtensions,ValidationFilter}.cs`
- Test: `tests/AgentForge.Areas.Abstractions.Unit/{AreaSlugTests,AreaRegistryTests,ResultExtensionsTests}.cs`

**Interfaces:**
- Consumes: `AgentForge.Core.Result<T>`, `AgentForge.Core.Error`, `AgentForge.Core.ErrorKind` aus Task 1.
- Produces: `IArea` mit `string Slug`, `void ConfigureServices(IServiceCollection, IConfiguration)`, `void MapEndpoints(IEndpointRouteBuilder)`, `Task MigrateAsync(IServiceProvider, CancellationToken)`. `AreaSlug.IsValid(string)` und `AreaSlug.Validate(string)`. `AreaRegistry` mit `IReadOnlyList<IArea> Areas` und `void Add(IArea)`. `AreaRegistration.AddArea<TArea>(this WebApplicationBuilder)`, `MapAreas(this WebApplication)`, `MigrateAreasAsync(this WebApplication, CancellationToken)`. `AreaPolicies.AreaAccess` als Konstante `"area-access"`. `IDbProvider.Apply(DbContextOptionsBuilder)`. `ResultExtensions.ToHttpResult<T>(this Result<T>, Func<T, IResult>)` und `ToProblem(this Error)`. `ValidationFilter<T>` als `IEndpointFilter`.

- [ ] **Step 1: Projekte anlegen und Pakete hinzufügen**

```bash
dotnet new classlib -o src/AgentForge.Areas.Abstractions
rm src/AgentForge.Areas.Abstractions/Class1.cs
dotnet new xunit3 -o tests/AgentForge.Areas.Abstractions.Unit
dotnet sln add src/AgentForge.Areas.Abstractions tests/AgentForge.Areas.Abstractions.Unit
dotnet add src/AgentForge.Areas.Abstractions reference src/AgentForge.Core
dotnet add tests/AgentForge.Areas.Abstractions.Unit reference src/AgentForge.Areas.Abstractions
dotnet add src/AgentForge.Areas.Abstractions package Microsoft.EntityFrameworkCore
```

Ergänze in `src/AgentForge.Areas.Abstractions/AgentForge.Areas.Abstractions.csproj` die Framework-Referenz, damit ASP.NET-Typen verfügbar sind:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```

Ergänze im Testprojekt `tests/AgentForge.Areas.Abstractions.Unit/AgentForge.Areas.Abstractions.Unit.csproj`:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <Using Include="AgentForge.Core" />
    <Using Include="AgentForge.Areas.Abstractions" />
  </ItemGroup>
```

- [ ] **Step 2: Die fehlschlagenden Tests schreiben**

`tests/AgentForge.Areas.Abstractions.Unit/AreaSlugTests.cs`:

```csharp
namespace AgentForge.Areas.Abstractions.Unit;

public class AreaSlugTests
{
    [Theory]
    [InlineData("agents")]
    [InlineData("dnd")]
    [InlineData("agent-runtime")]
    [InlineData("a1")]
    public void Gueltige_Slugs_werden_akzeptiert(string slug) => Assert.True(AreaSlug.IsValid(slug));

    [Theory]
    [InlineData("")]
    [InlineData("Agents")]
    [InlineData("agents_area")]
    [InlineData("-agents")]
    [InlineData("agents-")]
    [InlineData("agents--area")]
    [InlineData("agents/runs")]
    public void Ungueltige_Slugs_werden_abgelehnt(string slug) => Assert.False(AreaSlug.IsValid(slug));

    [Fact]
    public void Validate_wirft_bei_ungueltigem_Slug()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => AreaSlug.Validate("Agents"));
        Assert.Contains("Agents", exception.Message, StringComparison.Ordinal);
    }
}
```

`tests/AgentForge.Areas.Abstractions.Unit/AreaRegistryTests.cs`:

```csharp
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Abstractions.Unit;

public class AreaRegistryTests
{
    private sealed class StubArea(string slug) : IArea
    {
        public string Slug { get; } = slug;

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

        public void MapEndpoints(IEndpointRouteBuilder routes) { }

        public Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void Add_nimmt_verschiedene_Bereiche_auf()
    {
        var registry = new AreaRegistry();

        registry.Add(new StubArea("agents"));
        registry.Add(new StubArea("dnd"));

        Assert.Equal(["agents", "dnd"], registry.Areas.Select(a => a.Slug));
    }

    [Fact]
    public void Add_lehnt_doppelte_Slugs_ab()
    {
        var registry = new AreaRegistry();
        registry.Add(new StubArea("agents"));

        var exception = Assert.Throws<InvalidOperationException>(() => registry.Add(new StubArea("agents")));

        Assert.Contains("agents", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_lehnt_ungueltige_Slugs_ab() =>
        Assert.Throws<InvalidOperationException>(() => new AreaRegistry().Add(new StubArea("Agents")));
}
```

`tests/AgentForge.Areas.Abstractions.Unit/ResultExtensionsTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentForge.Areas.Abstractions.Unit;

public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Validation, StatusCodes.Status400BadRequest)]
    public void Fehlerarten_werden_auf_Statuscodes_abgebildet(ErrorKind kind, int expectedStatus)
    {
        var result = new Error(kind, "some_code", "Beschreibung.").ToProblem();

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.Equal("Beschreibung.", problem.ProblemDetails.Detail);
        Assert.Equal("some_code", Assert.Contains("code", problem.ProblemDetails.Extensions));
    }

    [Fact]
    public void ToHttpResult_ruft_den_Erfolgszweig()
    {
        Result<int> result = 5;

        var httpResult = result.ToHttpResult(value => TypedResults.Ok(value));

        Assert.Equal(5, Assert.IsType<Ok<int>>(httpResult).Value);
    }

    [Fact]
    public void ToHttpResult_uebersetzt_den_Fehlerzweig()
    {
        Result<int> result = new Error(ErrorKind.NotFound, "missing", "Weg.");

        var httpResult = result.ToHttpResult(value => TypedResults.Ok(value));

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(httpResult).StatusCode);
    }
}
```

- [ ] **Step 3: Tests laufen lassen und Fehlschlag prüfen**

Run: `dotnet test tests/AgentForge.Areas.Abstractions.Unit`
Erwartet: FAIL — Kompilierfehler, `IArea`, `AreaSlug`, `AreaRegistry` und `ResultExtensions` sind unbekannt.

- [ ] **Step 4: `IArea.cs` und `IDbProvider.cs` schreiben**

`src/AgentForge.Areas.Abstractions/IArea.cs`:

```csharp
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Abstractions;

public interface IArea
{
    string Slug { get; }

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder routes);

    Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken);
}
```

`src/AgentForge.Areas.Abstractions/IDbProvider.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Abstractions;

public interface IDbProvider
{
    void Apply(DbContextOptionsBuilder options);
}
```

- [ ] **Step 5: `AreaSlug.cs` und `AreaRegistry.cs` schreiben**

`src/AgentForge.Areas.Abstractions/AreaSlug.cs`:

```csharp
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
```

`src/AgentForge.Areas.Abstractions/AreaRegistry.cs`:

```csharp
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
```

- [ ] **Step 6: `ResultExtensions.cs` und `ValidationFilter.cs` schreiben**

`src/AgentForge.Areas.Abstractions/ResultExtensions.cs`:

```csharp
using AgentForge.Core;
using Microsoft.AspNetCore.Http;

namespace AgentForge.Areas.Abstractions;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, ToProblem);

    public static IResult ToProblem(this Error error)
    {
        var (status, title) = error.Kind switch
        {
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Nicht gefunden"),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Konflikt"),
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Ungültige Anfrage"),
            _ => (StatusCodes.Status500InternalServerError, "Unerwarteter Fehler")
        };

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
```

`src/AgentForge.Areas.Abstractions/ValidationFilter.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AgentForge.Areas.Abstractions;

public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<T>().FirstOrDefault() is not { } model)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [string.Empty] = ["Der Anfragerumpf fehlt oder ist nicht lesbar."]
            });
        }

        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            return await next(context);
        }

        var errors = results
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty), (r, member) => (member, r.ErrorMessage))
            .GroupBy(entry => entry.member, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.ErrorMessage ?? "Ungültiger Wert.").ToArray(),
                StringComparer.Ordinal);

        return TypedResults.ValidationProblem(errors);
    }
}
```

- [ ] **Step 7: `AreaRegistration.cs` schreiben**

`src/AgentForge.Areas.Abstractions/AreaRegistration.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Abstractions;

public static class AreaPolicies
{
    public const string AreaAccess = "area-access";
}

public static class AreaRegistration
{
    public static WebApplicationBuilder AddAreaSupport(this WebApplicationBuilder builder)
    {
        GetOrCreateRegistry(builder.Services);
        return builder;
    }

    public static WebApplicationBuilder AddArea<TArea>(this WebApplicationBuilder builder)
        where TArea : IArea, new()
    {
        var registry = GetOrCreateRegistry(builder.Services);
        var area = new TArea();
        registry.Add(area);
        area.ConfigureServices(builder.Services, builder.Configuration);
        return builder;
    }

    public static WebApplication MapAreas(this WebApplication app)
    {
        foreach (var area in app.Services.GetRequiredService<AreaRegistry>().Areas)
        {
            var group = app.MapGroup($"/api/{area.Slug}")
                .RequireAuthorization(AreaPolicies.AreaAccess)
                .WithTags(area.Slug);

            area.MapEndpoints(group);
        }

        return app;
    }

    public static async Task MigrateAreasAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();

        foreach (var area in app.Services.GetRequiredService<AreaRegistry>().Areas)
        {
            await area.MigrateAsync(scope.ServiceProvider, cancellationToken);
        }
    }

    private static AreaRegistry GetOrCreateRegistry(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(AreaRegistry))?.ImplementationInstance is AreaRegistry existing)
        {
            return existing;
        }

        var registry = new AreaRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
```

- [ ] **Step 8: Tests laufen lassen**

Run: `dotnet test tests/AgentForge.Areas.Abstractions.Unit`
Erwartet: PASS, 20 Tests.

- [ ] **Step 9: Committen**

```bash
git add -A
git commit -m "feat: area abstraction with slug validation, registry and result mapping"
```

---

### Task 3: Host-Bootstrap

**Files:**
- Create: `src/AgentForge.Host/{Program,HostEndpoints,GlobalExceptionHandler,DatabaseOptions,ConfiguredDbProvider,LocalUserOptions,LocalSingleUser}.cs`
- Create: `src/AgentForge.Host/appsettings.json`, `src/AgentForge.Host/appsettings.Development.json`
- Test: `tests/AgentForge.Host.Integration/{AgentForgeFactory,HostEndpointTests}.cs`

**Interfaces:**
- Consumes: `IArea`, `AreaRegistry`, `AreaRegistration.MapAreas`, `AreaRegistration.MigrateAreasAsync`, `AreaPolicies.AreaAccess`, `IDbProvider` aus Task 2; `IClock`, `SystemClock`, `ICurrentUser` aus Task 1.
- Produces: `public partial class Program` im globalen Namensraum als Einstiegspunkt für `WebApplicationFactory<Program>`. `AgentForge.Host.DatabaseOptions` mit `SectionName = "Database"`, `Sqlite = "sqlite"`, `Provider`, `ConnectionString`. `AgentForge.Host.LocalUserOptions` mit `SectionName = "Auth"` und `LocalOwnerId`. `AgentForge.Host.AreaInfo` als `record AreaInfo(string Slug)`. Testinfrastruktur `AgentForge.Host.Integration.AgentForgeFactory` mit `CreateClient()` und `IServiceProvider Services`.

- [ ] **Step 1: Projekte anlegen und Pakete hinzufügen**

```bash
dotnet new web -o src/AgentForge.Host
dotnet new xunit3 -o tests/AgentForge.Host.Integration
dotnet sln add src/AgentForge.Host tests/AgentForge.Host.Integration
dotnet add src/AgentForge.Host reference src/AgentForge.Core src/AgentForge.Areas.Abstractions
dotnet add tests/AgentForge.Host.Integration reference src/AgentForge.Host
dotnet add src/AgentForge.Host package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/AgentForge.Host package Microsoft.AspNetCore.OpenApi
dotnet add src/AgentForge.Host package Scalar.AspNetCore
dotnet add tests/AgentForge.Host.Integration package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/AgentForge.Host.Integration package Microsoft.EntityFrameworkCore.Sqlite
```

Ergänze in `tests/AgentForge.Host.Integration/AgentForge.Host.Integration.csproj`:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <Using Include="System.Net" />
    <Using Include="System.Net.Http.Json" />
    <Using Include="AgentForge.Areas.Abstractions" />
  </ItemGroup>
```

- [ ] **Step 2: Die Testfactory schreiben**

`tests/AgentForge.Host.Integration/AgentForgeFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentForge.Host.Integration;

public sealed class AgentForgeFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public AgentForgeFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "sqlite",
                ["Database:ConnectionString"] = "Data Source=:memory:",
                ["Auth:LocalOwnerId"] = "test-owner"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbProvider>();
            services.AddSingleton<IDbProvider>(new SharedConnectionDbProvider(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private sealed class SharedConnectionDbProvider(SqliteConnection connection) : IDbProvider
    {
        public void Apply(DbContextOptionsBuilder options) => options.UseSqlite(connection);
    }
}
```

Die Verbindung bleibt für die Lebensdauer der Factory offen. Eine SQLite-Datenbank im Modus `:memory:` verschwindet, sobald die letzte Verbindung geschlossen wird; ohne dieses Offenhalten wäre das Schema nach dem ersten Test weg.

- [ ] **Step 3: Die fehlschlagenden Integrationstests schreiben**

`tests/AgentForge.Host.Integration/HostEndpointTests.cs`:

```csharp
namespace AgentForge.Host.Integration;

public class HostEndpointTests(AgentForgeFactory factory) : IClassFixture<AgentForgeFactory>
{
    [Fact]
    public async Task Liveness_antwortet_mit_200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_antwortet_mit_200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Areas_liefert_die_registrierten_Bereiche()
    {
        using var client = factory.CreateClient();

        var areas = await client.GetFromJsonAsync<AreaInfo[]>("/api/areas", TestContext.Current.CancellationToken);

        Assert.NotNull(areas);
        Assert.Empty(areas);
    }

    [Fact]
    public async Task Unbekannter_Pfad_antwortet_mit_404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/gibt-es-nicht", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

In dieser Aufgabe ist noch kein Bereich registriert, deshalb ist die Liste leer. Task 8 ersetzt diese Erwartung durch den Agents-Bereich.

- [ ] **Step 4: Tests laufen lassen und Fehlschlag prüfen**

Run: `dotnet test tests/AgentForge.Host.Integration`
Erwartet: FAIL — Kompilierfehler, `AreaInfo` ist unbekannt und `Program` besitzt keine öffentliche Teilklasse.

- [ ] **Step 5: Konfiguration, Datenbankprovider und Benutzer schreiben**

`src/AgentForge.Host/DatabaseOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace AgentForge.Host;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string Sqlite = "sqlite";

    [Required]
    public string Provider { get; set; } = Sqlite;

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
```

`src/AgentForge.Host/ConfiguredDbProvider.cs`:

```csharp
using AgentForge.Areas.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgentForge.Host;

public sealed class ConfiguredDbProvider(IOptions<DatabaseOptions> options) : IDbProvider
{
    public void Apply(DbContextOptionsBuilder builder)
    {
        var connectionString = options.Value.ConnectionString;
        EnsureDataDirectoryExists(connectionString);
        builder.UseSqlite(connectionString);
    }

    private static void EnsureDataDirectoryExists(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

        if (string.IsNullOrEmpty(dataSource) || dataSource.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
```

`src/AgentForge.Host/LocalUserOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace AgentForge.Host;

public sealed class LocalUserOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string LocalOwnerId { get; set; } = "local";
}
```

`src/AgentForge.Host/LocalSingleUser.cs`:

```csharp
using AgentForge.Core;
using Microsoft.Extensions.Options;

namespace AgentForge.Host;

public sealed class LocalSingleUser(IOptions<LocalUserOptions> options) : ICurrentUser
{
    public string OwnerId => options.Value.LocalOwnerId;
}
```

- [ ] **Step 6: Ausnahmebehandlung und Host-Endpunkte schreiben**

`src/AgentForge.Host/GlobalExceptionHandler.cs`:

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AgentForge.Host;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path} (trace {TraceId})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Unerwarteter Fehler",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Die Anfrage konnte nicht verarbeitet werden."
            }
        });
    }
}
```

Die Ausnahme selbst verlässt den Prozess nicht; nach außen geht nur die Ablaufverfolgungs-Id, die auch im Protokoll steht.

`src/AgentForge.Host/HostEndpoints.cs`:

```csharp
using AgentForge.Areas.Abstractions;

namespace AgentForge.Host;

public sealed record AreaInfo(string Slug);

public static class HostEndpoints
{
    public static WebApplication MapHostEndpoints(this WebApplication app)
    {
        app.MapGet("/_health", () => TypedResults.Ok(new { status = "ok" }))
            .WithName("Liveness");

        app.MapHealthChecks("/_health/ready");

        app.MapGet("/api/areas", (AreaRegistry registry) =>
                TypedResults.Ok(registry.Areas.Select(area => new AreaInfo(area.Slug)).ToArray()))
            .WithName("Areas");

        return app;
    }
}
```

- [ ] **Step 7: `Program.cs` schreiben**

`src/AgentForge.Host/Program.cs`:

```csharp
using AgentForge.Areas.Abstractions;
using AgentForge.Core;
using AgentForge.Host;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddOptions<LocalUserOptions>()
    .Bind(builder.Configuration.GetSection(LocalUserOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<ICurrentUser, LocalSingleUser>();

builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => string.Equals(options.Provider, DatabaseOptions.Sqlite, StringComparison.OrdinalIgnoreCase),
        "Database:Provider unterstuetzt in dieser Ausbaustufe nur 'sqlite'. 'postgres' folgt mit der Umstellung auf Neon.")
    .ValidateOnStart();
builder.Services.AddSingleton<IDbProvider, ConfiguredDbProvider>();

builder.AddAreaSupport();

builder.Services.AddAuthorization(options =>
    options.AddPolicy(AreaPolicies.AreaAccess, policy => policy.RequireAssertion(_ => true)));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHostEndpoints();
app.MapAreas();

await app.MigrateAreasAsync();

await app.RunAsync();

public partial class Program;
```

Die Autorisierungs-Policy stimmt hier bedingungslos zu. Sie existiert trotzdem, weil sie der einzige Ort ist, den der spätere Wechsel auf echte Anmeldung anfassen muss.

- [ ] **Step 8: `appsettings.json` und `appsettings.Development.json` schreiben**

`src/AgentForge.Host/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Auth": {
    "LocalOwnerId": "local"
  },
  "Database": {
    "Provider": "sqlite",
    "ConnectionString": "Data Source=.data/agentforge.db"
  }
}
```

`src/AgentForge.Host/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

- [ ] **Step 9: Tests laufen lassen**

Run: `dotnet test tests/AgentForge.Host.Integration`
Erwartet: PASS, vier Tests. (Die Umsetzung ergänzte hier zusätzlich einen Test für die Ablehnung von `Database:Provider=postgres` sowie zwei Tests in `AgentForge.Areas.Abstractions.Unit`, die die Reihenfolgeunabhängigkeit von `AddAreaSupport` und `AddArea<T>` festhalten.)

Schlägt `Readiness_antwortet_mit_200` mit einem Fehler zur Authentifizierung fehl, fehlt der Grund nicht in der Policy, sondern in der Reihenfolge: `MapHealthChecks` darf keine Autorisierung verlangen. Prüfe, dass `/_health/ready` außerhalb von `MapAreas` gemountet ist.

- [ ] **Step 10: Den Host einmal von Hand starten**

```bash
dotnet run --project src/AgentForge.Host
```

Erwartet: Der Host startet ohne Fehler. Rufe `http://localhost:<port>/_health` und `http://localhost:<port>/scalar/v1` auf, dann beende mit Strg+C.

`.data/agentforge.db` entsteht hier noch **nicht**. `ConfiguredDbProvider.Apply` wird erst aufgerufen, wenn ein Bereich einen `DbContext` registriert — das passiert in Task 8. Bis dahin ist die Provider-Verdrahtung vorhanden, aber unbenutzt.

- [ ] **Step 11: Committen**

```bash
git add -A
git commit -m "feat: host bootstrap with health endpoints, problem details and area mounting"
```

---

### Task 4: Domänenmodell des Agents-Bereichs

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Domain/{RunStatus,MessageRole,AgentSnapshot,AgentDefinition,Agent,Run,RunMessage,RunTransitions,AgentErrors}.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/{TestClock,AgentTests,RunTests,RunTransitionsTests}.cs`

**Interfaces:**
- Consumes: `AgentForge.Core.Error`, `AgentForge.Core.ErrorKind` aus Task 1.
- Produces (alle im Namensraum `AgentForge.Areas.Agents.Domain`):
  - `enum RunStatus { Pending, Running, Completed, Failed, Cancelled }`
  - `enum MessageRole { System, User, Assistant, Tool }`
  - `record AgentSnapshot(string Name, string SystemPrompt, string Model, double Temperature, int MaxOutputTokens, int MaxTurns, string[] AllowedTools)`
  - `record AgentDefinition(string Name, string? Description, string SystemPrompt, string Model, double Temperature, int MaxOutputTokens, int MaxTurns, string[] AllowedTools)`
  - `Agent` mit `Create(string ownerId, AgentDefinition definition, DateTimeOffset now)`, `Update(AgentDefinition definition, DateTimeOffset now)`, `Archive(DateTimeOffset now)`, `ToSnapshot()`, `bool IsArchived` und den Konstanten `DefaultTemperature = 0.7`, `DefaultMaxOutputTokens = 4096`, `DefaultMaxTurns = 20`
  - `Run` mit `Create(Agent agent, string objective, DateTimeOffset now)`, `AppendMessage(MessageRole, string?, DateTimeOffset, string? toolCallsJson = null, string? toolCallId = null)`, `CanTransitionTo(RunStatus)`, `Cancel(DateTimeOffset now)`, `IReadOnlyList<RunMessage> Messages`
  - `RunMessage` mit `Sequence`, `Role`, `Content`, `ToolCallsJson`, `ToolCallId`
  - `RunTransitions.IsAllowed(RunStatus from, RunStatus to)`
  - `AgentErrors` mit `AgentNotFound(Guid)`, `NameTaken(string)`, `AgentArchived(Guid)`, `ConcurrencyConflict()`, `RunNotFound(Guid)`, `InvalidTransition(RunStatus, RunStatus)`

- [ ] **Step 1: Projekte anlegen und Pakete hinzufügen**

```bash
dotnet new classlib -o src/Areas/AgentForge.Areas.Agents
rm src/Areas/AgentForge.Areas.Agents/Class1.cs
dotnet new xunit3 -o tests/AgentForge.Areas.Agents.Unit
dotnet sln add src/Areas/AgentForge.Areas.Agents tests/AgentForge.Areas.Agents.Unit
dotnet add src/Areas/AgentForge.Areas.Agents reference src/AgentForge.Core src/AgentForge.Areas.Abstractions
dotnet add tests/AgentForge.Areas.Agents.Unit reference src/Areas/AgentForge.Areas.Agents
dotnet add src/Areas/AgentForge.Areas.Agents package Microsoft.EntityFrameworkCore
dotnet add src/Areas/AgentForge.Areas.Agents package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
dotnet add tests/AgentForge.Areas.Agents.Unit package Microsoft.EntityFrameworkCore.Sqlite
```

Das SQLite-Paket im Testprojekt wird erst in Task 5 gebraucht, wird aber hier schon aufgenommen, damit die Projektdatei nur einmal angefasst wird.

Ergänze in `src/Areas/AgentForge.Areas.Agents/AgentForge.Areas.Agents.csproj`:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```

Ergänze in `tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj`:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <Using Include="AgentForge.Core" />
    <Using Include="AgentForge.Areas.Agents.Domain" />
  </ItemGroup>
```

- [ ] **Step 2: Die Testuhr schreiben**

`tests/AgentForge.Areas.Agents.Unit/TestClock.cs`:

```csharp
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
```

- [ ] **Step 3: Die fehlschlagenden Tests schreiben**

`tests/AgentForge.Areas.Agents.Unit/AgentTests.cs`:

```csharp
namespace AgentForge.Areas.Agents.Unit;

public class AgentTests
{
    private static AgentDefinition Definition(string name = "Builder") =>
        new(name, "Baut Dinge.", "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]);

    [Fact]
    public void Create_uebernimmt_die_Definition_und_setzt_Zeitstempel()
    {
        var clock = TestClock.AtEpoch();

        var agent = Agent.Create("owner-1", Definition(), clock.UtcNow);

        Assert.NotEqual(Guid.Empty, agent.Id);
        Assert.Equal("owner-1", agent.OwnerId);
        Assert.Equal("Builder", agent.Name);
        Assert.Equal("Du bist hilfreich.", agent.SystemPrompt);
        Assert.Equal(["read_file"], agent.AllowedTools);
        Assert.Equal(clock.UtcNow, agent.CreatedAt);
        Assert.Equal(clock.UtcNow, agent.UpdatedAt);
        Assert.Null(agent.ArchivedAt);
        Assert.False(agent.IsArchived);
        Assert.NotEqual(Guid.Empty, agent.ConcurrencyToken);
    }

    [Fact]
    public void Update_aendert_Felder_Zeitstempel_und_Token()
    {
        var clock = TestClock.AtEpoch();
        var agent = Agent.Create("owner-1", Definition(), clock.UtcNow);
        var tokenBefore = agent.ConcurrencyToken;
        var createdAt = agent.CreatedAt;

        agent.Update(Definition("Renamed") with { Model = "other-model" }, clock.Advance(TimeSpan.FromMinutes(5)));

        Assert.Equal("Renamed", agent.Name);
        Assert.Equal("other-model", agent.Model);
        Assert.Equal(createdAt, agent.CreatedAt);
        Assert.Equal(clock.UtcNow, agent.UpdatedAt);
        Assert.NotEqual(tokenBefore, agent.ConcurrencyToken);
    }

    [Fact]
    public void Archive_markiert_den_Agenten_ohne_ihn_zu_entfernen()
    {
        var clock = TestClock.AtEpoch();
        var agent = Agent.Create("owner-1", Definition(), clock.UtcNow);

        agent.Archive(clock.Advance(TimeSpan.FromHours(1)));

        Assert.True(agent.IsArchived);
        Assert.Equal(clock.UtcNow, agent.ArchivedAt);
        Assert.Equal("Builder", agent.Name);
    }

    [Fact]
    public void Der_Agent_teilt_sein_Werkzeug_Array_nicht_mit_der_Definition()
    {
        var clock = TestClock.AtEpoch();
        var tools = new[] { "read_file" };
        var agent = Agent.Create(
            "owner-1",
            new AgentDefinition("Builder", null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, tools),
            clock.UtcNow);

        tools[0] = "shell";

        Assert.Equal(["read_file"], agent.AllowedTools);
    }

    [Fact]
    public void ToSnapshot_kopiert_die_ausfuehrungsrelevanten_Felder()
    {
        var agent = Agent.Create("owner-1", Definition(), TestClock.AtEpoch().UtcNow);

        var snapshot = agent.ToSnapshot();

        Assert.Equal(agent.Name, snapshot.Name);
        Assert.Equal(agent.SystemPrompt, snapshot.SystemPrompt);
        Assert.Equal(agent.Model, snapshot.Model);
        Assert.Equal(agent.Temperature, snapshot.Temperature);
        Assert.Equal(agent.MaxOutputTokens, snapshot.MaxOutputTokens);
        Assert.Equal(agent.MaxTurns, snapshot.MaxTurns);
        Assert.Equal(agent.AllowedTools, snapshot.AllowedTools);
    }
}
```

`tests/AgentForge.Areas.Agents.Unit/RunTests.cs`:

```csharp
namespace AgentForge.Areas.Agents.Unit;

public class RunTests
{
    private static Agent NewAgent(TestClock clock) =>
        Agent.Create("owner-1", new AgentDefinition("Builder", null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, []), clock.UtcNow);

    [Fact]
    public void Create_startet_im_Status_Pending()
    {
        var clock = TestClock.AtEpoch();
        var agent = NewAgent(clock);

        var run = Run.Create(agent, "Baue eine Todo-App.", clock.UtcNow);

        Assert.Equal(RunStatus.Pending, run.Status);
        Assert.Equal(agent.Id, run.AgentId);
        Assert.Equal("owner-1", run.OwnerId);
        Assert.Equal("Baue eine Todo-App.", run.Objective);
        Assert.Equal(clock.UtcNow, run.CreatedAt);
        Assert.Null(run.StartedAt);
        Assert.Null(run.CompletedAt);
        Assert.Null(run.Error);
        Assert.Null(run.PromptTokens);
        Assert.Null(run.CompletionTokens);
        Assert.Null(run.CostEstimate);
    }

    [Fact]
    public void Create_legt_System_und_User_Nachricht_an()
    {
        var clock = TestClock.AtEpoch();
        var agent = NewAgent(clock);

        var run = Run.Create(agent, "Baue eine Todo-App.", clock.UtcNow);

        Assert.Equal(2, run.Messages.Count);
        Assert.Equal(0, run.Messages[0].Sequence);
        Assert.Equal(MessageRole.System, run.Messages[0].Role);
        Assert.Equal("Du bist hilfreich.", run.Messages[0].Content);
        Assert.Equal(1, run.Messages[1].Sequence);
        Assert.Equal(MessageRole.User, run.Messages[1].Role);
        Assert.Equal("Baue eine Todo-App.", run.Messages[1].Content);
    }

    [Fact]
    public void Der_Snapshot_bleibt_unberuehrt_wenn_der_Agent_sich_aendert()
    {
        var clock = TestClock.AtEpoch();
        var agent = NewAgent(clock);
        var run = Run.Create(agent, "Baue eine Todo-App.", clock.UtcNow);

        agent.Update(
            new AgentDefinition("Builder", null, "Voellig anderer Prompt.", "another-model", 1.0, 512, 3, ["shell"]),
            clock.Advance(TimeSpan.FromMinutes(1)));

        Assert.Equal("Du bist hilfreich.", run.AgentSnapshot.SystemPrompt);
        Assert.Equal("some-model", run.AgentSnapshot.Model);
        Assert.Empty(run.AgentSnapshot.AllowedTools);
    }

    [Fact]
    public void Der_Snapshot_teilt_sein_Werkzeug_Array_nicht_mit_dem_Agenten()
    {
        var clock = TestClock.AtEpoch();
        var agent = Agent.Create(
            "owner-1",
            new AgentDefinition("Builder", null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]),
            clock.UtcNow);
        var run = Run.Create(agent, "Baue eine Todo-App.", clock.UtcNow);

        agent.AllowedTools[0] = "shell";

        Assert.Equal(["read_file"], run.AgentSnapshot.AllowedTools);
    }

    [Fact]
    public void Cancel_setzt_Status_Abschlusszeit_und_neues_Token()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);
        var tokenBefore = run.ConcurrencyToken;

        run.Cancel(clock.Advance(TimeSpan.FromSeconds(30)));

        Assert.Equal(RunStatus.Cancelled, run.Status);
        Assert.Equal(clock.UtcNow, run.CompletedAt);
        Assert.NotEqual(tokenBefore, run.ConcurrencyToken);
    }

    [Fact]
    public void Ein_abgebrochener_Run_laesst_sich_nicht_erneut_abbrechen()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);
        run.Cancel(clock.UtcNow);

        Assert.False(run.CanTransitionTo(RunStatus.Cancelled));
        Assert.Throws<InvalidOperationException>(() => run.Cancel(clock.UtcNow));
    }

    [Fact]
    public void AppendMessage_vergibt_fortlaufende_Sequenzen()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);

        run.AppendMessage(MessageRole.Assistant, "Alles klar.", clock.UtcNow);

        Assert.Equal([0, 1, 2], run.Messages.Select(m => m.Sequence));
    }

    [Fact]
    public void Eine_Werkzeugnachricht_ohne_ToolCallId_wird_abgelehnt()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);

        Assert.Throws<ArgumentException>(() => run.AppendMessage(MessageRole.Tool, "Ergebnis", clock.UtcNow));
    }

    [Fact]
    public void Nur_Werkzeugnachrichten_duerfen_eine_ToolCallId_tragen()
    {
        var clock = TestClock.AtEpoch();
        var run = Run.Create(NewAgent(clock), "Baue eine Todo-App.", clock.UtcNow);

        Assert.Throws<ArgumentException>(
            () => run.AppendMessage(MessageRole.Assistant, "Text", clock.UtcNow, toolCallId: "call_1"));
    }
}
```

`tests/AgentForge.Areas.Agents.Unit/RunTransitionsTests.cs`:

```csharp
namespace AgentForge.Areas.Agents.Unit;

public class RunTransitionsTests
{
    [Fact]
    public void Pending_darf_nach_Cancelled() =>
        Assert.True(RunTransitions.IsAllowed(RunStatus.Pending, RunStatus.Cancelled));

    [Theory]
    [InlineData(RunStatus.Pending, RunStatus.Running)]
    [InlineData(RunStatus.Pending, RunStatus.Completed)]
    [InlineData(RunStatus.Pending, RunStatus.Failed)]
    [InlineData(RunStatus.Pending, RunStatus.Pending)]
    [InlineData(RunStatus.Running, RunStatus.Completed)]
    [InlineData(RunStatus.Running, RunStatus.Cancelled)]
    [InlineData(RunStatus.Completed, RunStatus.Cancelled)]
    [InlineData(RunStatus.Failed, RunStatus.Running)]
    [InlineData(RunStatus.Cancelled, RunStatus.Pending)]
    public void Alle_uebrigen_Uebergaenge_sind_in_dieser_Ausbaustufe_gesperrt(RunStatus from, RunStatus to) =>
        Assert.False(RunTransitions.IsAllowed(from, to));
}
```

Die gesperrten Übergänge sind Absicht, nicht Unvollständigkeit: Teilprojekt 3 öffnet `Pending → Running → Completed | Failed` zusammen mit dem Motor, der sie auslöst.

- [ ] **Step 4: Tests laufen lassen und Fehlschlag prüfen**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit`
Erwartet: FAIL — Kompilierfehler, die Domänentypen fehlen.

- [ ] **Step 5: Aufzählungen, Snapshot und Definition schreiben**

`src/Areas/AgentForge.Areas.Agents/Domain/RunStatus.cs`:

```csharp
namespace AgentForge.Areas.Agents.Domain;

public enum RunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
```

`src/Areas/AgentForge.Areas.Agents/Domain/MessageRole.cs`:

```csharp
namespace AgentForge.Areas.Agents.Domain;

public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}
```

`src/Areas/AgentForge.Areas.Agents/Domain/AgentSnapshot.cs`:

```csharp
namespace AgentForge.Areas.Agents.Domain;

public sealed record AgentSnapshot(
    string Name,
    string SystemPrompt,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    int MaxTurns,
    string[] AllowedTools);
```

`src/Areas/AgentForge.Areas.Agents/Domain/AgentDefinition.cs`:

```csharp
namespace AgentForge.Areas.Agents.Domain;

public sealed record AgentDefinition(
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    int MaxTurns,
    string[] AllowedTools);
```

- [ ] **Step 6: `RunTransitions.cs` und `AgentErrors.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Domain/RunTransitions.cs`:

```csharp
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
```

`src/Areas/AgentForge.Areas.Agents/Domain/AgentErrors.cs`:

```csharp
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
```

- [ ] **Step 7: `Agent.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Domain/Agent.cs`:

```csharp
namespace AgentForge.Areas.Agents.Domain;

public sealed class Agent
{
    public const double DefaultTemperature = 0.7;
    public const int DefaultMaxOutputTokens = 4096;
    public const int DefaultMaxTurns = 20;

    private Agent()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string SystemPrompt { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public double Temperature { get; private set; }

    public int MaxOutputTokens { get; private set; }

    public int MaxTurns { get; private set; }

    public string[] AllowedTools { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public static Agent Create(string ownerId, AgentDefinition definition, DateTimeOffset now)
    {
        var agent = new Agent
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            CreatedAt = now
        };

        agent.Apply(definition, now);
        return agent;
    }

    public void Update(AgentDefinition definition, DateTimeOffset now) => Apply(definition, now);

    public void Archive(DateTimeOffset now)
    {
        ArchivedAt = now;
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public AgentSnapshot ToSnapshot() =>
        new(Name, SystemPrompt, Model, Temperature, MaxOutputTokens, MaxTurns, [.. AllowedTools]);

    private void Apply(AgentDefinition definition, DateTimeOffset now)
    {
        Name = definition.Name;
        Description = definition.Description;
        SystemPrompt = definition.SystemPrompt;
        Model = definition.Model;
        Temperature = definition.Temperature;
        MaxOutputTokens = definition.MaxOutputTokens;
        MaxTurns = definition.MaxTurns;
        AllowedTools = [.. definition.AllowedTools];
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }
}
```

`ToSnapshot` und `Apply` kopieren das Array. Ohne diese Kopie teilte sich der eingefrorene Snapshot sein Array mit dem lebenden Agenten, und „eingefroren" wäre eine Lüge.

- [ ] **Step 8: `RunMessage.cs` und `Run.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Domain/RunMessage.cs`:

```csharp
namespace AgentForge.Areas.Agents.Domain;

public sealed class RunMessage
{
    private RunMessage()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public Guid RunId { get; private set; }

    public int Sequence { get; private set; }

    public MessageRole Role { get; private set; }

    public string? Content { get; private set; }

    public string? ToolCallsJson { get; private set; }

    public string? ToolCallId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static RunMessage Create(
        Run run,
        int sequence,
        MessageRole role,
        string? content,
        DateTimeOffset now,
        string? toolCallsJson,
        string? toolCallId)
    {
        if (role == MessageRole.Tool && string.IsNullOrEmpty(toolCallId))
        {
            throw new ArgumentException("Tool messages must carry a tool call id.", nameof(toolCallId));
        }

        if (role != MessageRole.Tool && toolCallId is not null)
        {
            throw new ArgumentException("Only tool messages may carry a tool call id.", nameof(toolCallId));
        }

        return new RunMessage
        {
            Id = Guid.CreateVersion7(),
            OwnerId = run.OwnerId,
            RunId = run.Id,
            Sequence = sequence,
            Role = role,
            Content = content,
            ToolCallsJson = toolCallsJson,
            ToolCallId = toolCallId,
            CreatedAt = now
        };
    }
}
```

`src/Areas/AgentForge.Areas.Agents/Domain/Run.cs`:

```csharp
namespace AgentForge.Areas.Agents.Domain;

public sealed class Run
{
    private readonly List<RunMessage> _messages = [];

    private Run()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public Guid AgentId { get; private set; }

    public AgentSnapshot AgentSnapshot { get; private set; } = null!;

    public string Objective { get; private set; } = string.Empty;

    public RunStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? Error { get; private set; }

    public int? PromptTokens { get; private set; }

    public int? CompletionTokens { get; private set; }

    public decimal? CostEstimate { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyList<RunMessage> Messages => _messages;

    public static Run Create(Agent agent, string objective, DateTimeOffset now)
    {
        var run = new Run
        {
            Id = Guid.CreateVersion7(),
            OwnerId = agent.OwnerId,
            AgentId = agent.Id,
            AgentSnapshot = agent.ToSnapshot(),
            Objective = objective,
            Status = RunStatus.Pending,
            CreatedAt = now,
            ConcurrencyToken = Guid.CreateVersion7()
        };

        run.AppendMessage(MessageRole.System, run.AgentSnapshot.SystemPrompt, now);
        run.AppendMessage(MessageRole.User, objective, now);

        return run;
    }

    public RunMessage AppendMessage(
        MessageRole role,
        string? content,
        DateTimeOffset now,
        string? toolCallsJson = null,
        string? toolCallId = null)
    {
        var message = RunMessage.Create(this, _messages.Count, role, content, now, toolCallsJson, toolCallId);
        _messages.Add(message);
        return message;
    }

    public bool CanTransitionTo(RunStatus target) => RunTransitions.IsAllowed(Status, target);

    public void Cancel(DateTimeOffset now)
    {
        if (!CanTransitionTo(RunStatus.Cancelled))
        {
            throw new InvalidOperationException($"A run in status {Status} cannot move to {RunStatus.Cancelled}.");
        }

        Status = RunStatus.Cancelled;
        CompletedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }
}
```

`Cancel` wirft nur dann, wenn die Anwendungsschicht vorher nicht gefragt hat. Fachlich wird der Fehlerfall über `CanTransitionTo` abgefangen und als `Result` ausgegeben; die Ausnahme schützt gegen Programmierfehler, nicht gegen Benutzereingaben.

- [ ] **Step 9: Tests laufen lassen**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit`
Erwartet: PASS, 24 Tests.

- [ ] **Step 10: Committen**

```bash
git add -A
git commit -m "feat: agents domain model with frozen run snapshots and state machine"
```

---

### Task 5: Persistenz des Agents-Bereichs

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Persistence/AgentsDbContext.cs`, `src/Areas/AgentForge.Areas.Agents/Persistence/EntityConfigurations.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/{TestCurrentUser,AgentsDatabase,PersistenceTests}.cs`

**Interfaces:**
- Consumes: alle Domänentypen aus Task 4, `ICurrentUser` aus Task 1.
- Produces: `AgentForge.Areas.Agents.Persistence.AgentsDbContext` mit Konstruktor `(DbContextOptions<AgentsDbContext> options, ICurrentUser currentUser)`, den Mengen `Agents`, `Runs`, `RunMessages` und der Konstante `TablePrefix = "agents_"`. Testinfrastruktur `AgentsDatabase` mit `NewContext()` und `TestCurrentUser CurrentUser`.

Die Tests dieser Aufgabe laufen gegen eine echte SQLite-Datenbank im Modus `:memory:` und liegen trotzdem im `.Unit`-Projekt: sie starten keinen Host, sprechen kein HTTP und laufen in Millisekunden. Was sie prüfen, sind die Zusagen der Abbildung — dass die Constraints wirklich greifen.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`tests/AgentForge.Areas.Agents.Unit/TestCurrentUser.cs`:

```csharp
namespace AgentForge.Areas.Agents.Unit;

public sealed class TestCurrentUser(string ownerId) : ICurrentUser
{
    public string OwnerId { get; set; } = ownerId;
}
```

`tests/AgentForge.Areas.Agents.Unit/AgentsDatabase.cs`:

```csharp
using AgentForge.Areas.Agents.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public sealed class AgentsDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public AgentsDatabase(string ownerId = "owner-1")
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        CurrentUser = new TestCurrentUser(ownerId);

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public TestCurrentUser CurrentUser { get; }

    public AgentsDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AgentsDbContext>().UseSqlite(_connection).Options, CurrentUser);

    public void Dispose() => _connection.Dispose();
}
```

Jede Prüfung bekommt einen frischen Kontext auf derselben offenen Verbindung. Das entspricht dem Ablauf im Betrieb — ein Kontext je Anfrage, eine Datenbank für alle — und deckt Fehler auf, die ein einziger, alles zwischenspeichernder Kontext verstecken würde.

`tests/AgentForge.Areas.Agents.Unit/PersistenceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public class PersistenceTests
{
    private static AgentDefinition Definition(string name = "Builder") =>
        new(name, "Baut Dinge.", "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file", "write_file"]);

    private static Agent NewAgent(AgentsDatabase database, string name = "Builder") =>
        Agent.Create(database.CurrentUser.OwnerId, Definition(name), TestClock.AtEpoch().UtcNow);

    [Fact]
    public async Task Ein_Agent_ueberlebt_den_Rundlauf_durch_die_Datenbank()
    {
        using var database = new AgentsDatabase();
        var agent = NewAgent(database);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            var loaded = await context.Agents.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(agent.Id, loaded.Id);
            Assert.Equal("Builder", loaded.Name);
            Assert.Equal(["read_file", "write_file"], loaded.AllowedTools);
            Assert.Equal(agent.ConcurrencyToken, loaded.ConcurrencyToken);
        }
    }

    [Fact]
    public async Task Der_Snapshot_eines_Runs_ueberlebt_den_Rundlauf()
    {
        using var database = new AgentsDatabase();
        var agent = NewAgent(database);
        var run = Run.Create(agent, "Baue eine Todo-App.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            var loaded = await context.Runs.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Du bist hilfreich.", loaded.AgentSnapshot.SystemPrompt);
            Assert.Equal(["read_file", "write_file"], loaded.AgentSnapshot.AllowedTools);
            Assert.Equal(RunStatus.Pending, loaded.Status);
        }
    }

    [Fact]
    public async Task Zwei_aktive_Agenten_duerfen_nicht_denselben_Namen_tragen()
    {
        using var database = new AgentsDatabase();

        await using var context = database.NewContext();
        context.Agents.Add(NewAgent(database));
        context.Agents.Add(NewAgent(database));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Nach_dem_Archivieren_ist_der_Name_wieder_frei()
    {
        using var database = new AgentsDatabase();
        var first = NewAgent(database);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            first.Archive(TestClock.AtEpoch().UtcNow);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            context.Agents.Add(NewAgent(database));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, await context.Agents.CountAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Agenten_fremder_Besitzer_sind_unsichtbar()
    {
        using var database = new AgentsDatabase();

        await using (var context = database.NewContext())
        {
            context.Agents.Add(Agent.Create("owner-1", Definition("Meiner"), TestClock.AtEpoch().UtcNow));
            context.Agents.Add(Agent.Create("owner-2", Definition("Fremder"), TestClock.AtEpoch().UtcNow));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            var visible = await context.Agents.Select(a => a.Name).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(["Meiner"], visible);
        }

        database.CurrentUser.OwnerId = "owner-2";

        await using (var context = database.NewContext())
        {
            var visible = await context.Agents.Select(a => a.Name).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(["Fremder"], visible);
        }
    }

    [Fact]
    public async Task Nachrichten_verschwinden_mit_ihrem_Run()
    {
        using var database = new AgentsDatabase();
        var agent = NewAgent(database);
        var run = Run.Create(agent, "Baue eine Todo-App.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            context.Runs.Remove(await context.Runs.SingleAsync(TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Empty(await context.RunMessages.ToListAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Ein_Agent_mit_Runs_laesst_sich_nicht_loeschen()
    {
        using var database = new AgentsDatabase();
        var agent = NewAgent(database);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(Run.Create(agent, "Baue eine Todo-App.", TestClock.AtEpoch().UtcNow));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.NewContext())
        {
            context.Agents.Remove(await context.Agents.SingleAsync(TestContext.Current.CancellationToken));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Ein_veraltetes_Token_verhindert_das_Speichern()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();

        await using (var context = database.NewContext())
        {
            context.Agents.Add(NewAgent(database));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var first = database.NewContext();
        await using var second = database.NewContext();

        var fromFirst = await first.Agents.SingleAsync(TestContext.Current.CancellationToken);
        var fromSecond = await second.Agents.SingleAsync(TestContext.Current.CancellationToken);

        fromFirst.Update(Definition("Zuerst"), clock.Advance(TimeSpan.FromMinutes(1)));
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);

        fromSecond.Update(Definition("Danach"), clock.Advance(TimeSpan.FromMinutes(1)));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag prüfen**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit`
Erwartet: FAIL — Kompilierfehler, `AgentsDbContext` fehlt.

- [ ] **Step 3: `EntityConfigurations.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Persistence/EntityConfigurations.cs`:

```csharp
using System.Text.Json;
using AgentForge.Areas.Agents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentForge.Areas.Agents.Persistence;

internal static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new();

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}

internal sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable(AgentsDbContext.TablePrefix + "agent");
        builder.HasKey(agent => agent.Id);

        builder.Property(agent => agent.OwnerId).HasMaxLength(100).IsRequired();
        builder.Property(agent => agent.Name).HasMaxLength(100).IsRequired();
        builder.Property(agent => agent.Description).HasMaxLength(1000);
        builder.Property(agent => agent.SystemPrompt).IsRequired();
        builder.Property(agent => agent.Model).HasMaxLength(100).IsRequired();
        builder.Property(agent => agent.ConcurrencyToken).IsConcurrencyToken();

        builder.Property(agent => agent.AllowedTools)
            .HasConversion(
                value => JsonColumn.Write(value),
                json => JsonColumn.Read<string[]>(json),
                new ValueComparer<string[]>(
                    (left, right) => left!.SequenceEqual(right!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
                    value => value.ToArray()))
            .IsRequired();

        builder.HasIndex(agent => agent.OwnerId);

        builder.HasIndex(agent => new { agent.OwnerId, agent.Name })
            .IsUnique()
            .HasFilter("\"ArchivedAt\" IS NULL");
    }
}

internal sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.ToTable(AgentsDbContext.TablePrefix + "run");
        builder.HasKey(run => run.Id);

        builder.Property(run => run.OwnerId).HasMaxLength(100).IsRequired();
        builder.Property(run => run.Objective).IsRequired();
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(run => run.ConcurrencyToken).IsConcurrencyToken();

        builder.Property(run => run.AgentSnapshot)
            .HasConversion(
                value => JsonColumn.Write(value),
                json => JsonColumn.Read<AgentSnapshot>(json),
                new ValueComparer<AgentSnapshot>(
                    (left, right) => JsonColumn.Write(left) == JsonColumn.Write(right),
                    value => JsonColumn.Write(value).GetHashCode(StringComparison.Ordinal),
                    value => JsonColumn.Read<AgentSnapshot>(JsonColumn.Write(value))))
            .IsRequired();

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(run => run.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(run => run.Messages)
            .WithOne()
            .HasForeignKey(message => message.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(run => run.Messages)
            .HasField("_messages")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(run => new { run.OwnerId, run.AgentId });
    }
}

internal sealed class RunMessageConfiguration : IEntityTypeConfiguration<RunMessage>
{
    public void Configure(EntityTypeBuilder<RunMessage> builder)
    {
        builder.ToTable(AgentsDbContext.TablePrefix + "run_message");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.OwnerId).HasMaxLength(100).IsRequired();
        builder.Property(message => message.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(message => message.ToolCallId).HasMaxLength(100);

        builder.HasIndex(message => new { message.RunId, message.Sequence }).IsUnique();
    }
}
```

Der Teilindex `"ArchivedAt" IS NULL` ist der einzige Rohtext-SQL im Modell. Die doppelten Anführungszeichen sind bewusst gewählt: SQLite und PostgreSQL verstehen beide diese Schreibweise für Bezeichner, sodass der Ausdruck den Providerwechsel übersteht.

- [ ] **Step 4: `AgentsDbContext.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Persistence/AgentsDbContext.cs`:

```csharp
using AgentForge.Areas.Agents.Domain;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Persistence;

public sealed class AgentsDbContext(DbContextOptions<AgentsDbContext> options, ICurrentUser currentUser)
    : DbContext(options)
{
    public const string TablePrefix = "agents_";

    public DbSet<Agent> Agents => Set<Agent>();

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<RunMessage> RunMessages => Set<RunMessage>();

    private string OwnerId => currentUser.OwnerId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AgentConfiguration());
        modelBuilder.ApplyConfiguration(new RunConfiguration());
        modelBuilder.ApplyConfiguration(new RunMessageConfiguration());

        modelBuilder.Entity<Agent>().HasQueryFilter(agent => agent.OwnerId == OwnerId);
        modelBuilder.Entity<Run>().HasQueryFilter(run => run.OwnerId == OwnerId);
        modelBuilder.Entity<RunMessage>().HasQueryFilter(message => message.OwnerId == OwnerId);
    }
}
```

Der Filter greift auf `OwnerId` des Kontexts zu, nicht auf einen festen Wert. EF wertet ihn bei jeder Abfrage neu als Parameter aus — deshalb sieht derselbe Kontexttyp für verschiedene Benutzer verschiedene Daten, ohne dass eine einzige Abfrage im Code davon weiß.

- [ ] **Step 5: Tests laufen lassen**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit`
Erwartet: PASS, 32 Tests.

Schlägt `Ein_Agent_mit_Runs_laesst_sich_nicht_loeschen` fehl, weil SQLite die Fremdschlüssel nicht durchsetzt: SQLite prüft Fremdschlüssel nur bei aktiviertem `PRAGMA foreign_keys`. Der EF-SQLite-Provider setzt das je Verbindung selbst; passiert es nicht, ergänze in `AgentsDatabase` nach `_connection.Open()` einen Befehl `PRAGMA foreign_keys = ON;`.

- [ ] **Step 6: Committen**

```bash
git add -A
git commit -m "feat: agents persistence with owner filter, json columns and enforced constraints"
```

---

### Task 6: Anwendungsschicht für Agent-Definitionen

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Application/Paging.cs`, `src/Areas/AgentForge.Areas.Agents/Application/AgentService.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/AgentServiceTests.cs`

**Interfaces:**
- Consumes: `AgentsDbContext` aus Task 5, Domänentypen aus Task 4, `Result<T>`, `Error`, `IClock`, `ICurrentUser` aus Task 1.
- Produces (Namensraum `AgentForge.Areas.Agents.Application`):
  - `PageRequest` mit `int Skip`, `int Take`, `static PageRequest From(int? skip, int? take)`, `const int DefaultTake = 50`, `const int MaxTake = 200`
  - `record Page<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take)`
  - `AgentService(AgentsDbContext db, ICurrentUser currentUser, IClock clock)` mit
    `Task<Result<Agent>> CreateAsync(AgentDefinition definition, CancellationToken ct)`,
    `Task<Result<Agent>> GetAsync(Guid id, CancellationToken ct)`,
    `Task<Page<Agent>> ListAsync(PageRequest page, CancellationToken ct)`,
    `Task<Result<Agent>> UpdateAsync(Guid id, AgentDefinition definition, Guid concurrencyToken, CancellationToken ct)`,
    `Task<Result<Agent>> ArchiveAsync(Guid id, CancellationToken ct)`

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`tests/AgentForge.Areas.Agents.Unit/AgentServiceTests.cs`:

```csharp
using AgentForge.Areas.Agents.Application;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public class AgentServiceTests
{
    private static AgentDefinition Definition(string name = "Builder") =>
        new(name, "Baut Dinge.", "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]);

    private static (AgentsDbContext Context, AgentService Service) NewService(AgentsDatabase database, IClock clock)
    {
        var context = database.NewContext();
        return (context, new AgentService(context, database.CurrentUser, clock));
    }

    [Fact]
    public async Task CreateAsync_legt_einen_Agenten_an()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;

        var result = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Builder", result.Value!.Name);
        Assert.Equal("owner-1", result.Value.OwnerId);
        Assert.Equal(1, await context.Agents.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_lehnt_einen_vergebenen_Namen_ab()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        var result = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error!.Value.Kind);
        Assert.Equal("agent_name_taken", result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateAsync_erlaubt_den_Namen_eines_archivierten_Agenten()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        await service.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var result = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetAsync_meldet_einen_unbekannten_Agenten_als_nicht_gefunden()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;

        var result = await service.GetAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.NotFound, result.Error!.Value.Kind);
        Assert.Equal("agent_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task GetAsync_verbirgt_Agenten_fremder_Besitzer_als_nicht_gefunden()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        database.CurrentUser.OwnerId = "owner-2";
        var (otherContext, otherService) = NewService(database, TestClock.AtEpoch());
        await using var __ = otherContext;

        var result = await otherService.GetAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.NotFound, result.Error!.Value.Kind);
    }

    [Fact]
    public async Task GetAsync_findet_auch_archivierte_Agenten()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        await service.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var result = await service.GetAsync(created.Value.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsArchived);
    }

    [Fact]
    public async Task ListAsync_blendet_archivierte_aus_und_liefert_die_Gesamtzahl()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        await service.CreateAsync(Definition("Charlie"), TestContext.Current.CancellationToken);
        await service.CreateAsync(Definition("Alpha"), TestContext.Current.CancellationToken);
        var archived = await service.CreateAsync(Definition("Bravo"), TestContext.Current.CancellationToken);
        await service.ArchiveAsync(archived.Value!.Id, TestContext.Current.CancellationToken);

        var page = await service.ListAsync(PageRequest.From(0, 10), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.Total);
        Assert.Equal(["Alpha", "Charlie"], page.Items.Select(a => a.Name));
    }

    [Fact]
    public async Task ListAsync_beachtet_Skip_und_Take()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        foreach (var name in (string[])["Alpha", "Bravo", "Charlie"])
        {
            await service.CreateAsync(Definition(name), TestContext.Current.CancellationToken);
        }

        var page = await service.ListAsync(PageRequest.From(1, 1), TestContext.Current.CancellationToken);

        Assert.Equal(3, page.Total);
        Assert.Equal(["Bravo"], page.Items.Select(a => a.Name));
    }

    [Fact]
    public void PageRequest_begrenzt_unsinnige_Werte()
    {
        Assert.Equal(PageRequest.DefaultTake, PageRequest.From(null, null).Take);
        Assert.Equal(0, PageRequest.From(-5, null).Skip);
        Assert.Equal(PageRequest.MaxTake, PageRequest.From(0, 10_000).Take);
        Assert.Equal(1, PageRequest.From(0, 0).Take);
    }

    [Fact]
    public async Task UpdateAsync_aendert_den_Agenten_bei_passendem_Token()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var (context, service) = NewService(database, clock);
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));

        var result = await service.UpdateAsync(
            created.Value!.Id,
            Definition("Umbenannt"),
            created.Value.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Umbenannt", result.Value!.Name);
        Assert.Equal(clock.UtcNow, result.Value.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_lehnt_ein_veraltetes_Token_ab()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(
            created.Value!.Id,
            Definition("Umbenannt"),
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        Assert.Equal("concurrency_conflict", result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateAsync_lehnt_einen_fremden_Namen_ab()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        await service.CreateAsync(Definition("Alpha"), TestContext.Current.CancellationToken);
        var second = await service.CreateAsync(Definition("Bravo"), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(
            second.Value!.Id,
            Definition("Alpha"),
            second.Value.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.Equal("agent_name_taken", result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateAsync_erlaubt_den_eigenen_Namen()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition("Alpha"), TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(
            created.Value!.Id,
            Definition("Alpha") with { Model = "another-model" },
            created.Value.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("another-model", result.Value!.Model);
    }

    [Fact]
    public async Task UpdateAsync_lehnt_einen_archivierten_Agenten_ab()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        var archived = await service.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var result = await service.UpdateAsync(
            created.Value.Id,
            Definition("Umbenannt"),
            archived.Value!.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.Equal("agent_archived", result.Error!.Value.Code);
    }

    [Fact]
    public async Task ArchiveAsync_ist_wiederholbar()
    {
        using var database = new AgentsDatabase();
        var (context, service) = NewService(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await service.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        var first = await service.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);
        var second = await service.ArchiveAsync(created.Value.Id, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.ArchivedAt, second.Value!.ArchivedAt);
    }
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag prüfen**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit`
Erwartet: FAIL — Kompilierfehler, `AgentService`, `PageRequest` und `Page<>` fehlen.

- [ ] **Step 3: `Paging.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Application/Paging.cs`:

```csharp
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
```

Unsinnige Seitenangaben werden begrenzt statt abgelehnt. Eine Anfrage mit `take=10000` ist kein Fehler des Aufrufers, den er beheben könnte — sie ist eine Bitte, die der Dienst höflich kleiner erfüllt.

- [ ] **Step 4: `AgentService.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Application/AgentService.cs`:

```csharp
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Application;

public sealed class AgentService(AgentsDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<Result<Agent>> CreateAsync(AgentDefinition definition, CancellationToken ct)
    {
        if (await NameIsTakenAsync(definition.Name, null, ct))
        {
            return AgentErrors.NameTaken(definition.Name);
        }

        var agent = Agent.Create(currentUser.OwnerId, definition, clock.UtcNow);
        db.Agents.Add(agent);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return AgentErrors.NameTaken(definition.Name);
        }

        return agent;
    }

    public async Task<Result<Agent>> GetAsync(Guid id, CancellationToken ct)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        return agent is null ? AgentErrors.AgentNotFound(id) : agent;
    }

    public async Task<Page<Agent>> ListAsync(PageRequest page, CancellationToken ct)
    {
        var query = db.Agents.Where(agent => agent.ArchivedAt == null).OrderBy(agent => agent.Name);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.Take).ToListAsync(ct);

        return new Page<Agent>(items, total, page.Skip, page.Take);
    }

    public async Task<Result<Agent>> UpdateAsync(
        Guid id,
        AgentDefinition definition,
        Guid concurrencyToken,
        CancellationToken ct)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (agent is null)
        {
            return AgentErrors.AgentNotFound(id);
        }

        if (agent.IsArchived)
        {
            return AgentErrors.AgentArchived(id);
        }

        if (agent.ConcurrencyToken != concurrencyToken)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        if (await NameIsTakenAsync(definition.Name, agent.Id, ct))
        {
            return AgentErrors.NameTaken(definition.Name);
        }

        agent.Update(definition, clock.UtcNow);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentErrors.ConcurrencyConflict();
        }
        catch (DbUpdateException)
        {
            return AgentErrors.NameTaken(definition.Name);
        }

        return agent;
    }

    public async Task<Result<Agent>> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (agent is null)
        {
            return AgentErrors.AgentNotFound(id);
        }

        if (agent.IsArchived)
        {
            return agent;
        }

        agent.Archive(clock.UtcNow);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        return agent;
    }

    private Task<bool> NameIsTakenAsync(string name, Guid? exceptId, CancellationToken ct) =>
        db.Agents.AnyAsync(
            agent => agent.Name == name && agent.ArchivedAt == null && (exceptId == null || agent.Id != exceptId),
            ct);
}
```

Drei Entscheidungen, die in den Tests festgehalten sind: Das Archivieren ist wiederholbar und meldet beim zweiten Mal keinen Fehler, weil der gewünschte Zustand bereits erreicht ist. Ein archivierter Agent lässt sich nicht mehr ändern. Und die Namensprüfung geschieht zweimal — einmal vorab für eine verständliche Meldung, einmal als Auffangen des eindeutigen Index, falls zwei Anfragen gleichzeitig eintreffen.

- [ ] **Step 5: Tests laufen lassen**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit`
Erwartet: PASS, 47 Tests.

- [ ] **Step 6: Committen**

```bash
git add -A
git commit -m "feat: agent definition service with archiving and optimistic concurrency"
```

---

### Task 7: Anwendungsschicht für Runs

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Application/RunService.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/RunServiceTests.cs`

**Interfaces:**
- Consumes: `AgentsDbContext`, `AgentService`, `PageRequest`, `Page<T>`, Domänentypen, `Result<T>`, `IClock`.
- Produces: `AgentForge.Areas.Agents.Application.RunService(AgentsDbContext db, IClock clock)` mit
  `Task<Result<Run>> CreateAsync(Guid agentId, string objective, CancellationToken ct)`,
  `Task<Result<Run>> GetAsync(Guid id, CancellationToken ct)`,
  `Task<Page<Run>> ListAsync(Guid? agentId, RunStatus? status, PageRequest page, CancellationToken ct)`,
  `Task<Result<Run>> CancelAsync(Guid id, Guid concurrencyToken, CancellationToken ct)`,
  `Task<Result<IReadOnlyList<RunMessage>>> GetMessagesAsync(Guid runId, CancellationToken ct)`

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`tests/AgentForge.Areas.Agents.Unit/RunServiceTests.cs`:

```csharp
using AgentForge.Areas.Agents.Application;

namespace AgentForge.Areas.Agents.Unit;

public class RunServiceTests
{
    private static AgentDefinition Definition(string name = "Builder") =>
        new(name, null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, []);

    private sealed record Fixture(AgentsDbContext Context, AgentService Agents, RunService Runs, TestClock Clock)
        : IDisposable
    {
        public void Dispose() => Context.Dispose();
    }

    private static Fixture NewFixture(AgentsDatabase database)
    {
        var clock = TestClock.AtEpoch();
        var context = database.NewContext();
        return new Fixture(context, new AgentService(context, database.CurrentUser, clock), new RunService(context, clock), clock);
    }

    [Fact]
    public async Task CreateAsync_meldet_einen_unbekannten_Agenten()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);

        var result = await fixture.Runs.CreateAsync(Guid.CreateVersion7(), "Los.", TestContext.Current.CancellationToken);

        Assert.Equal("agent_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateAsync_lehnt_einen_archivierten_Agenten_ab()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        await fixture.Agents.ArchiveAsync(agent.Value!.Id, TestContext.Current.CancellationToken);

        var result = await fixture.Runs.CreateAsync(agent.Value.Id, "Los.", TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Value.Kind);
        Assert.Equal("agent_archived", result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateAsync_erzeugt_einen_wartenden_Run_mit_zwei_Nachrichten()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        var result = await fixture.Runs.CreateAsync(agent.Value!.Id, "Baue eine Todo-App.", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RunStatus.Pending, result.Value!.Status);
        Assert.Equal("Du bist hilfreich.", result.Value.AgentSnapshot.SystemPrompt);

        var messages = await fixture.Runs.GetMessagesAsync(result.Value.Id, TestContext.Current.CancellationToken);

        Assert.Equal([MessageRole.System, MessageRole.User], messages.Value!.Select(m => m.Role));
        Assert.Equal([0, 1], messages.Value.Select(m => m.Sequence));
    }

    [Fact]
    public async Task GetAsync_meldet_einen_unbekannten_Run()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);

        var result = await fixture.Runs.GetAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal("run_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task GetMessagesAsync_meldet_einen_unbekannten_Run()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);

        var result = await fixture.Runs.GetMessagesAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal("run_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task ListAsync_filtert_nach_Agent_und_Status()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var first = await fixture.Agents.CreateAsync(Definition("Alpha"), TestContext.Current.CancellationToken);
        var second = await fixture.Agents.CreateAsync(Definition("Bravo"), TestContext.Current.CancellationToken);

        var kept = await fixture.Runs.CreateAsync(first.Value!.Id, "Eins.", TestContext.Current.CancellationToken);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var cancelled = await fixture.Runs.CreateAsync(first.Value.Id, "Zwei.", TestContext.Current.CancellationToken);
        await fixture.Runs.CreateAsync(second.Value!.Id, "Drei.", TestContext.Current.CancellationToken);
        await fixture.Runs.CancelAsync(cancelled.Value!.Id, cancelled.Value.ConcurrencyToken, TestContext.Current.CancellationToken);

        var byAgent = await fixture.Runs.ListAsync(first.Value.Id, null, PageRequest.From(0, 10), TestContext.Current.CancellationToken);
        var byStatus = await fixture.Runs.ListAsync(null, RunStatus.Pending, PageRequest.From(0, 10), TestContext.Current.CancellationToken);

        Assert.Equal(2, byAgent.Total);
        Assert.Equal(2, byStatus.Total);
        Assert.DoesNotContain(cancelled.Value.Id, byStatus.Items.Select(r => r.Id));
        Assert.Contains(kept.Value!.Id, byAgent.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task CancelAsync_setzt_den_Run_auf_abgebrochen()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        var run = await fixture.Runs.CreateAsync(agent.Value!.Id, "Los.", TestContext.Current.CancellationToken);
        var cancelledAt = fixture.Clock.Advance(TimeSpan.FromSeconds(30));

        var result = await fixture.Runs.CancelAsync(run.Value!.Id, run.Value.ConcurrencyToken, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RunStatus.Cancelled, result.Value!.Status);
        Assert.Equal(cancelledAt, result.Value.CompletedAt);
    }

    [Fact]
    public async Task CancelAsync_lehnt_ein_veraltetes_Token_ab()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        var run = await fixture.Runs.CreateAsync(agent.Value!.Id, "Los.", TestContext.Current.CancellationToken);

        var result = await fixture.Runs.CancelAsync(run.Value!.Id, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal("concurrency_conflict", result.Error!.Value.Code);
    }

    [Fact]
    public async Task Ein_bereits_abgebrochener_Run_kann_nicht_erneut_abgebrochen_werden()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        var run = await fixture.Runs.CreateAsync(agent.Value!.Id, "Los.", TestContext.Current.CancellationToken);
        var cancelled = await fixture.Runs.CancelAsync(run.Value!.Id, run.Value.ConcurrencyToken, TestContext.Current.CancellationToken);

        var again = await fixture.Runs.CancelAsync(
            run.Value.Id,
            cancelled.Value!.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.Equal("run_invalid_transition", again.Error!.Value.Code);
    }
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag prüfen**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit`
Erwartet: FAIL — Kompilierfehler, `RunService` fehlt.

- [ ] **Step 3: `RunService.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Application/RunService.cs`:

```csharp
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Application;

public sealed class RunService(AgentsDbContext db, IClock clock)
{
    public async Task<Result<Run>> CreateAsync(Guid agentId, string objective, CancellationToken ct)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(candidate => candidate.Id == agentId, ct);

        if (agent is null)
        {
            return AgentErrors.AgentNotFound(agentId);
        }

        if (agent.IsArchived)
        {
            return AgentErrors.AgentArchived(agentId);
        }

        var run = Run.Create(agent, objective, clock.UtcNow);
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);

        return run;
    }

    public async Task<Result<Run>> GetAsync(Guid id, CancellationToken ct)
    {
        var run = await db.Runs.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        return run is null ? AgentErrors.RunNotFound(id) : run;
    }

    public async Task<Page<Run>> ListAsync(Guid? agentId, RunStatus? status, PageRequest page, CancellationToken ct)
    {
        var query = db.Runs.AsQueryable();

        if (agentId is { } id)
        {
            query = query.Where(run => run.AgentId == id);
        }

        if (status is { } wanted)
        {
            query = query.Where(run => run.Status == wanted);
        }

        var ordered = query.OrderByDescending(run => run.CreatedAt).ThenByDescending(run => run.Id);

        var total = await ordered.CountAsync(ct);
        var items = await ordered.Skip(page.Skip).Take(page.Take).ToListAsync(ct);

        return new Page<Run>(items, total, page.Skip, page.Take);
    }

    public async Task<Result<Run>> CancelAsync(Guid id, Guid concurrencyToken, CancellationToken ct)
    {
        var run = await db.Runs.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

        if (run is null)
        {
            return AgentErrors.RunNotFound(id);
        }

        if (run.ConcurrencyToken != concurrencyToken)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        if (!run.CanTransitionTo(RunStatus.Cancelled))
        {
            return AgentErrors.InvalidTransition(run.Status, RunStatus.Cancelled);
        }

        run.Cancel(clock.UtcNow);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentErrors.ConcurrencyConflict();
        }

        return run;
    }

    public async Task<Result<IReadOnlyList<RunMessage>>> GetMessagesAsync(Guid runId, CancellationToken ct)
    {
        if (!await db.Runs.AnyAsync(run => run.Id == runId, ct))
        {
            return AgentErrors.RunNotFound(runId);
        }

        var messages = await db.RunMessages
            .Where(message => message.RunId == runId)
            .OrderBy(message => message.Sequence)
            .ToListAsync(ct);

        return Result<IReadOnlyList<RunMessage>>.Success(messages);
    }
}
```

Die Sortierung fällt nach `CreatedAt` auf `Id` zurück. Beide Runs derselben Sekunde hätten sonst keine feste Reihenfolge, und eine seitenweise Ausgabe ohne feste Reihenfolge liefert Datensätze doppelt oder gar nicht. Weil die Ids nach Version 7 zeitsortiert sind, ist der Rückfall genau die richtige Ersatzordnung.

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit`
Erwartet: PASS, 56 Tests.

- [ ] **Step 5: Committen**

```bash
git add -A
git commit -m "feat: run service with cancellation, filtering and message retrieval"
```

---

### Task 8: HTTP-Oberfläche und Einhängen des Bereichs

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Http/{Requests,Responses,AgentEndpoints,RunEndpoints}.cs`
- Create: `src/Areas/AgentForge.Areas.Agents/AgentsArea.cs`
- Modify: `src/AgentForge.Host/Program.cs` — eine Zeile `builder.AddArea<AgentsArea>();`
- Modify: `tests/AgentForge.Host.Integration/HostEndpointTests.cs` — `/api/areas` liefert jetzt `agents`
- Test: `tests/AgentForge.Host.Integration/{AgentEndpointTests,RunEndpointTests}.cs`

**Interfaces:**
- Consumes: `AgentService`, `RunService`, `PageRequest`, `Page<T>` aus Task 6 und 7; Domänentypen aus Task 4; `IArea`, `IDbProvider`, `ResultExtensions`, `ValidationFilter<T>` aus Task 2.
- Produces (Namensraum `AgentForge.Areas.Agents.Http`): `CreateAgentRequest`, `UpdateAgentRequest`, `CreateRunRequest`, `CancelRunRequest`, `AgentResponse`, `AgentSnapshotResponse`, `RunResponse`, `RunMessageResponse`, `PagedResponse<T>`, `AgentEndpoints.MapAgentEndpoints(this IEndpointRouteBuilder)`, `RunEndpoints.MapRunEndpoints(this IEndpointRouteBuilder)`. Dazu `AgentForge.Areas.Agents.AgentsArea` mit `Slug => "agents"`.

- [ ] **Step 1: Die Testinfrastruktur ergänzen**

Ergänze in `tests/AgentForge.Host.Integration/AgentForge.Host.Integration.csproj` die zusätzlichen globalen Usings:

```xml
    <Using Include="System.Text.Json" />
    <Using Include="AgentForge.Areas.Agents.Http" />
```

`tests/AgentForge.Host.Integration/ApiClient.cs`:

```csharp
namespace AgentForge.Host.Integration;

public static class ApiClient
{
    public static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static CreateAgentRequest NewAgent(string name = "Builder") =>
        new(name, "Baut Dinge.", "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]);

    public static async Task<AgentResponse> CreateAgentAsync(HttpClient client, string name, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("/api/agents/definitions", NewAgent(name), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentResponse>(ct))!;
    }
}
```

- [ ] **Step 2: Die fehlschlagenden Integrationstests für Definitionen schreiben**

`tests/AgentForge.Host.Integration/AgentEndpointTests.cs`:

```csharp
namespace AgentForge.Host.Integration;

public sealed class AgentEndpointTests : IDisposable
{
    private readonly AgentForgeFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Ein_Agent_durchlaeuft_Anlegen_Lesen_Aendern_Listen_und_Archivieren()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var created = await client.PostAsJsonAsync("/api/agents/definitions", ApiClient.NewAgent(), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var agent = (await created.Content.ReadFromJsonAsync<AgentResponse>(ct))!;
        Assert.Equal("Builder", agent.Name);
        Assert.Equal(["read_file"], agent.AllowedTools);

        var fetched = await client.GetFromJsonAsync<AgentResponse>($"/api/agents/definitions/{agent.Id}", ct);
        Assert.Equal(agent.Id, fetched!.Id);

        var update = new UpdateAgentRequest("Umbenannt", null, "Neuer Prompt.", "other-model", 1.0, 512, 5, [], agent.ConcurrencyToken);
        using var updated = await client.PutAsJsonAsync($"/api/agents/definitions/{agent.Id}", update, ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var afterUpdate = (await updated.Content.ReadFromJsonAsync<AgentResponse>(ct))!;
        Assert.Equal("Umbenannt", afterUpdate.Name);
        Assert.NotEqual(agent.ConcurrencyToken, afterUpdate.ConcurrencyToken);

        var listed = await client.GetFromJsonAsync<PagedResponse<AgentResponse>>("/api/agents/definitions", ct);
        Assert.Equal(1, listed!.Total);

        using var archived = await client.DeleteAsync($"/api/agents/definitions/{agent.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        var afterArchive = await client.GetFromJsonAsync<PagedResponse<AgentResponse>>("/api/agents/definitions", ct);
        Assert.Equal(0, afterArchive!.Total);

        var stillReachable = await client.GetFromJsonAsync<AgentResponse>($"/api/agents/definitions/{agent.Id}", ct);
        Assert.NotNull(stillReachable!.ArchivedAt);
    }

    [Fact]
    public async Task Ein_unbekannter_Agent_ergibt_404_als_ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/agents/definitions/{Guid.CreateVersion7()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("agent_not_found", await ApiClient.ReadErrorCodeAsync(response, ct));
    }

    [Fact]
    public async Task Ein_leerer_Name_ergibt_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/agents/definitions",
            ApiClient.NewAgent() with { Name = "" },
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Eine_unzulaessige_Temperatur_ergibt_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/agents/definitions",
            ApiClient.NewAgent() with { Temperature = 5.0 },
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ein_doppelter_Name_ergibt_409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var response = await client.PostAsJsonAsync("/api/agents/definitions", ApiClient.NewAgent(), ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("agent_name_taken", await ApiClient.ReadErrorCodeAsync(response, ct));
    }

    [Fact]
    public async Task Ein_veraltetes_Token_ergibt_409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        var update = new UpdateAgentRequest("Umbenannt", null, "Prompt.", "some-model", 0.5, 2048, 10, [], Guid.CreateVersion7());
        using var response = await client.PutAsJsonAsync($"/api/agents/definitions/{agent.Id}", update, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("concurrency_conflict", await ApiClient.ReadErrorCodeAsync(response, ct));
    }
}
```

- [ ] **Step 3: Die fehlschlagenden Integrationstests für Runs schreiben**

`tests/AgentForge.Host.Integration/RunEndpointTests.cs`:

```csharp
namespace AgentForge.Host.Integration;

public sealed class RunEndpointTests : IDisposable
{
    private readonly AgentForgeFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Ein_Run_wird_wartend_angelegt_und_traegt_zwei_Nachrichten()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Baue eine Todo-App."),
            ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;
        Assert.Equal("Pending", run.Status);
        Assert.Equal("Du bist hilfreich.", run.AgentSnapshot.SystemPrompt);
        Assert.Null(run.StartedAt);
        Assert.Null(run.CompletedAt);

        var messages = await client.GetFromJsonAsync<RunMessageResponse[]>($"/api/agents/runs/{run.Id}/messages", ct);
        Assert.Equal(["System", "User"], messages!.Select(m => m.Role));
        Assert.Equal("Baue eine Todo-App.", messages[1].Content);
    }

    [Fact]
    public async Task Der_Snapshot_bleibt_stehen_wenn_der_Agent_sich_aendert()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Baue eine Todo-App."),
            ct);
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;

        var update = new UpdateAgentRequest("Builder", null, "Voellig anderer Prompt.", "other-model", 1.0, 512, 5, [], agent.ConcurrencyToken);
        using var updated = await client.PutAsJsonAsync($"/api/agents/definitions/{agent.Id}", update, ct);
        updated.EnsureSuccessStatusCode();

        var reloaded = await client.GetFromJsonAsync<RunResponse>($"/api/agents/runs/{run.Id}", ct);

        Assert.Equal("Du bist hilfreich.", reloaded!.AgentSnapshot.SystemPrompt);
        Assert.Equal("some-model", reloaded.AgentSnapshot.Model);
    }

    [Fact]
    public async Task Ein_Run_laesst_sich_abbrechen_aber_nicht_zweimal()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Baue eine Todo-App."),
            ct);
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;

        using var cancelled = await client.PostAsJsonAsync(
            $"/api/agents/runs/{run.Id}/cancel",
            new CancelRunRequest(run.ConcurrencyToken),
            ct);

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var afterCancel = (await cancelled.Content.ReadFromJsonAsync<RunResponse>(ct))!;
        Assert.Equal("Cancelled", afterCancel.Status);
        Assert.NotNull(afterCancel.CompletedAt);

        using var again = await client.PostAsJsonAsync(
            $"/api/agents/runs/{run.Id}/cancel",
            new CancelRunRequest(afterCancel.ConcurrencyToken),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("run_invalid_transition", await ApiClient.ReadErrorCodeAsync(again, ct));
    }

    [Fact]
    public async Task Ein_archivierter_Agent_nimmt_keine_neuen_Runs_an()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var archived = await client.DeleteAsync($"/api/agents/definitions/{agent.Id}", ct);
        archived.EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Zu spaet."),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("agent_archived", await ApiClient.ReadErrorCodeAsync(response, ct));
    }

    [Fact]
    public async Task Ein_unbekannter_Run_ergibt_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/agents/runs/{Guid.CreateVersion7()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("run_not_found", await ApiClient.ReadErrorCodeAsync(response, ct));
    }

    [Fact]
    public async Task Runs_lassen_sich_nach_Agent_und_Status_filtern()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var first = await ApiClient.CreateAgentAsync(client, "Alpha", ct);
        var second = await ApiClient.CreateAgentAsync(client, "Bravo", ct);

        using var one = await client.PostAsJsonAsync("/api/agents/runs", new CreateRunRequest(first.Id, "Eins."), ct);
        using var two = await client.PostAsJsonAsync("/api/agents/runs", new CreateRunRequest(first.Id, "Zwei."), ct);
        using var three = await client.PostAsJsonAsync("/api/agents/runs", new CreateRunRequest(second.Id, "Drei."), ct);
        one.EnsureSuccessStatusCode();
        three.EnsureSuccessStatusCode();

        var toCancel = (await two.Content.ReadFromJsonAsync<RunResponse>(ct))!;
        using var cancelled = await client.PostAsJsonAsync(
            $"/api/agents/runs/{toCancel.Id}/cancel",
            new CancelRunRequest(toCancel.ConcurrencyToken),
            ct);
        cancelled.EnsureSuccessStatusCode();

        var byAgent = await client.GetFromJsonAsync<PagedResponse<RunResponse>>($"/api/agents/runs?agentId={first.Id}", ct);
        var byStatus = await client.GetFromJsonAsync<PagedResponse<RunResponse>>("/api/agents/runs?status=Pending", ct);

        Assert.Equal(2, byAgent!.Total);
        Assert.Equal(2, byStatus!.Total);
        Assert.DoesNotContain(toCancel.Id, byStatus.Items.Select(r => r.Id));
    }
}
```

- [ ] **Step 4: Die Erwartung im Host-Test nachziehen**

Ersetze in `tests/AgentForge.Host.Integration/HostEndpointTests.cs` den Rumpf von `Areas_liefert_die_registrierten_Bereiche`:

```csharp
        Assert.NotNull(areas);
        Assert.Equal(["agents"], areas.Select(a => a.Slug));
```

- [ ] **Step 5: Tests laufen lassen und Fehlschlag prüfen**

Run: `dotnet test tests/AgentForge.Host.Integration`
Erwartet: FAIL — Kompilierfehler, die Typen aus `AgentForge.Areas.Agents.Http` fehlen.

- [ ] **Step 6: `Requests.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Http/Requests.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Http;

public sealed record CreateAgentRequest(
    [property: Required][property: StringLength(100, MinimumLength = 1)] string Name,
    [property: StringLength(1000)] string? Description,
    [property: Required][property: StringLength(20_000, MinimumLength = 1)] string SystemPrompt,
    [property: Required][property: StringLength(100, MinimumLength = 1)] string Model,
    [property: Range(0.0, 2.0)] double? Temperature,
    [property: Range(1, 200_000)] int? MaxOutputTokens,
    [property: Range(1, 200)] int? MaxTurns,
    string[]? AllowedTools);

public sealed record UpdateAgentRequest(
    [property: Required][property: StringLength(100, MinimumLength = 1)] string Name,
    [property: StringLength(1000)] string? Description,
    [property: Required][property: StringLength(20_000, MinimumLength = 1)] string SystemPrompt,
    [property: Required][property: StringLength(100, MinimumLength = 1)] string Model,
    [property: Range(0.0, 2.0)] double? Temperature,
    [property: Range(1, 200_000)] int? MaxOutputTokens,
    [property: Range(1, 200)] int? MaxTurns,
    string[]? AllowedTools,
    Guid ConcurrencyToken);

public sealed record CreateRunRequest(
    Guid AgentId,
    [property: Required][property: StringLength(20_000, MinimumLength = 1)] string Objective);

public sealed record CancelRunRequest(Guid ConcurrencyToken);

public static class RequestMapping
{
    public static AgentDefinition ToDefinition(this CreateAgentRequest request) =>
        Build(request.Name, request.Description, request.SystemPrompt, request.Model,
            request.Temperature, request.MaxOutputTokens, request.MaxTurns, request.AllowedTools);

    public static AgentDefinition ToDefinition(this UpdateAgentRequest request) =>
        Build(request.Name, request.Description, request.SystemPrompt, request.Model,
            request.Temperature, request.MaxOutputTokens, request.MaxTurns, request.AllowedTools);

    private static AgentDefinition Build(
        string name,
        string? description,
        string systemPrompt,
        string model,
        double? temperature,
        int? maxOutputTokens,
        int? maxTurns,
        string[]? allowedTools) =>
        new(name,
            description,
            systemPrompt,
            model,
            temperature ?? Agent.DefaultTemperature,
            maxOutputTokens ?? Agent.DefaultMaxOutputTokens,
            maxTurns ?? Agent.DefaultMaxTurns,
            allowedTools ?? []);
}
```

Die Attribute stehen mit `[property: ...]` an den Positionsparametern. Ohne dieses Ziel landen sie am Konstruktorparameter, und `Validator.TryValidateObject` — das Eigenschaften liest — würde sie stillschweigend übersehen. Die Validierung wäre dann wirkungslos, ohne dass irgendetwas fehlschlägt.

Auf `Guid`-Feldern stehen bewusst keine Attribute: `Guid.Empty` ist für `[Required]` ein gültiger Wert, und die fachliche Antwort ist ohnehin aussagekräftiger — eine unbekannte `AgentId` ergibt 404, ein leeres Token 409.

- [ ] **Step 7: `Responses.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Http/Responses.cs`:

```csharp
using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Http;

public sealed record AgentResponse(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    int MaxTurns,
    string[] AllowedTools,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    Guid ConcurrencyToken)
{
    public static AgentResponse From(Agent agent) =>
        new(agent.Id, agent.Name, agent.Description, agent.SystemPrompt, agent.Model, agent.Temperature,
            agent.MaxOutputTokens, agent.MaxTurns, agent.AllowedTools, agent.CreatedAt, agent.UpdatedAt,
            agent.ArchivedAt, agent.ConcurrencyToken);
}

public sealed record AgentSnapshotResponse(
    string Name,
    string SystemPrompt,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    int MaxTurns,
    string[] AllowedTools)
{
    public static AgentSnapshotResponse From(AgentSnapshot snapshot) =>
        new(snapshot.Name, snapshot.SystemPrompt, snapshot.Model, snapshot.Temperature,
            snapshot.MaxOutputTokens, snapshot.MaxTurns, snapshot.AllowedTools);
}

public sealed record RunResponse(
    Guid Id,
    Guid AgentId,
    AgentSnapshotResponse AgentSnapshot,
    string Objective,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    int? PromptTokens,
    int? CompletionTokens,
    decimal? CostEstimate,
    Guid ConcurrencyToken)
{
    public static RunResponse From(Run run) =>
        new(run.Id, run.AgentId, AgentSnapshotResponse.From(run.AgentSnapshot), run.Objective,
            run.Status.ToString(), run.CreatedAt, run.StartedAt, run.CompletedAt, run.Error,
            run.PromptTokens, run.CompletionTokens, run.CostEstimate, run.ConcurrencyToken);
}

public sealed record RunMessageResponse(
    Guid Id,
    int Sequence,
    string Role,
    string? Content,
    string? ToolCallsJson,
    string? ToolCallId,
    DateTimeOffset CreatedAt)
{
    public static RunMessageResponse From(RunMessage message) =>
        new(message.Id, message.Sequence, message.Role.ToString(), message.Content,
            message.ToolCallsJson, message.ToolCallId, message.CreatedAt);
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);
```

`Status` und `Role` gehen als Zeichenkette nach außen, nicht als Zahl. Eine Zahl im JSON bindet jeden Aufrufer an die Reihenfolge der Aufzählung, und die erste eingefügte Zwischenstufe verschiebt sie stillschweigend.

- [ ] **Step 8: `AgentEndpoints.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Http/AgentEndpoints.cs`:

```csharp
using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;

namespace AgentForge.Areas.Agents.Http;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/definitions").WithTags("agent-definitions");

        group.MapGet("/", async (AgentService service, int? skip, int? take, CancellationToken ct) =>
        {
            var page = await service.ListAsync(PageRequest.From(skip, take), ct);

            return TypedResults.Ok(new PagedResponse<AgentResponse>(
                [.. page.Items.Select(AgentResponse.From)],
                page.Total,
                page.Skip,
                page.Take));
        });

        group.MapGet("/{id:guid}", async (AgentService service, Guid id, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult(agent => TypedResults.Ok(AgentResponse.From(agent))));

        group.MapPost("/", async (AgentService service, CreateAgentRequest request, CancellationToken ct) =>
                (await service.CreateAsync(request.ToDefinition(), ct)).ToHttpResult(agent =>
                    TypedResults.Created($"/api/agents/definitions/{agent.Id}", AgentResponse.From(agent))))
            .AddEndpointFilter<ValidationFilter<CreateAgentRequest>>();

        group.MapPut("/{id:guid}", async (AgentService service, Guid id, UpdateAgentRequest request, CancellationToken ct) =>
                (await service.UpdateAsync(id, request.ToDefinition(), request.ConcurrencyToken, ct))
                    .ToHttpResult(agent => TypedResults.Ok(AgentResponse.From(agent))))
            .AddEndpointFilter<ValidationFilter<UpdateAgentRequest>>();

        group.MapDelete("/{id:guid}", async (AgentService service, Guid id, CancellationToken ct) =>
            (await service.ArchiveAsync(id, ct)).ToHttpResult(agent => TypedResults.Ok(AgentResponse.From(agent))));
    }
}
```

- [ ] **Step 9: `RunEndpoints.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/Http/RunEndpoints.cs`:

```csharp
using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Http;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/runs").WithTags("agent-runs");

        group.MapGet("/", async (
            RunService service,
            Guid? agentId,
            RunStatus? status,
            int? skip,
            int? take,
            CancellationToken ct) =>
        {
            var page = await service.ListAsync(agentId, status, PageRequest.From(skip, take), ct);

            return TypedResults.Ok(new PagedResponse<RunResponse>(
                [.. page.Items.Select(RunResponse.From)],
                page.Total,
                page.Skip,
                page.Take));
        });

        group.MapGet("/{id:guid}", async (RunService service, Guid id, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult(run => TypedResults.Ok(RunResponse.From(run))));

        group.MapPost("/", async (RunService service, CreateRunRequest request, CancellationToken ct) =>
                (await service.CreateAsync(request.AgentId, request.Objective, ct)).ToHttpResult(run =>
                    TypedResults.Created($"/api/agents/runs/{run.Id}", RunResponse.From(run))))
            .AddEndpointFilter<ValidationFilter<CreateRunRequest>>();

        group.MapPost("/{id:guid}/cancel", async (
                RunService service,
                Guid id,
                CancelRunRequest request,
                CancellationToken ct) =>
                (await service.CancelAsync(id, request.ConcurrencyToken, ct))
                    .ToHttpResult(run => TypedResults.Ok(RunResponse.From(run))))
            .AddEndpointFilter<ValidationFilter<CancelRunRequest>>();

        group.MapGet("/{id:guid}/messages", async (RunService service, Guid id, CancellationToken ct) =>
            (await service.GetMessagesAsync(id, ct)).ToHttpResult(messages =>
                TypedResults.Ok(messages.Select(RunMessageResponse.From).ToArray())));
    }
}
```

- [ ] **Step 10: `AgentsArea.cs` schreiben**

`src/Areas/AgentForge.Areas.Agents/AgentsArea.cs`:

```csharp
using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Http;
using AgentForge.Areas.Agents.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Areas.Agents;

public sealed class AgentsArea : IArea
{
    public string Slug => "agents";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AgentsDbContext>((provider, options) =>
            provider.GetRequiredService<IDbProvider>().Apply(options));

        services.AddScoped<AgentService>();
        services.AddScoped<RunService>();

        services.AddHealthChecks().AddDbContextCheck<AgentsDbContext>("agents-db");
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        routes.MapAgentEndpoints();
        routes.MapRunEndpoints();
    }

    public Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<AgentsDbContext>().Database.EnsureCreatedAsync(cancellationToken);
}
```

Der Bereich nennt seinen Datenbankprovider nicht. Er fragt nach `IDbProvider` und bekommt, was der Host bereitstellt — im Betrieb eine SQLite-Datei, im Test eine offene Verbindung im Arbeitsspeicher, später Neon. Das ist der Grund, warum die Integrationstests keine einzige Zeile Produktionscode umgehen müssen.

- [ ] **Step 11: Den Bereich im Host registrieren**

Ergänze die Projektreferenz und eine Zeile in `Program.cs`:

```bash
dotnet add src/AgentForge.Host reference src/Areas/AgentForge.Areas.Agents
```

In `src/AgentForge.Host/Program.cs` direkt vor `var app = builder.Build();`:

```csharp
builder.AddArea<AgentsArea>();
```

Und oben die Using-Zeile:

```csharp
using AgentForge.Areas.Agents;
```

- [ ] **Step 12: Tests laufen lassen**

Run: `dotnet test`
Erwartet: PASS über alle Projekte, 98 Tests — 3 in `Core.Unit`, 22 in `Areas.Abstractions.Unit`, 56 in `Areas.Agents.Unit`, 17 in `Host.Integration`.

- [ ] **Step 13: Committen**

```bash
git add -A
git commit -m "feat: agents http surface and area registration in the host"
```

---

### Task 9: Architekturtests

**Files:**
- Test: `tests/AgentForge.Host.Architecture/{AreaAssemblies,BoundaryTests}.cs`

**Interfaces:**
- Consumes: `IArea` und `AreaSlug` aus Task 2, `Result<T>` aus Task 1, `Program` aus Task 3, `AgentsArea` aus Task 8.
- Produces: nichts, was anderer Code benutzt. Diese Aufgabe erzeugt ausschließlich Zusicherungen.

- [ ] **Step 1: Projekt anlegen**

```bash
dotnet new xunit3 -o tests/AgentForge.Host.Architecture
dotnet sln add tests/AgentForge.Host.Architecture
dotnet add tests/AgentForge.Host.Architecture reference src/AgentForge.Host
```

Ergänze in `tests/AgentForge.Host.Architecture/AgentForge.Host.Architecture.csproj`:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <Using Include="System.Reflection" />
    <Using Include="AgentForge.Core" />
    <Using Include="AgentForge.Areas.Abstractions" />
  </ItemGroup>
```

- [ ] **Step 2: Die Assembly-Ermittlung schreiben**

`tests/AgentForge.Host.Architecture/AreaAssemblies.cs`:

```csharp
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
```

Die Ermittlung liest das Ausgabeverzeichnis statt der Referenzliste des Hosts. Ein Bereich, den jemand versehentlich nicht mehr benutzt, verschwände aus der Referenzliste und entzöge sich damit genau der Prüfung, die ihn kontrollieren soll.

- [ ] **Step 3: Die Grenztests schreiben**

`tests/AgentForge.Host.Architecture/BoundaryTests.cs`:

```csharp
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
```

`Es_gibt_mindestens_einen_Bereich` sieht überflüssig aus und ist der wichtigste Test der Datei. Ohne ihn liefen alle anderen über eine leere Liste und wären grün, ohne je etwas geprüft zu haben — der stillste Weg, wie ein Architekturtest aufhört zu schützen.

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AgentForge.Host.Architecture`
Erwartet: PASS, sechs Tests.

- [ ] **Step 5: Committen**

```bash
git add -A
git commit -m "test: enforce area boundaries through architecture tests"
```

---

### Task 10: Wirksamkeitsnachweis und Abschluss

**Files:**
- Create: `README.md`
- Temporär: `src/Areas/AgentForge.Areas.Temp` (wird im Verlauf wieder entfernt)

**Interfaces:**
- Consumes: alles Vorherige.
- Produces: nichts.

- [ ] **Step 1: Nachweisen, dass die Slug-Prüfung greift**

Ändere in `src/Areas/AgentForge.Areas.Agents/AgentsArea.cs` vorübergehend:

```csharp
    public string Slug => "Agents";
```

Run: `dotnet test tests/AgentForge.Host.Architecture`
Erwartet: FAIL in `Alle_Slugs_sind_formgueltig_und_eindeutig` mit der Meldung `'Agents' ist kein gueltiger Slug.`

Mache die Änderung anschließend rückgängig und lasse den Test erneut laufen.
Erwartet: PASS.

- [ ] **Step 2: Nachweisen, dass die Bereichsgrenze greift**

```bash
dotnet new classlib -o src/Areas/AgentForge.Areas.Temp
dotnet sln add src/Areas/AgentForge.Areas.Temp
dotnet add src/Areas/AgentForge.Areas.Agents reference src/Areas/AgentForge.Areas.Temp
dotnet test tests/AgentForge.Host.Architecture
```

Erwartet: FAIL in `Kein_Bereich_referenziert_einen_anderen_Bereich_ausserhalb_von_Contracts` mit der Meldung, dass `AgentForge.Areas.Agents` auf `AgentForge.Areas.Temp` verweist.

Danach zurückbauen:

```bash
dotnet remove src/Areas/AgentForge.Areas.Agents reference src/Areas/AgentForge.Areas.Temp
dotnet sln remove src/Areas/AgentForge.Areas.Temp
```

Lösche anschließend das Verzeichnis `src/Areas/AgentForge.Areas.Temp` von Hand — es enthält nur die eben erzeugte Vorlage.

Räume danach die Reste der Testläufe auf, damit die alte Assembly nicht im Ausgabeverzeichnis liegen bleibt und die Ermittlung stört:

```bash
dotnet clean
dotnet test tests/AgentForge.Host.Architecture
```

Erwartet: PASS.

- [ ] **Step 3: `README.md` schreiben**

`README.md`:

````markdown
# AgentForge

Ein modularer .NET-Monolith, in dem fachlich getrennte Bereiche als Module eines
einzigen Hosts leben. Der erste Bereich verwaltet KI-Agenten.

## Aufbau

- `src/AgentForge.Host` — der Composition Root. Die einzige Stelle, die alles kennt.
- `src/AgentForge.Core` — Ergebnistypen, Zeit, Benutzer. Kennt weder ASP.NET noch EF Core.
- `src/AgentForge.Areas.Abstractions` — `IArea` und die Registrierungsmaschinerie.
- `src/Areas/*` — die fachlichen Bereiche.
- `tests/*` — benannt nach dem Muster `<Projekt>.<Testart>`.
- `docs/superpowers/` — Specs und Pläne.

## Einen Bereich hinzufügen

1. Klassenbibliothek unter `src/Areas/AgentForge.Areas.<Name>` anlegen.
2. Genau ein `IArea` implementieren. Der Slug ist kleingeschrieben und mit Bindestrichen getrennt.
3. Im Host `builder.AddArea<...>()` ergänzen — eine Zeile.
4. Braucht der Bereich einen anderen, entsteht dafür ein `*.Contracts`-Projekt mit
   Interfaces und DTOs. Direkte Referenzen zwischen Bereichen lässt
   `tests/AgentForge.Host.Architecture` nicht durch.

## Starten

```bash
dotnet run --project src/AgentForge.Host
```

Die Datenbank ist SQLite unter `.data/agentforge.db` und wird beim Start angelegt.
In der Entwicklungsumgebung liegt die API-Oberfläche unter `/scalar/v1`.

## Tests

```bash
dotnet test
```

## Stand

Umgesetzt sind Teilprojekt 1 und 2 der Spec in
`docs/superpowers/specs/2026-07-29-agentforge-skeleton-agents-area-design.md`:
Skelett, Area-Konvention und die Verwaltung von Agenten und Runs. Es gibt noch keinen
Aufruf eines Sprachmodells und keine Ausführung — ein Run bleibt `Pending`, bis er
abgebrochen wird.
````

- [ ] **Step 4: Alle Fertigstellungskriterien der Spec durchgehen**

```bash
dotnet clean
dotnet build
dotnet test
```

Erwartet: Build ohne Fehler und ohne Warnungen, alle Tests grün.

Prüfe die Kriterien der Spec einzeln ab und halte das Ergebnis fest:

1. `dotnet build` und `dotnet test` ohne Fehler und Warnungen — durch den Lauf oben belegt.
2. Host startet, `/_health` und `/_health/ready` liefern 200 — belegt durch `HostEndpointTests`.
3. `GET /api/areas` liefert genau `agents` — belegt durch `HostEndpointTests`.
4. Vollständiger Durchlauf über Definitionen inklusive Archivierung — belegt durch `AgentEndpointTests`.
5. Run anlegen, Snapshot prüfen, Nachrichten lesen, abbrechen, zweiter Abbruch 409 — belegt durch `RunEndpointTests`.
6. Jeder Fehlerfall der Fehlertabelle als ProblemDetails — belegt durch `AgentEndpointTests` und `RunEndpointTests`.
7. Wirksamkeit der Architekturtests — belegt durch Step 1 und Step 2 dieser Aufgabe.

Fehlt ein Beleg, ergänze den fehlenden Test, bevor du weitergehst.

- [ ] **Step 5: Den Host ein letztes Mal von Hand starten**

```bash
dotnet run --project src/AgentForge.Host
```

Lege über `/scalar/v1` einen Agenten an, starte einen Run, lies dessen Nachrichten und
brich ihn ab. Beende danach mit Strg+C.

- [ ] **Step 6: Committen**

```bash
git add -A
git commit -m "docs: add readme and record completion criteria evidence"
```
