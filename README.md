# AgentForge

Ein modularer .NET-Monolith, in dem fachlich getrennte Bereiche als Module eines
einzigen Hosts leben. Der erste Bereich verwaltet KI-Agenten und führt Runs aus.

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

### Agents-Runtime (`Areas:Agents`)

Konfiguration in `appsettings.json` / `appsettings.Development.json`:

| Schlüssel | Bedeutung |
|---|---|
| `Areas:Agents:Llm:BaseUrl` | OpenAI-kompatible Basis-URL (z. B. NanoGPT) |
| `Areas:Agents:Llm:ApiKey` | Bearer-Token; **nicht** in Git committen — User-Secrets oder Env |
| `Areas:Agents:Llm:UseFake` | `true` → Scripted-LLM ohne Netz (Development-Vorgabe) |
| `Areas:Agents:MaxConcurrentRuns` | Parallelität des Hintergrund-Workers |
| `Areas:Agents:Pricing:*` | Tokenpreise für die Kostenschätzung |

In Development ist `UseFake` standardmäßig `true`, damit `dotnet run` ohne Key startet.
Für echte NanoGPT-Aufrufe lokal:

```bash
dotnet user-secrets set "Areas:Agents:Llm:ApiKey" "<dein-key>" --project src/AgentForge.Host
```

und in `appsettings.Development.json` `UseFake` auf `false` setzen (oder per Env überschreiben).
Ohne Key und mit `UseFake: false` bricht der Start mit Validierungsfehler ab.

`POST /api/agents/runs` legt den Run als `Pending` an und stellt ihn sofort in die
Warteschlange. Ein Worker führt den Turn-Loop aus (LLM + Stub-Tools). Clients können
per `GET /api/agents/runs/{id}` pollen oder `GET /api/agents/runs/{id}/stream` (SSE)
folgen. Abbruch bleibt über `POST .../cancel` möglich, solange der Run `Pending` oder
`Running` ist.

## Tests

```bash
dotnet test
```

## Stand

Umgesetzt sind Teilprojekte 1–3:

- Specs: `docs/superpowers/specs/2026-07-29-agentforge-skeleton-agents-area-design.md`,
  `docs/superpowers/specs/2026-07-30-agentforge-agent-runtime-design.md`
- Skelett, Area-Konvention, Agent-/Run-Verwaltung
- Agent-Runtime: Auto-Start nach Create, Stub-Tools, Fake- oder HTTP-LLM, Token/Kosten,
  SSE und Polling, Cancel aus Pending/Running
