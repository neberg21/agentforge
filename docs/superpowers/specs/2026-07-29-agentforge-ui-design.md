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

**Enthalten:** Shell mit Bereichsnavigation, Agenten-Verwaltung, Run-Verwaltung samt Stream, Einzel- und Gruppengespräche mit Erwähnungen, Draft-Run-Übergang, Fehlerbehandlung, Auslieferung durch den Host, Vitest-Aufbau.

**Nicht enthalten:** Anmeldung und Benutzerverwaltung (Host: ein Besitzer), Mandantenfähigkeit, Rollen, Mehrsprachigkeit, Ende-zu-Ende-Tests, eigene Mobilansicht, Datei-Browser für Workspaces, Kosten-Diagramme, die detaillierte Backend-Spezifikation von Teilprojekt 3b (nur die geforderte API-Fläche unten).

## Einordnung und Abhängigkeiten

Voraussetzungen: Host, Agents-Bereich, Runtime mit SSE, Workspace-Werkzeuge (Teilprojekte 1–4) sind umgesetzt und bilden die Fläche, an die diese UI gebunden wird.

Gespräche, Draft-Run und optionales `conversationId` am Run existieren in der API **noch nicht**. Sie werden als Teilprojekt **3b** nachgeliefert. Dieses Dokument spezifiziert nur die Fläche, die die UI braucht; entworfen wird 3b getrennt. Ohne 3b sind Agenten- und Run-Ansichten baubar; Chat- und Draft-Kriterien sind dann blockiert.

## API-Fläche

### Bereits umgesetzt (UI bindet 1:1)

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

**Fehler:** ProblemDetails nach RFC 9457 mit Erweiterung `code` (snake_case). Die UI unterscheidet Fälle ausschließlich über `code`, niemals über Meldungstexte und nicht über Pfadsegmente von `type`. Bekannte Codes:

| `code` | HTTP |
|---|---|
| `agent_not_found` | 404 |
| `run_not_found` | 404 |
| `agent_name_taken` | 409 |
| `concurrency_conflict` | 409 |
| `agent_archived` | 409 |
| `run_invalid_transition` | 409 |

**Run-SSE heute:** Ereignisarten `status`, `message`, `usage`, `error`, `done`. Es gibt keine `token`-Ereignisse und kein separates `tool`-Ereignis. Werkzeugaufrufe stecken in den persistierten Nachrichten (`toolCallsJson` / Rolle Tool). Die UI lädt den Verlauf per `GET .../messages` und aktualisiert ihn bei `message` (und bei Bedarf durch erneutes Laden). `Last-Event-ID` wird vom Server noch nicht geliefert; die UI verlässt sich darauf nicht.

**AllowedTools** haben Bedeutung (`read_file`, `write_file`, `run_shell`). Die Modellauswahl im Formular bleibt freies Textfeld; eine Modell-Liste vom Server gibt es nicht.

### Kleine Erweiterungen bestehender Endpunkte (von 3b / Host erwartet)

| Änderung | Grund |
|---|---|
| `q` an `GET /api/agents/definitions` (Name enthält, case-insensitive) | Suchfeld ohne vorgetäuschte Vollständigkeit clientseitig über eine Seite |
| optionales `conversationId` an `CreateRunRequest` | Historisierung Run ↔ Gespräch |
| Draft-Run-Endpunkt (oder gleichwertig vereinbart) | Objective aus dem Thread vorschlagen lassen |

### Gespräche (neu, Teilprojekt 3b — nur Fläche)

Ein `Conversation` hat einen Titel und `1..N` Agenten als Teilnehmer. **Der Einzelchat ist die Gruppe mit einem Teilnehmer** — eine Entität, eine Ansicht.

```
GET    /api/agents/conversations
POST   /api/agents/conversations                 {title?, participantAgentIds[]}
GET    /api/agents/conversations/{id}            Teilnehmer mit Id und Name
PUT    /api/agents/conversations/{id}            Titel und Teilnehmer, ConcurrencyToken
DELETE /api/agents/conversations/{id}            archiviert (ArchivedAt), wie Agenten
GET    /api/agents/conversations/{id}/messages
POST   /api/agents/conversations/{id}/messages   {content, mentions[]} → 202 + streamId
GET    /api/agents/conversations/{id}/stream     SSE
POST   /api/agents/conversations/{id}/draft-run  [{ agentId? }] → { objective, agentId }
```

