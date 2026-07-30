# AgentForge — UI

**Datum:** 2026-07-30 (überarbeitet; Erstfassung 2026-07-29)
**Status:** Entwurf zur Umsetzung freigegeben
**Umfang:** Teilprojekt 6 von 6

## Ziel

Eine Oberfläche über der AgentForge-API, in der du Agenten anlegst und konfigurierst, mit einem oder mehreren Agenten planst und ausarbeitest, und konkrete Aufträge als Runs startest und verfolgst. Vorbild für den Seitenaufbau bleibt die kleine React-Anwendung im Repo `aae` (Liste mit Suche, Detail, Chat). Optik und Code-Aufbau werden neu entschieden.

**Conversation** und **Run** bleiben getrennt:

| | Conversation | Run |
|---|---|---|
| Zweck | Planen, nachfragen, Aufgabe ausarbeiten | Konkreten Auftrag ausführen |
| Werkzeuge | nur `read_file` auf dem gemeinsamen Checkout von `BaseRef` | volle Agent-Werkzeuge / Workspace (Schreiben, Shell, Push wenn aktiv) |
| Adressierung | `@mentions` — wer erwähnt wird, antwortet | genau ein Agent |
| Ergebnis | Verlauf; optional „Draft run“ | Status, Nachrichten, Usage, Abbruch |

Der bevorzugte Ablauf: im Gespräch ausarbeiten → ein Teilnehmer schlägt ein Objective vor → du bestätigst oder editierst → `POST /runs` mit optionalem `conversationId` zur Historisierung.

## Abgrenzung

**Enthalten:** Conversation-Backend (Persistenz, Mentions-Antwort-Loop, read-only Workspace-Bindung, Draft-Run, SSE), Erweiterungen an Definitions/Runs (`q`, `conversationId`), React-UI (Shell, Agenten, Runs, Gespräche, Draft-Run), Host-Auslieferung, Unit-/Integrationstests (API) und Vitest (UI).

**Nicht enthalten:** Anmeldung und Benutzerverwaltung (Host: ein Besitzer), Mandantenfähigkeit, Rollen, Mehrsprachigkeit, Ende-zu-Ende-Browser-Tests, eigene Mobilansicht, Datei-Browser für Workspaces, Kosten-Diagramme, Token-SSE (Zeichen-Streaming).

## Einordnung und Abhängigkeiten

Voraussetzungen: Host, Agents-Bereich, Runtime mit SSE, Workspace-Werkzeuge (Teilprojekte 1–4) sind umgesetzt. Dieses Teilprojekt **erweitert denselben Agents-Bereich** und liefert die UI darüber — kein separates Backend-Teilprojekt.

## Backend: Gespräche und API-Erweiterungen

Alles liegt unter `src/Areas/AgentForge.Areas.Agents/` (Domain, Persistence, Application, Http, Runtime). Registrierung weiterhin nur über `AgentsArea`.

### Bestehende Fläche (bleibt)

```
GET/POST       /api/agents/definitions
GET/PUT/DELETE /api/agents/definitions/{id}
GET/POST       /api/agents/runs
GET            /api/agents/runs/{id}
POST           /api/agents/runs/{id}/cancel   { concurrencyToken }
GET            /api/agents/runs/{id}/messages
GET            /api/agents/runs/{id}/stream
GET            /api/areas
```

**Fehler:** ProblemDetails mit Erweiterung `code` (snake_case). UI und Clients unterscheiden ausschließlich über `code`.

| `code` | HTTP |
|---|---|
| `agent_not_found` | 404 |
| `run_not_found` | 404 |
| `conversation_not_found` | 404 |
| `agent_name_taken` | 409 |
| `concurrency_conflict` | 409 |
| `agent_archived` | 409 |
| `run_invalid_transition` | 409 |
| `mention_not_participant` | 400 |
| `conversation_archived` | 409 |

**Run-SSE:** `status` \| `message` \| `usage` \| `error` \| `done`. Kein `token`, kein separates `tool`-Ereignis. Werkzeuge in Nachrichten (`toolCallsJson` / Rolle Tool). Kein `Last-Event-ID` in v1.

