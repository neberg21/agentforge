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
