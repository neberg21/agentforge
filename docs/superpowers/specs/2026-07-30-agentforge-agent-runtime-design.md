# AgentForge — Agent-Runtime

**Datum:** 2026-07-30
**Status:** Entwurf zur Umsetzung freigegeben
**Umfang:** Teilprojekt 3 von 6

## Ziel

Runs sollen wirklich laufen: nach dem Anlegen nimmt ein Hintergrundarbeiter den Auftrag an, spricht über eine OpenAI-kompatible Schnittstelle mit NanoGPT, führt einen Tool-Calling-Loop aus, speichert Nachrichten und Nutzungsdaten und meldet den Fortschritt per Server-Sent Events — ohne echte Datei- oder Shell-Werkzeuge und ohne Container.

Am Ende dieses Teilprojekts gilt: ein angelegter Run wechselt von selbst nach `Running` und endet in `Completed`, `Failed` oder `Cancelled`; der Nachrichtenverlauf wächst; Token und Kostenschätzung sind gesetzt; Clients können pollen oder dem Stream folgen.

## Abgrenzung

**Enthalten:** LLM-Client hinter einer Schnittstelle (NanoGPT HTTP in Betrieb, Attrappe in Tests), Tool-Registry mit Stub-Implementierungen, Turn-Loop mit `MaxTurns`, Hintergrundwarteschlange mit begrenzter Parallelität, erweiterte Run-Zustandsübergänge, SSE-Endpunkt, Bereichskonfiguration `Areas:Agents:*`, Kostenschätzung aus konfigurierten Preisen, Unit- und Integrationstests mit Fake-LLM.

**Nicht enthalten:** Echte Datei-/Shell-/Container-Werkzeuge und Workspaces (Teilprojekt 4), Gespräche und deren Ereignisströme (Teilprojekt 3b), UI (Teilprojekt 6), PostgreSQL/Neon und EF-Migrationen, echte Authentifizierung, Mandantenfähigkeit, Live-NanoGPT in CI.

## Einordnung

Teilprojekte 1 und 2 sind umgesetzt: Host, Area-Konvention, Agents-Bereich mit Definitionen, Runs und OpenAI-förmigen Nachrichten. `AllowedTools` wird gespeichert, aber noch nicht ausgewertet. Erlaubt war bisher nur `Pending → Cancelled`. Felder `StartedAt`, Token und `CostEstimate` blieben leer.

Dieses Teilprojekt füllt den Motor. Teilprojekt 4 ersetzt Stub-Werkzeuge durch Container-Ausführung, ohne den Loop umzubauen. Teilprojekt 3b und die UI setzen später auf denselben SSE-/Ereignisstil auf.

## Grundentscheidungen

1. **Runtime bleibt im Agents-Bereich.** Neue Typen liegen unter `src/Areas/AgentForge.Areas.Agents/Runtime/`. Der Host registriert weiterhin nur `AddArea<AgentsArea>()`.
2. **Werkzeuge sind steckbar.** `ITool` / `IToolRegistry`. In diesem Teilprojekt liefern Stubs deterministische Antworten; unbekannte Werkzeugnamen erzeugen eine strukturierte Tool-Fehlerantwort, keinen Prozessabsturz.
3. **Anlegen startet die Ausführung.** `POST /api/agents/runs` speichert den Run als `Pending`, stellt ihn in die Warteschlange und antwortet sofort mit `201`. Es gibt keinen separaten Start-Endpunkt.
4. **Beobachtung: SSE und Polling.** Bestehende GET-Endpunkte bleiben. Neu: `GET /api/agents/runs/{id}/stream`.
5. **Tests ohne Netz.** Unit- und Integrationstests nutzen einen Fake-`ILlmClient`. Echter NanoGPT-Zugriff nur, wenn konfiguriert (manuell/Dev), nicht in CI.
6. **Begrenzte Parallelität.** `MaxConcurrentRuns` in der Bereichskonfiguration (Vorgabe klein, z. B. 2).

## Architektur

```
POST /runs ──► RunService.CreateAsync
                  │ persist Pending + System/User-Nachrichten
                  ▼
               IRunQueue.Enqueue(runId)
                  ▼
            RunWorker (BackgroundService)
                  │ Semaphore(MaxConcurrentRuns)
                  ▼
               RunLoop
                  │◄── ILlmClient (NanoGPT | Fake)
                  │◄── IToolRegistry / ITool (Stubs)
                  │◄── AgentsDbContext (Nachrichten, Status, Usage)
                  ▼
            IRunEventBus ──► SSE-Abonnenten
```