### Erweiterungen bestehender Endpunkte

| Änderung | Verhalten |
|---|---|
| `q` an `GET /definitions` | optional; Name enthält, case-insensitive; filtert nicht-archivierte |
| `conversationId?` an `POST /runs` und `RunResponse` | optional; muss existierende, nicht archivierte Conversation des Owners sein, sonst 400/404; nur Historisierung, ändert Run-Ausführung nicht |
| Agent-Snapshot / AllowedTools | unverändert für Runs |

### Gespräche — Modell

Ein `Conversation` hat `OwnerId`, Titel, `ConcurrencyToken`, `ArchivedAt?`, `CreatedAt`/`UpdatedAt`, und `1..N` Teilnehmer (`AgentId`). **Einzelchat = Gruppe mit einem Teilnehmer.**

`ConversationMessage`: `Sequence`, `Role`, `Content`, `ToolCallsJson`, `ToolCallId`, `SenderAgentId?`, `SenderName?` (denormalisiert zum Sendezeitpunkt), `CreatedAt`. User-Nachrichten können Mentions speichern (JSON-Array der Agent-Ids) für Audit; die Ausführung liest Mentions aus dem POST.

Archivierung: `DELETE` setzt `ArchivedAt`; Listen ohne Archivierte; Get per Id weiter möglich. Keine neuen Messages / Drafts auf archivierte Gespräche (`conversation_archived`).

### Gespräche — HTTP

```
GET    /api/agents/conversations
POST   /api/agents/conversations                 { title?, participantAgentIds[] }
GET    /api/agents/conversations/{id}
PUT    /api/agents/conversations/{id}            { title, participantAgentIds[], concurrencyToken }
DELETE /api/agents/conversations/{id}
GET    /api/agents/conversations/{id}/messages
POST   /api/agents/conversations/{id}/messages   { content, mentions[] } → 202 { streamId }
GET    /api/agents/conversations/{id}/stream     SSE (pro Conversation-Id)
POST   /api/agents/conversations/{id}/draft-run  { agentId? } → { objective, agentId }
```

Liste: `lastMessageExcerpt`, `lastMessageAt`, Teilnehmer mit Id und aktuellem Namen. Create: fehlt Titel → aus Teilnehmernamen. Mindestens ein Teilnehmer; alle Agenten müssen existieren und nicht archiviert sein.

**Mentions:** Ids müssen Teilnehmer sein, sonst `mention_not_participant`. Wer erwähnt wird, bekommt genau eine Antwort-Runde (Tool-Loop wie Run, aber siehe Werkzeuge). Mehrere Mentions → **nacheinander** in Mention-Reihenfolge (eine Rechnung/Transparenz nach der anderen; parallel wäre undurchsichtiger). Leere Mentions → speichern, kein Loop (Notiz). `@name` → Id nur in der UI.

**SSE:** dieselbe Ereignismenge wie Runs (`status`/`message`/`usage`/`error`/`done`), Bus keyed by conversation id (eigenes `IConversationEventBus` oder generic bus by Guid). `streamId` in der 202-Antwort korreliert die UI-Optimistic-Message; steht nicht in der URL. Eine EventSource-Verbindung je geöffnetem Gespräch.

Verworfen: *alle antworten immer*; *Moderator wählt* — später über Mentions-Belegung.

### Gespräche — Runtime

```
POST messages → persist User message
            → if mentions empty: done
            → else enqueue ConversationReplyJob(conversationId, streamId, agentIds[])
ConversationWorker → for each agentId in order:
                       ConversationLoop (LLM + read_file only)
                       publish message/usage/… on conversation bus
                     → publish done
```

`ConversationLoop` spiegelt `RunLoop`, Unterschiede:

1. **Werkzeuge:** dem LLM nur `read_file` anbieten, wenn `Workspace:Enabled`; sonst keine Tools. Niemals `write_file` / `run_shell`, unabhängig von `AllowedTools` des Agenten.
2. **Workspace:** kein Worktree. Vor dem Loop: Clone/Fetch sicherstellen; Arbeitsverzeichnis = konfigurierter `LocalPath` Checkout auf `BaseRef` (shared, read-only für Tools). `ReadFileTool` liest über einen Conversation-Session-Kontext mit Root = `LocalPath` (Pfadjail wie heute). Kein Push, kein Cleanup-Worktree.
3. **System-Prompt:** aktueller Prompt des Agenten (kein eingefrorener Snapshot nötig für Chat; Name für `SenderName` zum Antwortzeitpunkt).
4. **MaxTurns / Model / Temperature:** vom Agenten wie bei Runs.
5. Kein Run-Status; Conversation bleibt „offen“. Fehler einer Reply → `error`-Event + weiter mit nächstem Mention oder `done`.

Draft-Run: synchroner LLM-Aufruf (kein Stream nötig) mit Gesprächsverlauf und Anweisung, ein knappes Objective vorzuschlagen. Optionaler `agentId` muss Teilnehmer sein; sonst erster Teilnehmer. Antwort JSON/Text → `{ objective, agentId }`. Kein Run wird dabei angelegt.

### Tests (Backend)

Unit: Conversation-Domain, Service (Mentions-Validierung, Archiv), Loop mit Fake-LLM und Fake-Git/Workspace. Integration: CRUD, Note ohne Mention, Mention startet Reply (Fake LLM), Draft-Run, Create Run mit `conversationId`, `q` auf Definitions.
## Technik und Auslieferung

React 19, TypeScript, Vite, Tailwind 4, react-router 7, Vitest mit Testing Library — Stack wie `aae`. Blazor bleibt verworfen (beherrschter Chat-/Stream-Stack wiegt hier mehr als eine Toolchain).

### Ort im Repo

```
src/AgentForge.Web/
  package.json  vite.config.ts  tsconfig*.json  index.html
  src/
    main.tsx  App.tsx  index.css
    shell/
      AppShell.tsx  AreaNav.tsx  ContextPanel.tsx
    lib/
      http.ts  sse.ts  areas.ts
    areas/
      index.ts
      agents/
        routes.tsx  api.ts  types.ts
        AgentListPage.tsx  AgentFormPage.tsx  AgentDetailPage.tsx
        RunListPage.tsx  RunDetailPage.tsx  StartRunDialog.tsx
        ConversationListPage.tsx  ConversationPage.tsx  NewConversationDialog.tsx
        DraftRunDialog.tsx
        Transcript.tsx  TranscriptLog.tsx  ToolCallCard.tsx  MessageComposer.tsx
        useRunStream.ts  useConversationStream.ts  transcriptReducer.ts
    test/
      setup.ts  fakeEventSource.ts
  src/__tests__/
```

Kein .NET-Projekt, nicht in der Solution. Frontend-Tests unter `src/AgentForge.Web/src/__tests__/`, nicht unter `tests/`.

Seiten unter ~200 Zeilen; Datenholen in `api.ts` und Hooks.

### Registry

Jeder Bereich exportiert `{ slug, title, routes, nav }`; `areas/index.ts` listet explizit — analog `builder.AddArea<AgentsArea>()`. Navigation = Schnittmenge `/api/areas` ∩ Registry.

### Entwicklung und Produktion

Vite leitet `/api` an den Host weiter (SSE ungepuffert). `npm run build` → `dist`; MSBuild-Ziel am Host kopiert beim `Publish` nach `wwwroot`; SPA-Fallback auf `index.html`. Ein Origin, kein CORS. `dotnet build` ruft **kein** npm.

### Stil

Tailwind an den Elementen; `index.css` mit CSS-Variablen und Dark Mode über `prefers-color-scheme`. Dieselbe Klassenkette zum dritten Mal → Bauteil. Kein vorab gebauter Komponenten-Vorrat.

## Shell und Navigation