Listenantwort: Auszug der letzten Nachricht und Zeitstempel. Erwähnte Agenten müssen Teilnehmer sein; sonst 400. Archivierung wie bei Agenten.

**Adressierung:** `mentions` = Agent-Ids. Wer erwähnt wird, antwortet. Leere Liste = Nachricht wird gespeichert, niemand läuft (Notiz). `@name` → Id löst die UI auf, nicht der Server.

**Werkzeuge in Gesprächen:** immer nur `read_file` (wenn Workspace aktiv), gegen denselben konfigurierten Workspace-Remote wie Runs, read-only auf einem gemeinsamen Checkout von `BaseRef` — kein Worktree pro Gespräch, kein `write_file`, kein `run_shell`, kein Push. Die `AllowedTools` des Agenten gelten für Runs; in Gesprächen werden Schreib-/Shell-Werkzeuge nie angeboten, auch wenn sie am Agenten stehen. Fehlt Workspace-Konfiguration, antworten Gesprächs-Agenten ohne Dateizugriff.

**Draft run:** Die UI ruft den Draft-Endpunkt auf (optional mit bevorzugtem `agentId` unter den Teilnehmern; sonst wählt 3b einen). Antwort: `{ objective, agentId }`. Dialog zum Bestätigen/Editieren von Objective und Agent, dann `POST /api/agents/runs` mit Objective, `agentId` und optionalem `conversationId`.

**Ströme:** `EventSource` nur GET → `POST` Nachricht liefert `202` + `streamId`, dann `GET .../stream`. Eine Verbindung je geöffnetem Gespräch. Conversation-SSE soll dieselben Ereignisnamen wie Runs nutzen, wo möglich, damit ein Transcript-Reducer reicht. Die UI v1 muss mit dem heutigen Run-Ereignissatz (`status` | `message` | `usage` | `error` | `done`) auskommen; spätere `token`/`tool`-Ereignisse sind optional aufnehmbar.

Verworfen für Mentions: *alle antworten immer* und *Moderator-Agent wählt* — später nachrüstbar über andere Belegung von `mentions`.

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

1. `npm run build`, `npm run lint` und `npm test` ohne Fehler und ohne Warnungen.
2. Agent anlegen, bearbeiten, archivieren; archivierter fehlt in der Liste, per Id erreichbar.
3. Run starten; Verlauf über Messages + Stream; Werkzeugkarten; Protokoll-Umschalter; Abbrechen und zweiter Abbruch mit `run_invalid_transition` / Konflikt-Meldung.
4. Gruppengespräch mit mehreren Agenten; `@` adressiert; Absender an Name und Farbe unterscheidbar.
5. Nachricht ohne Erwähnung wird gespeichert und als nicht adressiert gekennzeichnet.
6. Draft run schlägt Objective vor; nach Bestätigung entsteht ein Run mit optionalem `conversationId`; Run-Detail verlinkt zurück.
7. Jeder Fall der Fehlertabelle ist durch einen Test belegt.
8. Host liefert das Frontend; Neuladen auf `/agents/runs/:id` kein 404.
9. Bereichsnavigation = `/api/areas` ∩ Registry.

Kriterien 4–6 setzen 3b voraus.

## Annahmen

- Teilprojekte 1–4 sind umgesetzt; 3b liefert Gespräche, Draft-Run, `q`, optionales `conversationId` und read-only `read_file` auf gemeinsamem `BaseRef`-Checkout.
- Host: ein Besitzer (`LocalSingleUser`); keine Anmeldemaske.
- Node/npm auf dem Entwicklungsrechner; Entwicklung unter Windows.
- Ströme ungepuffert (Host und Vite-Proxy).
- Größenordnung Dutzende Agenten/Gespräche; kein virtuelles Scrollen.