| Baustein | Verantwortung |
|---|---|
| `ILlmClient` | Chat-Completions; liefert Assistenteninhalt, optionale `tool_calls`, Usage |
| `ITool` / `IToolRegistry` | Werkzeuge nach Name; Stubs für Namen aus dem Snapshot |
| `RunLoop` | Orchestrierung eines Runs bis Terminalzustand |
| `IRunQueue` / `RunWorker` | In-Process-Warteschlange (`Channel`) und parallele Ausführung |
| `IRunEventBus` | Fan-out von Run-Ereignissen an SSE |
| `AgentsOptions` | `Areas:Agents:*`, `ValidateOnStart` |

### Dateischnitt (Orientierung)

Unter `Runtime/`: Client, Tool-Verträge und Stubs, Queue/Worker, Loop, Event-Bus, Options. Http ergänzt den Stream-Endpunkt; Domain öffnet die Zustandsübergänge und ggf. Mutatoren am `Run` (Start, Complete, Fail, Usage). Application/`RunService` enqueued nach dem Speichern.

## Zustandsübergänge

| Von | Nach |
|---|---|
| `Pending` | `Running`, `Cancelled` |
| `Running` | `Completed`, `Failed`, `Cancelled` |
| `Completed` / `Failed` / `Cancelled` | — |

`StartedAt` wird beim Wechsel nach `Running` gesetzt. `CompletedAt` (und bei Fehler `Error`) bei jedem Terminalzustand, einschließlich Abbruch.

**Abbruch:** Der bestehende Cancel-Endpunkt bleibt. Der Loop prüft kooperativ zwischen LLM- und Werkzeugschritten (Status bzw. Cancellation). Ein bereits geschriebener Turn bleibt stehen; der Abbruch greift vor dem nächsten Turn. Der Worker startet keinen Run, der bereits `Cancelled` ist.

## Turn-Loop

1. Run laden; wenn nicht `Pending` (oder bereits terminal), abbrechen/überspringen.
2. `Pending → Running`, `StartedAt` setzen, Ereignis `status` veröffentlichen.
3. Nachrichten in OpenAI-Reihenfolge an `ILlmClient` übergeben; Modell, Temperatur, `MaxOutputTokens` und erlaubte Tools stammen aus dem **AgentSnapshot** des Runs.
4. Assistentennachricht speichern (`Content` und/oder `ToolCallsJson`).
5. Gibt es `tool_calls`: für jeden Aufruf Registry fragen, Stub oder Fehlergebnis als `Tool`-Nachricht mit `ToolCallId` speichern; weiter bei Schritt 3.
6. Keine `tool_calls`: `Running → Completed`.
7. Nach jedem erfolgreichen Speichern: Usage kumulieren, `CostEstimate` aktualisieren, Ereignisse `message` / `usage` senden.
8. Abbruch erkannt → `Cancelled`. LLM-/Transportfehler oder erschöpftes `MaxTurns` ohne finalen Assistentenabschluss → `Failed` mit klarer `Error`-Meldung. Abschließend Ereignis `done` (bei Fehler zusätzlich `error`).

`MaxTurns` zählt LLM-Runden (Assistentenantworten), nicht einzelne Tool-Nachrichten. Snapshot-`MaxTurns` gilt, nicht ein globaler Default zur Laufzeit.

## Werkzeuge in diesem Teilprojekt

Für jeden Namen in `AgentSnapshot.AllowedTools` existiert ein Stub, der eine kurze, deterministische JSON-/Textantwort liefert (z. B. Erfolg inkl. Werkzeugname und Hinweis „stub“). Vom Modell angeforderte unbekannte Namen erzeugen eine Tool-Nachricht mit Fehlerinhalt, damit das Modell ggf. reagieren kann — kein Host-Absturz.

Echte Semantik von Datei- und Shell-Werkzeugen kommt erst mit Teilprojekt 4 an derselben `ITool`-Naht.

## HTTP und SSE

Unverändert: Anlegen, Listen, Lesen, Nachrichten, Abbruch; Polling wie bisher.

Neu:

```
GET /api/agents/runs/{id}/stream
```

- Medienyp: `text/event-stream`
- Gleiche Sichtbarkeit wie andere Bereichsendpunkte (fremder Besitzer → wie Get: nicht gefunden)
- Ereignistypen: `status`, `message`, `usage`, `error`, `done`
- Payload JSON; `done` beendet den Stream
- Spät kommende Abonnenten erhalten den weiteren Verlauf ab Anmeldung; der vollständige Stand bleibt über GET erreichbar (kein verpflichtendes Replay-Protokoll in diesem Teilprojekt)