Drei Spalten: links Bereich + Bereichsnav (Agenten, Runs, Gespräche) + fünf zuletzt berührte Objekte; Mitte Ansicht; rechts Kontext-Steckplatz je Seite. Schmal: zuerst Kontext weg, dann linke Spalte als Schublade. Zuletzt berührt: `localStorage` (Id, Art, Beschriftung); fehlendes Objekt beim Klick entfernen.

### Routen

```
/agents/definitions
/agents/definitions/new
/agents/definitions/:id
/agents/definitions/:id/edit
/agents/runs
/agents/runs/:id
/agents/conversations
/agents/conversations/:id
```

`/` → erster Registry-Bereich; `/agents` → `/agents/definitions`.

## Die Ansichten

### Agenten-Liste

Tabelle: Name, Modell, Beschreibung, Änderungszeit. Suche über `q`, 300 ms entprellt. Seitenweise `skip`/`take` (50). Aktionen: Gespräch beginnen, Run starten, Bearbeiten, Archivieren (mit Rückfrage).

### Agenten-Formular

Anlegen und Bearbeiten eine Datei. Abschnitte: Identität, System-Prompt, Modell & Grenzen, Werkzeuge (Freitext-Plaketten für echte Tool-Namen). Clientvalidierung wie Server: Temperature 0–2, Output-Token 1–200 000, Turns 1–200, Name ≤ 100, Prompt nicht leer. `ConcurrencyToken` unsichtbar; bei `concurrency_conflict` Neuladen-Knopf, Eingaben bleiben.

### Agenten-Detail

Kopf, Prompt lesbar, Aktionen Run / Gespräch / Bearbeiten / Archivieren. Kontext: letzte Runs und Gespräche dieses Agenten.

### Run starten

Dialog: Objective, Agent (vorbelegt wenn aus Agenten-Ansicht). Danach Run-Detail. `agent_archived` → klare Meldung. Optional `conversationId`, wenn der Dialog aus einem Gespräch kommt.

### Run-Liste

Objective gekürzt, Agent, Status-Plakette, Zeiten, Dauer. Filter Agent und Status, neueste zuerst.

### Run-Detail

Verlauf als Gespräch: Objective-Bubble, Assistenten-Bubbles, Werkzeugkarten aus Nachrichten (`toolCallsJson` / Tool-Rolle) — zugeklappt, aufklappbar. Umschalter **Protokoll**: Zeilen mit Sequence, Rolle, Inhalt inkl. System an Position 0. Kontext: Status, Snapshot, Token/Kosten, Turns vs `MaxTurns` aus Snapshot; Link zum Gespräch wenn `conversationId` gesetzt. Abbrechen nur bei `Pending`/`Running` mit Token. Scroll klebt unten, bis man hochscrollt.

SSE-Ereignisse führen denselben Zustand; fehlende Token-Streams bedeuten: Assistententext erscheint mit dem `message`-Ereignis bzw. nach Nachrichten-Reload, nicht Zeichen für Zeichen — bis die API Token liefert.

### Gesprächs-Liste

Titel, Teilnehmer, letzte Nachricht, Zeit. Neues Gespräch: optionaler Titel, Mehrfachauswahl Teilnehmer; leerer Titel → aus Teilnehmernamen. Archivieren mit Rückfrage.

### Gespräch

Gleiches Verlaufs-Bauteil. Agentennachrichten mit Name und stabiler Farbe aus Agent-Id. `@` öffnet Teilnehmerauswahl; Erwähnungen als Plaketten / Ids. Ohne Erwähnung: speichern + Hinweis „nicht adressiert“.

Kontext: Teilnehmer verwalten (`PUT` + Token), Link je Agent, **Draft run**.

### Draft run

CTA auf der Gesprächsseite → Draft-Endpunkt → Dialog mit vorgeschlagenem Objective und Agent (beides editierbar) → `POST /runs` inkl. optionalem `conversationId` → Navigation zum Run-Detail.

## Datenfluss

`lib/http.ts` + Hooks ohne Abfrage-Bibliothek. Listen nach Mutation bewusst neu laden.

