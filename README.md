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
| `Areas:Agents:Workspace:Enabled` | `true` → echte Workspace-Tools + Push vor `Completed` |
| `Areas:Agents:Workspace:RemoteUrl` | Git-Remote für Clone/Push |
| `Areas:Agents:Workspace:LocalPath` | Lokaler Clone (absolut oder relativ zum Content-Root) |
| `Areas:Agents:Workspace:BaseRef` | Ausgangs-Ref für Worktrees (Vorgabe `main`) |
| `Areas:Agents:Workspace:WorktreesRoot` | Verzeichnis für Run-Worktrees (typisch `workspaces/`) |
| `Areas:Agents:Workspace:ShellTimeout` | Timeout für `run_shell` |
| `Areas:Agents:Workspace:MaxOutputChars` | Kürzung von stdout/stderr |

In Development ist `UseFake` standardmäßig `true`, damit `dotnet run` ohne Key startet.
`Workspace:Enabled` bleibt standardmäßig `false` (Stub-Tools und Complete im Loop wie Teilprojekt 3).
Bei `Enabled: true` arbeitet jeder Run in einem Git-Worktree auf Branch `run/{runId}`;
nach erfolgreichem Loop pusht die Runtime und setzt erst dann `Completed`. Agent-Commits
laufen über `run_shell`; Git-Credentials kommen vom Host (Credential Helper), nicht aus
`appsettings`. Docker/Container-Executor folgt in einem späteren Teilprojekt.
Für echte NanoGPT-Aufrufe lokal:

```bash
dotnet user-secrets set "Areas:Agents:Llm:ApiKey" "<dein-key>" --project src/AgentForge.Host
```

und in `appsettings.Development.json` `UseFake` auf `false` setzen (oder per Env überschreiben).
Ohne Key und mit `UseFake: false` bricht der Start mit Validierungsfehler ab.

`POST /api/agents/runs` legt den Run als `Pending` an und stellt ihn sofort in die
Warteschlange. Ein Worker führt den Turn-Loop aus (LLM + Tools). Clients können
per `GET /api/agents/runs/{id}` pollen oder `GET /api/agents/runs/{id}/stream` (SSE)
folgen. Abbruch bleibt über `POST .../cancel` möglich, solange der Run `Pending` oder
`Running` ist.

## Tests

```bash
dotnet test
```

## Stand

Umgesetzt sind Teilprojekte 1–4:

- Specs: `docs/superpowers/specs/2026-07-29-agentforge-skeleton-agents-area-design.md`,
  `docs/superpowers/specs/2026-07-30-agentforge-agent-runtime-design.md`,
  `docs/superpowers/specs/2026-07-30-agentforge-workspace-tools-design.md`
- Skelett, Area-Konvention, Agent-/Run-Verwaltung
- Agent-Runtime: Auto-Start nach Create, Tools, Fake- oder HTTP-LLM, Token/Kosten,
  SSE und Polling, Cancel aus Pending/Running
- Workspace-Tools: Host-seitiges `read_file` / `write_file` / `run_shell` auf Git-Worktrees,
  Push vor `Completed` (kein Docker in diesem Teilprojekt)
