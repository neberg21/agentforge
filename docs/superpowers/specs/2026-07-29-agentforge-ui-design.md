# AgentForge — UI

**Datum:** 2026-07-29
**Status:** Entwurf zur Umsetzung freigegeben
**Umfang:** Teilprojekt 6 von 6

## Ziel

Eine Oberfläche über der AgentForge-API, in der du Agenten anlegst und konfigurierst, ihnen Aufträge gibst und ihnen bei der Ausführung zusiehst, und in der du mit ihnen redest — einzeln und in Gruppen. Vorbild ist die kleine React-Anwendung im Repo `aae`, aus der der Seitenaufbau übernommen wird: Liste mit Suche, Detailseite, Chat-Ansicht. Optik und Code-Aufbau werden neu entschieden.

Der Unterschied zu `aae` liegt im Datenmodell: dort ist alles ein Chat-Thread, hier gibt es zwei Dinge. Ein **Run** ist ein konkreter Auftrag („erstelle mir eine D&D-Seite") mit Werkzeugaufrufen und Kostenbilanz. Ein **Gespräch** ist ein lockeres Reden mit einem oder mehreren Agenten, um ihre Fähigkeiten und Einschätzungen abzuklopfen. Beide zeigen einen Verlauf, aber sie sind nicht dasselbe, und die UI hält sie auseinander.

## Abgrenzung

**Enthalten:** Ein Durchstich über die gesamte API — Shell mit Bereichsnavigation, Agenten-Verwaltung, Run-Verwaltung samt Verfolgung per Stream, Einzel- und Gruppengespräche mit Adressierung über Erwähnungen, Fehlerbehandlung, Auslieferung durch den Host, Testaufbau.

**Nicht enthalten:** Anmeldung und Benutzerverwaltung (der Host kennt in der aktuellen Ausbaustufe genau einen Besitzer), Mandantenfähigkeit, Rollen, Mehrsprachigkeit, Ende-zu-Ende-Tests, eine eigene Mobilansicht, Datei-Browser für die von Agenten erzeugten Workspaces, Auswertungen und Diagramme über Kosten.

## Einordnung und Abhängigkeiten

Dieses Teilprojekt ist das letzte der sechs aus der Skelett-Spec und setzt Teilprojekte 1 bis 4 voraus: Host, Agents-Bereich, Agent-Runtime mit Tool-Calling, Container-Executor.

Darüber hinaus fordert es Backend-Fläche ein, die in **keiner** bestehenden Spec steht — Gespräche als eigene Entität und Ereignisströme. Das ist eine echte Ausweitung des Vorhabens, keine UI-Detailfrage, und wird deshalb als eigenes Teilprojekt **3b** geführt: nach der Runtime, vor dieser UI. Der vorliegende Entwurf beschreibt nur die Fläche, die er braucht; entworfen wird sie in 3b.

## Geforderte API-Fläche

### Gespräche (neu, Teilprojekt 3b)

Ein `Conversation` hat einen Titel und `1..N` Agenten als Teilnehmer. **Der Einzelchat ist die Gruppe mit einem Teilnehmer** — eine Entität, eine Ansicht, kein zweiter Weg. Diese Vereinheitlichung ist die wichtigste Entscheidung dieses Abschnitts; die Alternative, Einzel- und Gruppenchat getrennt zu modellieren, hätte jede Ansicht und jeden Endpunkt verdoppelt, um genau einen Unterschied auszudrücken: die Zahl der Teilnehmer.

```
GET    /api/agents/conversations                 Liste, seitenweise
POST   /api/agents/conversations                 {title?, participantAgentIds[]}
GET    /api/agents/conversations/{id}            inkl. Teilnehmer mit Name
PUT    /api/agents/conversations/{id}            Titel und Teilnehmer, mit ConcurrencyToken
DELETE /api/agents/conversations/{id}            archiviert, wie bei Agenten
GET    /api/agents/conversations/{id}/messages
POST   /api/agents/conversations/{id}/messages   {content, mentions[]} → 202 + streamId
GET    /api/agents/conversations/{id}/stream     SSE
```

`GET /conversations/{id}` und die Listenantwort liefern je Teilnehmer Id **und** Name. Ohne das holt die UI für jede angezeigte Nachricht einzeln den Agenten nach. Die Listenantwort trägt zusätzlich einen Auszug der letzten Nachricht und deren Zeitstempel, weil die Gesprächsliste beides zeigt und sie sonst je Zeile den Verlauf nachladen müsste.

Erwähnte Agenten müssen Teilnehmer des Gesprächs sein; eine Erwähnung eines Nichtteilnehmers wird mit 400 abgelehnt statt stillschweigend ignoriert.

Archivierung folgt der Regel der Agenten: `DELETE` setzt `ArchivedAt`, archivierte Gespräche fehlen in Listen, bleiben per Id abrufbar.

### Adressierung im Gruppenchat

Eine Nachricht trägt `mentions` als Liste von Agent-Ids. **Wer erwähnt wird, antwortet.** Ist die Liste leer, wird die Nachricht gespeichert und niemand läuft — sie steht als Notiz im Verlauf.

Das Auflösen von `@name` zu einer Id geschieht im Eingabefeld der UI, nicht im Server; über die Leitung gehen Ids. Damit hängt das Verhalten nicht an Namensgleichheit, und ein umbenannter Agent bricht keine alte Nachricht.

Verworfen wurden: *alle Teilnehmer antworten auf jede Nachricht* — bei vier Agenten vier Antworten und vier Rechnungen für eine Frage an einen; und *ein Moderator-Agent entscheidet, wer antwortet* — eine zusätzliche Modellrunde pro Nachricht, undurchsichtiges Verhalten, schwer testbar. Beide bleiben später nachrüstbar, weil sie nur die Belegung von `mentions` verändern, nicht das Modell.

### Ströme

`EventSource` im Browser kann ausschließlich GET. Deshalb laufen Ströme über zwei Schritte: `POST` legt die Nachricht an und antwortet mit `202` und einer `streamId`, dann öffnet die UI den `GET`-Strom. Für Runs genügt der Strom, weil der Run schon eine Id hat:

```
GET /api/agents/runs/{id}/stream
GET /api/agents/conversations/{id}/stream
```

Das ist unbequemer als ein POST, der direkt strömt, und wurde trotzdem gewählt: der Browser bringt Wiederverbindung samt `Last-Event-ID` mitgeliefert mit, und ein neu geladener Tab kann sich an eine laufende Antwort anhängen, die er nicht angestoßen hat. Die Alternative — `fetch` mit `ReadableStream` und selbstgeschriebenem SSE-Parser — kostet beides und spart einen Rundlauf.

Beide Ströme senden dieselben Ereignisarten, damit ein Verlaufs-Bauteil und ein Reducer für beide reichen:

| Ereignis | Inhalt |
|---|---|
| `token` | Textstück der entstehenden Nachricht |
| `message` | fertige Nachricht mit Id, Sequence, Rolle, Absender |
| `tool` | Werkzeugaufruf mit Argumenten und Ergebnis |
| `status` | Run-Status |
| `usage` | Token und Kostenschätzung |
| `done` | Ende der Antwort |
| `error` | Fehler samt Code |

Jedes Ereignis trägt die `Sequence` der Nachricht, zu der es gehört, die `streamId` der Antwort, zu der es zählt, und eine SSE-Ereignis-Id für `Last-Event-ID`.

Der Strom gehört dem Gespräch, nicht der einzelnen Antwort: die UI hält **eine** Verbindung je geöffnetem Gespräch offen und bekommt darüber jede Antwort, auch eine von einem anderen Tab angestoßene. Die `streamId` aus der `POST`-Antwort dient nur dazu, die eigene gerade gesendete Nachricht den einlaufenden Ereignissen zuzuordnen; sie steht nicht in der Strom-Adresse.

### Änderungen an bestehenden Endpunkten

**`GET /api/agents/definitions` braucht `q`** (Name enthält, ohne Beachtung der Groß- und Kleinschreibung). Die Liste hat ein Suchfeld, und clientseitiges Filtern einer Seite von höchstens 200 Einträgen behauptet Vollständigkeit, die es nicht hat.

**Jedes ProblemDetails trägt ein stabiles `type` als Code.** Die UI muss bei 409 vier Fälle auseinanderhalten — Namenskollision, Nebenläufigkeit, unzulässiger Zustandsübergang, Run auf archivierten Agenten — und jeder braucht eine andere Meldung und einen anderen nächsten Schritt. Ein Vergleich auf Meldungstexte wäre ein stiller Bruch beim ersten Umformulieren.

Die Modellauswahl im Agenten-Formular ist in v1 ein **freies Textfeld**. Eine Modell-Liste vom Server wäre eine eigene Entscheidung mit eigener Pflege und ist es an dieser Stelle nicht wert.

## Technik und Auslieferung

React 19, TypeScript, Vite, Tailwind 4, react-router 7, Vitest mit Testing Library — derselbe Stack wie `aae`. Er ist bekannt, das Ökosystem groß, und Chat mit Strömen ist darin Routine. Der Preis ist eine zweite Toolchain im .NET-Monorepo.

Blazor wurde erwogen und verworfen: eine Toolchain und geteilte C#-DTOs wären ein echter Vorteil gewesen, aber der Stack, der hier tatsächlich beherrscht wird, wiegt für ein Teilprojekt dieser Größe schwerer.

### Ort im Repo

```
src/AgentForge.Web/
  package.json  vite.config.ts  tsconfig*.json  index.html
  src/
    main.tsx  App.tsx  index.css
    shell/
      AppShell.tsx        drei Spalten, Haltepunkte
      AreaNav.tsx         Bereiche, aus /api/areas und Registry
      ContextPanel.tsx    rechte Spalte als Steckplatz
    lib/
      http.ts             fetch-Hülle, ProblemDetails → Fehlerobjekt
      sse.ts              EventSource-Anbindung
      areas.ts            /api/areas laden
    areas/
      index.ts            Registry, explizit
      agents/
        routes.tsx  api.ts  types.ts
        AgentListPage.tsx  AgentFormPage.tsx  AgentDetailPage.tsx
        RunListPage.tsx  RunDetailPage.tsx  StartRunDialog.tsx
        ConversationListPage.tsx  ConversationPage.tsx  NewConversationDialog.tsx
        Transcript.tsx  TranscriptLog.tsx  ToolCallCard.tsx  MessageComposer.tsx
        useRunStream.ts  useConversationStream.ts  transcriptReducer.ts
    test/
      setup.ts  fakeEventSource.ts
  src/__tests__/
```

Kein .NET-Projekt und nicht in der Solution — ein Node-Verzeichnis unter `src/`, damit die Regel aus der Skelett-Spec hält, dass auf oberster Repo-Ebene nur `src`, `tests`, `docs` und die Dateien liegen, die dort liegen müssen. Die Frontend-Tests leben bei ihrem Code unter `src/AgentForge.Web/src/__tests__/`, nicht im `tests/`-Baum, der den .NET-Testprojekten gehört.

Jede Seitendatei bleibt unter etwa 200 Zeilen. Datenholen liegt in `api.ts` und in Hooks, nie in einer Seite.

### Registry als Spiegel der Area-Konvention

Jeder Bereich exportiert `{ slug, title, routes, nav }`, und `areas/index.ts` listet die Bereiche namentlich auf — kein Glob, kein automatisches Einsammeln. Das ist bewusst dieselbe Regel wie `builder.AddArea<AgentsArea>()` im Host: alle Bereiche in einer Datei sichtbar, keine Überraschung durch Reflexion, ein neuer Bereich kostet eine Zeile.

Die Navigation zeigt die Schnittmenge aus `/api/areas` und der Registry. Was der Server nicht meldet, erscheint nicht — so verschwindet ein abgeschalteter Bereich von selbst. Was der Server meldet, das Frontend aber nicht kennt, wird ignoriert; es gibt nichts, wohin man routen könnte.

### Entwicklung und Produktion

Beim Entwickeln läuft der Vite-Server und leitet `/api` an den Host weiter, Ströme eingeschlossen und ungepuffert.

Für Produktion baut `npm run build` nach `dist`; ein MSBuild-Ziel am Host kopiert das Ergebnis beim `Publish` nach `wwwroot`. Der Host liefert die Dateien statisch aus und fängt unbekannte Pfade auf `index.html` ab, damit ein Neuladen auf einer tiefen Route nicht im 404 endet. Ein Deployment, eine Herkunft, kein CORS.

`dotnet build` ruft **kein** npm. Sonst zahlt jeder Backend-Build den Frontend-Build mit.

### Stil

Tailwind-Klassen stehen direkt an den Elementen, ohne Bauteil-Schicht dazwischen — wie in `aae`. Dazu `index.css` mit eigenen CSS-Variablen für Farben und Abstände und Dark Mode über `prefers-color-scheme`; die Werte werden nicht aus `aae` übernommen, der Akzentfarbwert fällt beim Umsetzen.

Der bekannte Nachteil dieser Wahl ist Stil-Drift und Suchen-und-Ersetzen bei Änderungen am Aussehen. Eine Regel dämpft ihn: taucht dieselbe Klassenkette zum dritten Mal auf, wandert sie in ein Bauteil. Kein Vorrat an Bauteilen im Voraus, aber auch keine fünfzig Fundstellen für eine Stiländerung.

## Shell und Navigation

Drei Spalten:

- **Links:** Bereichswechsel oben, darunter die Navigation des aktiven Bereichs — Agenten, Runs, Gespräche — und die fünf zuletzt berührten Objekte.
- **Mitte:** die Ansicht.
- **Rechts:** Kontext zur Ansicht.

Die rechte Spalte ist ein Steckplatz, den jede Seite füllt: Teilnehmer und Modelle im Gespräch, Status, Kennzahlen und eingefrorener Snapshot beim Run, Runs und Gespräche dieses Agenten in der Agenten-Ansicht. Die Shell weiß nicht, was darin steht.

Bei schmalem Fenster verschwindet zuerst die Kontextspalte hinter einem Knopf, dann wird die linke Spalte zur Schublade. Zwei Haltepunkte, keine eigene Mobilansicht.

Die „zuletzt berührten Objekte" liegen im `localStorage`, nicht auf dem Server: fünf Verweise auf Ids samt Art und Beschriftung. Ein Eintrag, dessen Objekt nicht mehr existiert, wird beim Klick als fehlend behandelt und aus der Liste entfernt.

### Routen

Unter dem Bereichs-Slug, parallel zum Backend:

```
/agents/definitions            Liste
/agents/definitions/new        Formular
/agents/definitions/:id        Detail
/agents/definitions/:id/edit   Formular
/agents/runs                   Liste
/agents/runs/:id               Verlauf
/agents/conversations          Liste
/agents/conversations/:id      Gespräch
```

`/` leitet auf den ersten Bereich der Registry, `/agents` auf `/agents/definitions`.

## Die Ansichten

### Agenten-Liste

Tabelle mit Name, Modell, Beschreibung, Änderungszeit. Suchfeld auf `q`, um 300 ms entprellt. Seitenweise über `skip` und `take` mit 50. Zeilenaktionen: Gespräch beginnen, Run starten, Bearbeiten, Archivieren — Archivieren mit Rückfrage, danach verschwindet die Zeile.

### Agenten-Formular

Anlegen und Bearbeiten teilen eine Datei. Eine Spalte, vier Abschnitte untereinander: **Identität** (Name, Beschreibung), **System-Prompt**, **Modell & Grenzen** (Modell, Temperature, max. Output-Token, max. Turns), **Werkzeuge**.

Diese Anordnung wurde einem zweispaltigen Aufbau mit großem Prompt-Editor vorgezogen: in der Mitte einer Drei-Spalten-Shell bleiben für die Prompt-Spalte zu wenige Pixel, damit sich der Vorteil einstellt, und unter etwa 1100 Pixel Fensterbreite müsste sie ohnehin auf eine Spalte zurückfallen. Wird der Prompt später zu eng, ist ein Knopf „Prompt groß bearbeiten" mit Vollbild-Editor die billigere Antwort. Ein Assistent in Schritten wurde ebenfalls verworfen: beim Bearbeiten steht er im Weg, und er bräuchte eine zweite Ansicht für denselben Datensatz.

Die Grenzen werden clientseitig gegen exakt dieselben Werte geprüft wie serverseitig: Temperature 0–2, Output-Token 1–200 000, Turns 1–200, Name bis 100 Zeichen, System-Prompt nicht leer. Werkzeuge sind Freitext-Plaketten, weil `AllowedTools` bis Teilprojekt 4 keine Bedeutung hat.

Das `ConcurrencyToken` läuft unsichtbar mit. Bei 409 mit dem Nebenläufigkeits-Code erscheint „wurde anderswo geändert" samt Knopf zum Neuladen, und die Eingaben bleiben im Formular stehen — nichts ist ärgerlicher, als einen langen Prompt zweimal zu schreiben.

### Agenten-Detail

Kopf mit Name und Modell, der System-Prompt lesbar gesetzt, Knöpfe für Run starten, Gespräch beginnen, Bearbeiten, Archivieren. Kontextspalte: die letzten Runs und Gespräche dieses Agenten.

### Run starten

Kein eigener Bildschirm, sondern ein Dialog: Objective als mehrzeiliges Feld, Agent vorbelegt, wenn der Dialog aus der Agenten-Ansicht kommt, sonst wählbar. Nach dem Absenden geht es direkt auf das Run-Detail. Ein archivierter Agent liefert 409 und eine Meldung, die genau das erklärt.

### Run-Liste

Objective gekürzt, Agent, Status als Plakette, Anlagezeit, Dauer. Filter auf Agent und Status, seitenweise, neueste zuerst.

### Run-Detail

Der Verlauf liest sich als Gespräch: deine Bubble mit dem Objective, Antwort-Bubbles des Agenten, dazwischen schmale Werkzeugkarten mit Name und Kurzfassung — zugeklappt. Aufgeklappt zeigen sie Argumente und Ergebnis in Festbreite; sehr langer Ausgabetext wird gekürzt und über „mehr anzeigen" vollständig gezeigt.

Diese Form wurde gewählt, weil sie ihr Bauteil mit dem Gespräch teilt — halb so viel Code, ein Leseeindruck. Ein Umschalter **Protokoll** zeigt dieselben Daten als Zeilenliste mit `Sequence`, Rolle und Inhalt, einschließlich des System-Prompts an Position 0. Er kostet fast nichts, weil er dieselben Daten ohne Ausschmückung zeigt, und ist die Ansicht, die man bei einem fehlgeschlagenen Run tatsächlich will.

Verworfen wurde, Text und Werkzeugaufrufe in getrennte Spalten zu legen: der Gedankengang bliebe kürzer, aber die zeitliche Ordnung zerfällt, und genau die braucht man bei der Fehlersuche.

Die Kontextspalte zeigt Status, Agent, Token und Kostenschätzung aus dem Run selbst sowie den Turn-Stand: gezählt als Zahl der Assistant-Nachrichten im Verlauf, gemessen gegen `MaxTurns` aus dem eingefrorenen Snapshot. Ein eigenes Feld dafür gibt es nicht und braucht es nicht.

Abbrechen erscheint nur bei `Pending` und `Running` und schickt das `ConcurrencyToken` mit. Der Verlauf klebt am unteren Rand, solange du dort stehst; scrollst du hoch, hält er still und bietet einen Knopf zurück nach unten.

### Gesprächs-Liste

Titel, Teilnehmer mit Namen, letzte Nachricht, Zeit. „Neues Gespräch" öffnet einen Dialog mit optionalem Titel und Mehrfachauswahl der Teilnehmer aus den Agenten; bleibt der Titel leer, wird einer aus den Teilnehmernamen gebildet. Je Zeile lässt sich ein Gespräch archivieren, mit Rückfrage und derselben Wirkung wie bei Agenten.

### Gespräch

Dasselbe Verlaufs-Bauteil wie im Run-Detail. Jede Agentennachricht trägt den Absendernamen und eine aus der Agent-Id abgeleitete, stabile Farbe — im Gruppenchat ist das der Unterschied zwischen Lesen und Rätseln.

Im Eingabefeld öffnet `@` die Auswahl der Teilnehmer; gewählte Erwähnungen stehen als Plaketten über dem Feld und gehen als Ids mit. Sendest du ohne Erwähnung, wird die Nachricht gespeichert und ein Hinweis zeigt, dass niemand adressiert war.

Die Kontextspalte verwaltet die Teilnehmer — hinzufügen und entfernen mitten im Gespräch, per `PUT` mit `ConcurrencyToken` — und verlinkt je Teilnehmer auf den Agenten.

## Datenfluss

### Zustand ohne Datenschicht

Eine `fetch`-Hülle in `lib/http.ts`, pro Ansicht ein Hook, der lädt und seinen Ladezustand selbst trägt — wie in `aae`, ohne Abfrage-Bibliothek. Bei sieben Ansichten und drei Dialogen ist das tragbar.

Der Preis ist ehrlich zu benennen: nach jeder Änderung muss die betroffene Liste ausdrücklich neu geladen werden, und das kann man vergessen. Eine Abfrage-Bibliothek würde genau das erledigen, hilft aber beim interessanten Teil — den Strömen — nicht. Sollte sich das Vergessen als wiederkehrender Fehler zeigen, ist der Einbau später ein lokaler Eingriff in die Hooks.

### Ströme

`lib/sse.ts` öffnet einen `EventSource` und verteilt Ereignisse; darauf sitzen `useRunStream` und `useConversationStream`. Beide führen dieselben Ereignisse in denselben Zustand: Nachrichten, eine im Aufbau befindliche Nachricht mit angesammelten Token, Status, Kennzahlen, Fehler.

Beim Öffnen einer Ansicht wird **erst** der Verlauf per `GET .../messages` geholt, **dann** der Strom geöffnet. Nachrichten werden nach `Sequence` in eine Map geschrieben, nicht angehängt. Damit sind zwei Dinge erledigt, die sonst je einen Sonderfall bräuchten: eine Nachricht, die zwischen beiden Schritten entsteht, und Wiederholungen nach einer Wiederverbindung mit `Last-Event-ID`.

Die Ereignisverarbeitung liegt als reine Funktion in `transcriptReducer.ts` und wird ohne DOM geprüft.

Deine eigene Nachricht erscheint sofort mit dem Zustand „sendet" und wird vom `message`-Ereignis ersetzt. Scheitert das Senden, bleibt sie mit einem Wiederholen-Knopf stehen, statt zu verschwinden.

Reißt die Verbindung, verbindet der Browser selbst neu, und ein dezenter Hinweis sagt es. Nach drei erfolglosen Versuchen bleibt ein Knopf zum Neuladen.

Listen strömen nicht. Sie laden beim Betreten und nach Änderungen.

## Fehlerbehandlung

`http.ts` übersetzt ProblemDetails nach RFC 9457 in ein Fehlerobjekt mit Status, `type`-Code, Titel, Detail und Feldfehlern.

| Fall | Verhalten der UI |
|---|---|
| 400 Validierung | Feldfehler zurück an das Formularfeld, Eingaben bleiben |
| 404 | „nicht gefunden"-Ansicht mit Weg zurück in die Liste |
| 409 Namenskollision | Fehler am Namensfeld, Vorschlag zum Umbenennen |
| 409 Nebenläufigkeit | „wurde anderswo geändert", Knopf zum Neuladen |
| 409 Zustandsübergang | Hinweis, dass der Run bereits beendet ist, Ansicht wird aktualisiert |
| 409 archivierter Agent | Hinweis mit Verweis auf den archivierten Agenten |
| 500 | allgemeine Meldung, Korrelations-Id wenn mitgeliefert |
| Netz weg | Hinweis am Ort der Aktion, Wiederholen-Knopf |

Die Unterscheidung der vier 409-Fälle hängt am `type`-Code, nie am Meldungstext.

Leere Zustände sind überall ausformuliert und tragen den nächsten Schritt als Knopf — „noch keine Agenten · Agent anlegen", nicht „keine Daten".

## Tests

Vitest mit Testing Library, ein Test je Ansicht, wie in `aae`. `fetch` wird gestubbt; kein Netz, kein MSW.

Für Ströme liegt ein falscher `EventSource` in `src/test/fakeEventSource.ts`, der Ereignisfolgen einspeist. Damit sind diese Fälle deterministisch prüfbar:

- Token-Folge baut eine Nachricht auf und wird durch `message` abgeschlossen.
- Ein `tool`-Ereignis erscheint als zugeklappte Karte und öffnet sich auf Klick.
- Eine Wiederverbindung, die Ereignisse wiederholt, führt zu keiner doppelten Nachricht.
- Ein `error`-Ereignis beendet den Ladezustand und zeigt eine Meldung.

`transcriptReducer` wird zusätzlich direkt geprüft: Sequenz-Lücken, Wiederholungen, Ereignisse in falscher Reihenfolge.

Zugänglichkeit: der Verlauf ist `role="log"` mit `aria-live="polite"`, Formularfelder haben Labels, Tabellen haben Kopfzellen, Dialoge fangen den Fokus. Keine Ende-zu-Ende-Tests in v1.

## Fertigstellungskriterien

1. `npm run build`, `npm run lint` und `npm test` laufen ohne Fehler und ohne Warnungen durch.
2. Ein Agent lässt sich anlegen, bearbeiten und archivieren; der archivierte fehlt in der Liste und ist per Id weiter erreichbar.
3. Ein Run lässt sich starten; sein Verlauf erscheint per Strom mit Werkzeugkarten, der Protokoll-Umschalter zeigt dieselben Daten, Abbrechen wirkt und ein zweiter Abbruch zeigt die 409-Meldung.
4. Ein Gruppengespräch mit drei Agenten lässt sich anlegen; `@name` adressiert genau einen Agenten, dessen Antwort strömt ein, und Absender sind an Name und Farbe unterscheidbar.
5. Eine Nachricht ohne Erwähnung wird gespeichert und als nicht adressiert gekennzeichnet, ohne einen Agenten zu starten.
6. Jeder Fall aus der Fehlertabelle erzeugt eine eigene, verständliche Meldung und ist durch einen Test belegt.
7. Eine Wiederverbindung mitten in einer Antwort führt zu keiner doppelten und keiner fehlenden Nachricht.
8. Der Host liefert das gebaute Frontend aus; ein Neuladen auf `/agents/runs/:id` landet nicht im 404.
9. Die Bereichsnavigation zeigt genau die Bereiche, die `/api/areas` meldet und die Registry kennt.

## Annahmen

- Teilprojekte 1 bis 4 sind umgesetzt, und Teilprojekt 3b hat die oben geforderte Fläche geliefert. Ohne 3b ist dieses Teilprojekt nicht baubar.
- Der Host kennt genau einen Besitzer (`LocalSingleUser`). Es gibt keine Anmeldemaske, und die UI zeigt keinen Benutzer an. Kommt echte Anmeldung, ist das ein eigener Entwurf.
- Node und npm liegen auf dem Entwicklungsrechner vor; die Entwicklung findet unter Windows statt.
- Ströme werden vom Host ungepuffert ausgeliefert, und der Vite-Proxy gibt sie ungepuffert weiter.
- Die Zahl der Agenten und Gespräche bleibt in der Größenordnung Dutzende. Virtuelles Scrollen und Suchindizes sind deshalb nicht vorgesehen.
