# AgentForge.Areas.Agents

Der `agents`-Bereich (`IArea.Slug = "agents"`) verwaltet Agent-Definitionen, führt
sie als **Runs** (Ziel-basiert, autonom) oder in **Conversations** (Chat, Mensch ↔
mehrere Agenten) aus und rechnet den LLM-Verbrauch über NanoGPT-Billing ab.

```
AgentForge.Areas.Agents/
│
├── AgentsArea.cs                 IArea-Implementierung, DI-Registrierung, Routing
│
├── Domain/                       reines Domänenmodell, kennt kein EF Core/ASP.NET
│   ├── Agent.cs, AgentDefinition.cs, AgentSnapshot.cs
│   ├── Run.cs, RunMessage.cs, RunStatus.cs, RunTransitions.cs
│   └── Conversation.cs, ConversationMessage.cs, ConversationParticipant.cs, MessageRole.cs
│
├── Application/                  Use-Cases über die Domäne
│   ├── AgentService.cs, AgentSuggestionService.cs, BuilderSessionService.cs
│   ├── RunService.cs, ConversationService.cs, ConversationTitleService.cs
│   └── BillingService.cs, Paging.cs
│
├── Http/                         Minimal-API Endpoints + Request/Response-DTOs
│   ├── AgentEndpoints.cs   → /api/agents/definitions, /api/agents/builder/session
│   ├── RunEndpoints.cs     → /api/agents/runs
│   ├── ConversationEndpoints.cs → /api/agents/conversations
│   └── BillingEndpoints.cs → /api/agents/billing
│
├── Runtime/                      der eigentliche Ausführungsmotor
│   ├── RunLoop.cs                Turn-Loop für einen einzelnen Run
│   ├── ConversationLoop.cs       Turn-Loop für eine Chat-Antwort
│   ├── CostEstimator.cs, AgentsOptions.cs
│   ├── Llm/                      ILlmClient: Scripted (Fake) oder OpenAI-kompatibel (HTTP)
│   ├── Tools/                    ITool: read_file, write_file, run_shell
│   ├── Workspace/                Git-Worktree pro Run (GitCliWorkspace)
│   ├── Billing/                  NanoGPT-Account-Client (Fake oder HTTP)
│   ├── Queue/                    Channel-basierte Warteschlangen + HostedServices
│   └── Events/                   In-Process Event-Bus für SSE-Streaming
│
└── Persistence/
    ├── AgentsDbContext.cs, EntityConfigurations.cs
    └── Migrations/
```

## Endpoints

| Bereich       | Methode & Pfad                                  | Zweck                                  |
| ------------- | ------------------------------------------------ | --------------------------------------- |
| Definitionen  | `GET /api/agents/definitions`                    | Liste, Suche `q`, Paging                |
|               | `GET /api/agents/definitions/{id}`                | Einzelner Agent                         |
|               | `POST /api/agents/definitions`                    | Anlegen                                 |
|               | `PUT /api/agents/definitions/{id}`                | Ändern (Concurrency-Token)              |
|               | `DELETE /api/agents/definitions/{id}`             | Archivieren                             |
|               | `GET /api/agents/definitions/suggestions`         | Namensvorschlag (Bogus, deutsche Vornamen) |
|               | `POST /api/agents/builder/session`                | Startet eine Konversation zum Bauen eines Agenten |
| Runs          | `GET /api/agents/runs`                            | Liste, Filter `agentId`, `status`       |
|               | `GET /api/agents/runs/{id}`                       | Einzelner Run                           |
|               | `POST /api/agents/runs`                           | Neuer Run (`agentId`, `objective`, `conversationId?`) |
|               | `POST /api/agents/runs/{id}/cancel`                | Abbrechen (nur Pending/Running)         |
|               | `GET /api/agents/runs/{id}/messages`               | Run-Transkript                          |
|               | `GET /api/agents/runs/{id}/stream`                 | SSE: `status`, `message`, `usage`, `error`, `done` |
| Conversations | `GET /api/agents/conversations`                   | Liste                                   |
|               | `GET|POST|PUT|DELETE /api/agents/conversations/{id}` | CRUD, Archivieren                    |
|               | `PATCH /api/agents/conversations/{id}/title`      | `set` / `lock` / `resume` (Auto-Titel)  |
|               | `POST /api/agents/conversations/{id}/messages`     | Nachricht posten → 202 mit `streamId`   |
|               | `GET /api/agents/conversations/{id}/stream`        | SSE der Konversation                    |
|               | `POST /api/agents/conversations/{id}/draft-run`    | Schlägt `objective` + `agentId` für einen Run vor |
| Billing       | `GET /api/agents/billing/balance` \| `/usage` \| `/deposits/limits` | NanoGPT Operator-Konto      |
|               | `POST /api/agents/billing/deposits`                | Einzahlung anstoßen (nur BTC-LN)        |
|               | `GET /api/agents/billing/deposits/{txId}`          | Einzahlungsstatus                       |

## Run-Lifecycle (State Machine)

`RunTransitions` erlaubt nur folgende Übergänge:

```
                 ┌───────────┐
        ┌───────►│  Pending  │
        │        └─────┬─────┘
        │              │ RunWorker holt Run aus Queue
        │              ▼
        │        ┌───────────┐        max_turns erreicht,
        │        │  Running  │───────► ohne finale Antwort
        │        └─────┬─────┘               │
        │      ┌───────┼────────┐            ▼
        │      ▼       ▼        ▼      ┌───────────┐
        │ ┌─────────┐┌────────┐┌──────►│  Failed    │
        │ │Completed││Cancelled│      │(terminal)  │
        │ │(terminal)││(terminal)│      └───────────┘
        │ └─────────┘└────────┘
        │
   POST /runs/{id}/cancel  (nur aus Pending/Running möglich)

Terminalzustände (Completed, Failed, Cancelled) haben keine ausgehenden
Übergänge — RunTransitions.IsAllowed(...) liefert dort immer false.
```

