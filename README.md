# AgentForge

Ein modularer .NET-Monolith, in dem fachlich getrennte Bereiche als Module eines
einzigen Hosts leben. Der erste Bereich verwaltet KI-Agenten und führt Runs aus.

```
┌─────────────────────────────────────────────────────────────────┐
│                         AgentForge.Host                         │
│                     (Composition Root)                          │
│                                                                   │
│   builder.AddArea<AgentsArea>()                                 │
│   builder.AddArea<...>()                                        │
└───────────────────────────┬───────────────────────────────────-─┘
                             │ kennt alle Bereiche
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
     ┌────────────────┐ ┌──────────┐ ┌─────────────┐
     │  Areas.Agents   │ │ Area B   │ │  Area C ...  │
     │  IArea impl.    │ │ IArea    │ │  IArea       │
     └───────┬─────────┘ └────┬─────┘ └──────┬──────┘
             │  *.Contracts    │  *.Contracts  │
             └────────────────►◄───────────────┘
              (nur über Contracts, keine Direktreferenz)

     ┌─────────────────────────────────────────┐
     │           AgentForge.Core                │
     │  Ergebnistypen · Zeit · Benutzer          │
     │  kennt weder ASP.NET noch EF Core         │
     └───────────────────────────────────────────┘
```

## Aufbau

- `backend/src/AgentForge.Host` — der Composition Root. Die einzige Stelle, die alles kennt.
- `backend/src/AgentForge.Core` — Ergebnistypen, Zeit, Benutzer. Kennt weder ASP.NET noch EF Core.
- `backend/src/AgentForge.Areas.Abstractions` — `IArea` und die Registrierungsmaschinerie.
- `backend/src/Areas/*` — die fachlichen Bereiche.
- `backend/tests/*` — benannt nach dem Muster `<Projekt>.<Testart>`.
- `frontend/*` — das Frontend.
- `docs/superpowers/` — Specs und Pläne.

## Einen Bereich hinzufügen

1. Klassenbibliothek unter `src/Areas/AgentForge.Areas.<Name>` anlegen.
2. Genau ein `IArea` implementieren. Der Slug ist kleingeschrieben und mit Bindestrichen getrennt.
3. Im Host `builder.AddArea<...>()` ergänzen — eine Zeile.
4. Braucht der Bereich einen anderen, entsteht dafür ein `*.Contracts`-Projekt mit
   Interfaces und DTOs. Direkte Referenzen zwischen Bereichen lässt
   `tests/AgentForge.Host.Architecture` nicht durch.

```
   Neuer Bereich anlegen
   ──────────────────────
   src/Areas/AgentForge.Areas.<Name>/
   │
   ├── <Name>Area.cs        implements IArea  ──┐
   ├── Endpoints/                                │
   ├── Domain/                                   │  1 Zeile im Host:
   └── AgentForge.Areas.<Name>.csproj             │  builder.AddArea<NameArea>();
                                                  ▼
                                    ┌────────────────────────┐
                                    │   AgentForge.Host       │
                                    └────────────────────────┘

   braucht Bereich B den Bereich A?
   ──────────────────────────────────
   Area.B  ──X── Area.A            ✗ verboten (Architecture-Test schlägt fehl)
   Area.B  ──►  A.Contracts  ◄──  Area.A   ✓ erlaubt
```

## Starten

```
dotnet run --project src/AgentForge.Host
```

Die Datenbank ist SQLite unter `.data/agentforge.db` und wird beim Start angelegt.
In der Entwicklungsumgebung liegt die API-Oberfläche unter `/scalar/v1`.

### Agents-Runtime (`Areas:Agents`)

Konfiguration in `appsettings.json` / `appsettings.Development.json`:

