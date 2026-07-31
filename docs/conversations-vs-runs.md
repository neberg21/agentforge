# Conversations vs. Runs

Der `agents`-Bereich bietet zwei unterschiedliche Arten, einen Agenten arbeiten
zu lassen. Beide teilen sich Agent-Definitionen und die LLM-Infrastruktur,
verfolgen aber unterschiedliche Ziele.

## Die Kernidee in einem Satz

- **Run** = *"Erledige dieses eine Ziel autonom, mit Datei-/Shell-Zugriff, bis fertig oder abgebrochen."*
- **Conversation** = *"Chat mit einem oder mehreren Agenten, Runde für Runde, wie in einem Messenger."*

```
                     RUN                             CONVERSATION
              ┌───────────────────┐           ┌───────────────────────┐
  Auslöser    │ objective (Ziel)   │           │ Chat-Nachricht          │
              │ einmal gesetzt     │           │ beliebig oft, hin & her │
              └───────────────────┘           └───────────────────────┘

  Teilnehmer  1 Agent                          1 Mensch + 1..n Agenten
                                                (@mentions möglich)

  Werkzeuge   read_file, write_file,           nur read_file
              run_shell (Git-Worktree)         (keine Schreibrechte, kein Shell)

  Status      eigene State Machine             kein eigener Status;
              Pending→Running→                 die Konversation lebt weiter,
              Completed/Failed/Cancelled       einzelne Antworten laufen durch

  Ende        Agent liefert finale Antwort     jede Antwort endet für sich;
              ohne weiteren Tool-Call,         die Konversation als Ganzes
              oder max_turns erreicht          endet nie von selbst

  Ergebnis    ggf. Code-Änderungen,            reiner Diskussions-/Planungs-
              committed + gepusht              verlauf, nichts wird geschrieben
              (bei Workspace:Enabled)

  Typischer   "Schreibe Unit-Tests für         "Wie würdest du das Problem X
  Zweck       den BillingService und           angehen? Lass uns die Optionen
              committe sie."                   durchsprechen."
```

## Warum diese Trennung?

Ein Run ist scharf: er darf schreiben, committen, `run_shell` ausführen — er
soll ein Ergebnis produzieren. Eine Conversation ist bewusst **read-only**
(nur `read_file`), damit man mit einem Agenten frei brainstormen, Pläne
absprechen oder Code lesen lassen kann, ohne dass versehentlich etwas
verändert wird. Der `draft-run`-Endpunkt bildet die Brücke: Aus einer
Konversation heraus lässt sich ein konkretes `objective` für einen Run
ableiten, den man dann bewusst separat startet.

```
   Diskussion in einer Conversation
              │
              │  "Das klingt nach einem klaren Auftrag"
              ▼
   POST /conversations/{id}/draft-run
              │
              │  { objective, agentId }  (Vorschlag, noch nicht gestartet)
              ▼
   POST /runs  { agentId, objective, conversationId }
              │
              ▼
   Run läuft autonom mit Schreibrechten
```

## Beispiel 1: Ein Run

**Ziel:** Ein Agent soll eigenständig Tests schreiben und pushen.

```
POST /api/agents/runs
Content-Type: application/json

{
  "agentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "objective": "Schreibe Unit-Tests für BillingService.GetBalanceAsync und stelle sicher, dass dotnet test grün ist."
}
```

Antwort (`201 Created`):

```
{
  "id": "b2c1...",
  "agentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "objective": "Schreibe Unit-Tests für BillingService..."
}
```

Der `RunWorker` holt den Run aus der Queue, setzt ihn auf `Running`, und der
Agent arbeitet in einem eigenen Git-Worktree (`run/b2c1...`). Fortschritt via:

```
GET /api/agents/runs/b2c1.../stream        (SSE: message, usage, status, done)
GET /api/agents/runs/b2c1.../messages       (kompletter Transkript danach)
```

Am Ende: `status: "Completed"`, die Änderungen sind auf dem Remote gepusht
(sofern `Workspace:Enabled` gesetzt ist).

## Beispiel 2: Eine Conversation

**Ziel:** Mit einem Agenten die Architektur eines neuen Features besprechen —
ohne dass er etwas verändert.

```
POST /api/agents/conversations
Content-Type: application/json

{
  "title": "Architektur: Notification-Area",
  "participantAgentIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
}
```

Nachricht senden:

```
POST /api/agents/conversations/{id}/messages
Content-Type: application/json

{
  "content": "@Aurora Wie würdest du eine neue Area für Benachrichtigungen strukturieren?",
  "mentions": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
}
```

Antwort (`202 Accepted`): `{ "streamId": "..." }` — die eigentliche Antwort
kommt asynchron über:

```
GET /api/agents/conversations/{id}/stream
```

Fragt der Agent währenddessen eine Datei ab, ist nur `read_file` erlaubt;
ein `write_file`- oder `run_shell`-Aufruf würde sofort mit
`tool_not_allowed_in_conversation` beantwortet, ohne dass etwas geschieht.

## Faustregel

| Situation                                              | Wahl         |
| -------------------------------------------------------- | ------------- |
| "Agent, mach X und liefere ein Ergebnis ab."              | **Run**       |
| "Ich will mit dem Agenten reden / einen Plan absprechen." | **Conversation** |
| Mehrere Agenten sollen gemeinsam diskutieren               | **Conversation** (mehrere `participantAgentIds`) |
| Ergebnis soll committed/gepusht werden                     | **Run**       |
| Nur lesen, nichts verändern                                 | **Conversation** |
| Aus einer Diskussion soll ein konkreter Auftrag werden      | Conversation → `draft-run` → **Run** |