## Workflow: `RunLoop.ExecuteAsync` (ein einzelner Run)

```
                         ┌────────────────────────────┐
                         │  Run laden, Status prüfen    │
                         │  (nur Pending wird gestartet) │
                         └──────────────┬───────────────┘
                                        │ MarkRunning()
                                        ▼
                    ┌────────────────────────────────────────┐
              ┌────►│  turns < MaxTurns ?                     │
              │     └───────────────┬──────────────────────-─┘
              │           ja        │        nein
              │                     ▼                └──► Fail("max_turns exceeded")
              │        cancelled? ──yes──► Cancel + Done-Event, Ende
              │           │no
              │           ▼
              │  ┌──────────────────────────┐
              │  │ Verlauf laden             │
              │  │ LLM-Request bauen         │
              │  │ ILlmClient.CompleteAsync  │
              │  └───────────┬───────────────┘
              │              ▼
              │  Assistant-Message speichern + Usage/Kosten fortschreiben
              │  Event: message, usage
              │              │
              │   ToolCalls == 0 ?
              │     ├─ ja, Workspace deaktiviert → Run.Complete(), Event done, ENDE
              │     ├─ ja, Workspace aktiviert   → ENDE ohne Complete (Workspace-Push folgt separat)
              │     └─ nein ──────────────────────┐
              │                                    ▼
              │                     für jeden ToolCall:
              │                       cancelled? → Cancel, Ende
              │                       IToolRegistry.ExecuteOrErrorAsync(name, args)
              │                       Tool-Message speichern, Event: message
              │                    turns++
              └───────────────────────────────────┘

Tools (nur wenn Workspace:Enabled): read_file, write_file, run_shell
  → laufen im Git-Worktree des Runs (Branch run/{runId})
  → Push erfolgt vor dem finalen Completed (separater Schritt, s. Haupt-README)
```

## Workflow: `ConversationLoop.ExecuteReplyAsync` (Chat-Antwort eines Agenten)

Unterschied zum Run: mehrere Teilnehmer, eingeschränktes Toolset, kein eigener
Run-Status — die Antwort hängt direkt in der Konversation.

```
Client                  ConversationEndpoints        ConversationReplyWorker      ConversationLoop
  │                             │                              │                          │
  │ POST /conversations/{id}/messages                          │                          │
  ├────────────────────────────►                                                          │
  │        202 { streamId }    │                                                          │
  │◄────────────────────────────┤  → ConversationReplyQueue ───►                          │
  │                             │                              ├─────────────────────────►│
  │ GET /conversations/{id}/stream (SSE)                       │        Turn-Loop          │
  ├────────────────────────────►◄──────────────────────────────────────────────────────────┤
  │◄════════════════════════════╡ status: Running                                          │
  │                             │                                                          │
  │                             │        while turns < agent.MaxTurns:                     │
  │                             │          Verlauf laden (system prompt + history)          │
  │                             │          LLM-Antwort holen                                │
  │                             │          Assistant-Message anhängen → Event: message      │
  │                             │                                                          │
  │                             │          ToolCalls?                                       │
  │                             │            name == "read_file" → ausführen                │
  │                             │            sonst               → "tool_not_allowed_       │
  │                             │                                   in_conversation"         │
  │                             │            keine ToolCalls      → ENDE (Antwort fertig)   │
  │◄════════════════════════════╡ weitere message-Events / Done                             │
```

Nur `read_file` ist in Konversationen erlaubt (schreibende Tools und `run_shell`
sind dem Run-Kontext vorbehalten) — jeder andere Toolcall wird sofort mit einem
Fehler-Tool-Result beantwortet, ohne das LLM erneut aufzurufen.

## Hintergrund-Worker

Drei `IHostedService`s ziehen Arbeit aus Channel-basierten Queues:

```
IRunQueue              → RunWorker              → RunLoop.ExecuteAsync
IConversationReplyQueue→ ConversationReplyWorker → ConversationLoop.ExecuteReplyAsync
IConversationTitleQueue→ ConversationTitleWorker → ConversationTitleService
```

`Areas:Agents:MaxConcurrentRuns` begrenzt die Parallelität des `RunWorker`.

## LLM- und Billing-Clients

Beide folgen demselben Fake/HTTP-Umschaltmuster über `Areas:Agents:Llm:UseFake`
(und automatisch `true` in der Umgebung `Testing`):

| Interface               | Fake                  | Echt (HTTP)                     |
| ------------------------ | ---------------------- | -------------------------------- |
| `ILlmClient`              | `ScriptedLlmClient`    | `OpenAiCompatibleLlmClient`      |
| `INanoGptAccountClient`   | `FakeNanoGptAccountClient` | `NanoGptAccountClient`       |

## Tests

```
dotnet test --project backend/tests/AgentForge.Areas.Agents.Unit
```

Abgedeckt u. a.: `RunTransitionsTests`, `RunLoopTests`, `RunLifecycleTests`,
`ConversationLoopTests`, `WorkspaceToolTests`, `BillingServiceTests`,
`ToolRegistryTests`, `PersistenceTests`.