| Schlüssel                                     | Bedeutung                                                                     |
| ---------------------------------------------- | ------------------------------------------------------------------------------ |
| `Areas:Agents:Llm:BaseUrl`                     | OpenAI-kompatible Basis-URL (z. B. NanoGPT)                                    |
| `Areas:Agents:Llm:ApiKey`                      | Bearer-Token; **nicht** in Git committen — User-Secrets oder Env               |
| `Areas:Agents:Llm:UseFake`                     | `true` → Scripted-LLM ohne Netz (Development-Vorgabe)                          |
| `Areas:Agents:MaxConcurrentRuns`               | Parallelität des Hintergrund-Workers                                           |
| `Areas:Agents:Pricing:*`                       | Tokenpreise für die Kostenschätzung                                            |
| `Areas:Agents:Workspace:Enabled`               | `true` → echte Workspace-Tools + Push vor `Completed`                          |
| `Areas:Agents:Workspace:RemoteUrl`             | Git-Remote für Clone/Push                                                      |
| `Areas:Agents:Workspace:LocalPath`             | Lokaler Clone (absolut oder relativ zum Content-Root)                          |
| `Areas:Agents:Workspace:BaseRef`               | Ausgangs-Ref für Worktrees (Vorgabe `main`)                                    |
| `Areas:Agents:Workspace:WorktreesRoot`         | Verzeichnis für Run-Worktrees (typisch `workspaces/`)                          |
| `Areas:Agents:Workspace:ShellTimeout`          | Timeout für `run_shell`                                                        |
| `Areas:Agents:Workspace:MaxOutputChars`        | Kürzung von stdout/stderr                                                      |
| `Areas:Agents:Billing:LowBalanceUsdThreshold`  | USD balance below this sets `lowBalance` on `GET /api/agents/billing/balance`  |

In Development ist `UseFake` standardmäßig `true`, damit `dotnet run` ohne Key startet.
`Workspace:Enabled` bleibt standardmäßig `false` (Stub-Tools und Complete im Loop wie Teilprojekt 3).

Für echte NanoGPT-Aufrufe lokal:

```
dotnet user-secrets set "Areas:Agents:Llm:ApiKey" "<dein-key>" --project src/AgentForge.Host
```

und in `appsettings.Development.json` `UseFake` auf `false` setzen (oder per Env überschreiben).
Ohne Key und mit `UseFake: false` bricht der Start mit Validierungsfehler ab.

Operator billing (host NanoGPT key): `GET /api/agents/billing/balance`, `GET .../usage`,
`GET .../deposits/limits`, `POST .../deposits` (BTC-LN only), `GET .../deposits/{txId}`.
Deposit create is rate-limited upstream (~10 / 10 min). Requires real NanoGPT when `UseFake` is false.

#### Workflow: Ein Agent-Run

```
Client                    API                    Worker                  LLM / Tools
  │                         │                        │                        │
  │  POST /api/agents/runs  │                        │                        │
  ├────────────────────────►│                        │                        │
  │                         │ Run = Pending           │                        │
  │                         │ ─► Queue ───────────────►                        │
  │◄────────────────────────┤                        │                        │
  │   201 { runId, Pending }│                        │                        │
  │                         │                        │  Run = Running          │
  │                         │                        ├───────────────────────►│
  │                         │                        │      Turn-Loop          │
  │                         │                        │  ┌──────────────────┐  │
  │                         │                        │  │ LLM antwortet     │  │
  │                         │                        │  │  → Tool-Aufruf?   │  │
  │                         │                        │  │     ja: run_shell,│  │
  │                         │                        │  │     read/write_file│ │
  │                         │                        │  │  → sonst: fertig  │  │
  │                         │                        │  └────────┬──────────┘  │
  │                         │                        │           │ wiederholen │
  │                         │                        │◄──────────┘             │
  │  GET .../runs/{id}      │                        │                        │
  ├────────────────────────►│  Polling                │                        │
  │◄────────────────────────┤                        │                        │
  │                         │                        │                        │
  │  GET .../runs/{id}/stream (SSE)                   │                        │
  ├────────────────────────►│◄───────────────────────┤  Events live           │
  │◄════════════════════════╡ streamt Turn-Updates    │                        │
  │                         │                        │                        │
  │                         │        [Workspace:Enabled = true]                │
  │                         │                        │  git worktree           │
  │                         │                        │  Branch: run/{runId}    │
  │                         │                        │  Agent committet         │
  │                         │                        │  git push  ─────────────►│ Remote
  │                         │                        │           │              │
  │                         │                        │  Run = Completed         │
  │  POST .../runs/{id}/cancel  (solange Pending/Running möglich)               │
  ├────────────────────────►│───────────────────────►│  Abbruch                │
```

## Tests

```
dotnet test
```

Architektur wird durch `tests/AgentForge.Host.Architecture` erzwungen: Bereiche dürfen
sich nur über `*.Contracts`-Projekte referenzieren, niemals direkt.

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

Docker/Container-Executor folgt in einem späteren Teilprojekt.