`POST /api/agents/runs` antwortet weiter sofort mit `201` und Status `Pending`.

## Konfiguration

Abschnitt `Areas:Agents` an `AgentsOptions`, `ValidateOnStart`:

| Schlüssel | Bedeutung |
|---|---|
| `Llm:BaseUrl` | NanoGPT-/OpenAI-kompatible Basis-URL |
| `Llm:ApiKey` | Geheimnis (nicht committen; User-Secrets/Env in Dev) |
| `Llm:Timeout` | HTTP-Timeout je Completion |
| `MaxConcurrentRuns` | Parallelität des Workers (Minimum 1) |
| `Pricing:PromptTokenPerMillion` / `Pricing:CompletionTokenPerMillion` | Grundlage für `CostEstimate` |

Das Modell eines Runs kommt aus dem Snapshot. Ein optionaler Config-Default ist nur Fallback, wenn eine Definition kein Modell setzt — in der bestehenden API ist `Model` Pflicht, der Snapshot trägt es immer.

Umgebung `Testing` (Integrationstests): Fake-`ILlmClient` registrieren, kein echter HTTP-Aufruf. Fehlt in Development der Key absichtlich, soll der Start klar scheitern, sofern nicht ausdrücklich ein Fake gewählt wird — in CI/Testing ist der Fake der Normalfall.

## Persistenz

Kein Schema-Bruch: bestehende Tabellen und Spalten reichen. Token- und Kostfelder sowie `StartedAt`/`CompletedAt`/`Error` werden befüllt. `EnsureCreatedAsync` bleibt; keine EF-Migrationen in diesem Teilprojekt.

Domain: `RunTransitions` und Run-Mutatoren so erweitern, dass Start, Abschluss, Fehler, Abbruch aus `Running` und Usage-Updates aus dem Loop möglich sind, ohne die bisherigen Invarianten (Snapshot, Nachrichtenformat) zu brechen.

## Fehlerbehandlung

| Situation | Verhalten |
|---|---|
| LLM-Timeout / HTTP-Fehler / ungültige Antwort | `Failed` + `Error`; SSE `error`, `done` |
| Unbekanntes oder werfendes Werkzeug | Tool-Nachricht mit Fehlerinhalt; Loop kann fortgesetzt werden |
| `MaxTurns` erreicht ohne finalen Assistententext | `Failed` (z. B. Hinweis auf Turn-Limit) |
| Abbruch | `Cancelled`, nicht `Failed` |
| Bereichskonfiguration ungültig | Prozessstart bricht ab |

Alle HTTP-Fehler außerhalb des laufenden Runs bleiben ProblemDetails wie in Teilprojekt 1/2.

## Tests

- **Unit:** `RunLoop` mit Fake-LLM (mehrere Turns, `tool_calls`, Abbruch, Turn-Limit, Kostenrechnung); erweiterte Transitionstests; Stub-Registry.
- **Integration:** `WebApplicationFactory` mit Fake-LLM; Anlegen → innerhalb Timeout `Completed` (oder erwartetes `Failed`); Nachrichtenanzahl/Rollen; SSE empfängt mindestens `status` und `done`; Abbruch während `Running` → `Cancelled`.
- Kein Live-NanoGPT in der Standard-Testpipeline.

## Fertigstellungskriterien

1. `dotnet build` und `dotnet test` ohne Fehler und ohne Warnungen.
2. Anlegen eines Runs führt ohne weiteren API-Aufruf zur Ausführung (Fake-LLM in Tests).
3. Erlaubte Übergänge wie oben; Abbruch aus `Pending` und `Running` möglich.
4. Nachrichten wachsen um Assistenten- und ggf. Tool-Einträge; Token und `CostEstimate` sind nach Erfolg gesetzt.
5. `GET .../stream` liefert SSE-Ereignisse; Polling-GETs bleiben gültig.
6. `Areas:Agents` ist gebunden und startvalidiert; Stubs bedienen `AllowedTools`.
7. Architekturtests der Bereichsgrenzen bleiben grün.

## Bewusste Nicht-Ziele

- Kein separates Runtime-Projekt und keine Host-eigene Worker-Registrierung außerhalb von `AgentsArea.ConfigureServices`.
- Kein Inline-Ausführen im HTTP-Request.
- Kein Cassette-/Record-Replay gegen NanoGPT in CI.
- Unbegrenzte Parallelität entfällt zugunsten von `MaxConcurrentRuns`.