`lib/sse.ts` + `useRunStream` / `useConversationStream` → gemeinsamer `transcriptReducer` (rein, ohne DOM getestet). Öffnen: erst Messages, dann Stream; Nachrichten in Map nach `Sequence`/`Id`, nicht nur anhängen.

Eigene Nachricht sofort „sendet“, ersetzt durch `message` oder Retry bei Fehler. Verbindungsabriss: Browser-Reconnect wo möglich; Hinweis; nach wiederholtem Scheitern Neuladen-Knopf. Listen strömen nicht.

## Fehlerbehandlung

| Fall | UI |
|---|---|
| 400 Validierung | Feldfehler, Eingaben bleiben |
| 404 (`agent_not_found`, `run_not_found`, …) | Nicht-gefunden-Ansicht → Liste |
| `agent_name_taken` | Fehler am Namensfeld |
| `concurrency_conflict` | „anderswo geändert“ + Neuladen |
| `run_invalid_transition` | Hinweis, Ansicht aktualisieren |
| `agent_archived` | Hinweis mit Bezug zum Agenten |
| 500 | allgemeine Meldung, Korrelations-Id wenn vorhanden |
| Netz weg | Hinweis + Wiederholen |

Leere Zustände formulieren den nächsten Schritt („noch keine Agenten · Agent anlegen“).

## Tests

Vitest + Testing Library; `fetch` gestubbt; `fakeEventSource.ts`. Mindestens:

- Token-/Message-Pfad soweit die API ihn liefert; ohne Token: Nachricht erscheint vollständig über `message` / Reload
- Werkzeugkarte aus Nachrichten-Payload, aufklappbar
- Reconnect/Wiederholung ohne doppelte Nachricht (Id/Sequence)
- `error` beendet Laden und zeigt Meldung
- Draft-Run-Dialog → Create Run mit `conversationId`
- Mentions und leere Mentions (Notiz)
- je ein Test für die Fehler-`code`s oben
- `transcriptReducer` direkt: Lücken, Duplikate, Reihenfolge

a11y: Verlauf `role="log"` `aria-live="polite"`, Labels, Tabellenköpfe, Fokus in Dialogen. Keine E2E in v1.

## Fertigstellungskriterien

1. `dotnet test` (Agents.Unit + Host.Integration) sowie `npm run build`, `npm run lint` und `npm test` ohne Fehler und ohne Warnungen.
2. Agent anlegen, bearbeiten, archivieren; Suche über `q`; archivierter fehlt in der Liste, per Id erreichbar.
3. Run starten; Verlauf über Messages + Stream; Werkzeugkarten; Protokoll-Umschalter; Abbrechen und zweiter Abbruch mit `run_invalid_transition` / Konflikt-Meldung; optional `conversationId` am Run.
4. Gruppengespräch mit mehreren Agenten; `@` adressiert; Absender an Name und Farbe unterscheidbar; Conversation-Agent darf `read_file` (wenn Workspace an), niemals schreiben/shell.
5. Nachricht ohne Erwähnung wird gespeichert und als nicht adressiert gekennzeichnet; kein LLM-Lauf.
6. Draft run schlägt Objective vor; nach Bestätigung entsteht ein Run mit `conversationId`; Run-Detail verlinkt zurück.
7. Jeder Fall der Fehlertabelle ist durch einen Test belegt (API und/oder UI).
8. Host liefert das Frontend; Neuladen auf `/agents/runs/:id` kein 404.
9. Bereichsnavigation = `/api/areas` ∩ Registry.

## Annahmen

- Teilprojekte 1–4 sind umgesetzt. Gespräche, Draft-Run, `q`, `conversationId` und der read-only Conversation-Loop sind Teil dieses Vorhabens (siehe Backend-Abschnitt und Plan).
- Host: ein Besitzer (`LocalSingleUser`); keine Anmeldemaske.
- Node/npm auf dem Entwicklungsrechner; Entwicklung unter Windows.
- Ströme ungepuffert (Host und Vite-Proxy).
- Größenordnung Dutzende Agenten/Gespräche; kein virtuelles Scrollen.
