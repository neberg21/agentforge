# AgentForge — Monorepo-Skelett und Agents-Bereich

**Datum:** 2026-07-29
**Status:** Entwurf zur Umsetzung freigegeben
**Umfang:** Teilprojekt 1 und 2 von 6

## Ziel

Ein .NET-Monorepo, in dem fachlich getrennte Bereiche als Module eines einzigen Hosts leben, und darin als erster Bereich die Verwaltung von KI-Agenten. Am Ende dieses Teilprojekts läuft die API, Agenten lassen sich anlegen und konfigurieren, und Ausführungen (Runs) werden als Datensätze geführt — ohne Sprachmodell, ohne Container.

Die spätere Ausbaustufe ist ein System, in dem diese Agenten selbst Anwendungen bauen. Das Skelett wird daraufhin entworfen, aber nicht darauf vorweggenommen.

## Abgrenzung

**Enthalten:** Repo- und Solution-Struktur, Area-Konvention samt erzwungener Grenzen, Host mit Persistenz und Auth-Seam, Agents-Bereich mit Agent-Definitionen und Run-Datensätzen, REST-Oberfläche, Fehlerbehandlung, Testaufbau.

**Nicht enthalten:** Aufrufe gegen NanoGPT, Tool-Calling, Container-Ausführung, Datei- und Shell-Werkzeuge, Workspace-Verwaltung, UI, echte Authentifizierung, Mandantenfähigkeit, Datenbankmigrationen.

## Gesamtbild

Das Vorhaben zerfällt in sechs Teilprojekte mit je eigener Spec:

| # | Teilprojekt | Ergebnis |
|---|---|---|
| 1 | Monorepo-Skelett und Area-Konvention | Host startet, Bereiche registrieren sich, Persistenz, Auth-Seam, Tests laufen |
| 2 | Agents-Bereich: Verwaltung | Agent-Definitionen, Runs, Nachrichten als REST-API |
| 3 | Agent-Runtime | Tool-Calling-Loop gegen NanoGPT, Streaming, Turn-Limits, Kostenerfassung |
| 4 | Container-Executor und Werkzeuge | Workspace-Lifecycle, Datei- und Shell-Werkzeuge im Container |
| 5 | Zweiter Bereich (D&D) | Belastungsprobe für die Area-Konvention |
| 6 | UI | Oberfläche über der API |

Dieses Dokument beschreibt 1 und 2 gemeinsam, weil ein Skelett ohne echten Bereich seine eigene Konvention nicht beweisen kann.

## Grundentscheidung: zwei Sorten „Bereich"

Der Begriff trägt im Vorhaben zwei verschiedene Bedeutungen, die getrennt gehalten werden:

**Systembereiche** sind fachliche Module des eigenen Systems — Agents, später D&D. Sie teilen sich Host, Datenbank, Auth und Deployment. Sie leben unter `src/Areas/` als modularer Monolith.

**Von Agenten gebaute Anwendungen** sind Ergebnisartefakte mit unbekannter Technik, erzeugt von einem Sprachmodell. Sie leben unter `workspaces/`, außerhalb des Hostprozesses, mit eigenem Build. Ein Fehler dort darf das Agent-System nicht mitreißen. Das Verzeichnis entsteht erst in Teilprojekt 4; in diesem Teilprojekt wird lediglich der `.gitignore`-Eintrag vorbereitet.

## Repo-Layout

```
agentforge/
  AgentForge.sln
  global.json                       SDK-Pin auf 10.0.1xx
  Directory.Build.props             gemeinsame Compiler-Einstellungen
  Directory.Packages.props          zentrale Paketversionen
  .gitignore                        ignoriert .data/ und workspaces/
  src/
    AgentForge.Host/                ASP.NET Core, Composition Root
    AgentForge.Core/                Result-Typen, IClock, ICurrentUser, Id-Erzeugung
    AgentForge.Areas.Abstractions/  IArea und Registrierungs-Contracts
    Areas/
      AgentForge.Areas.Agents/
  tests/
    AgentForge.Core.Unit/
    AgentForge.Areas.Agents.Unit/
    AgentForge.Host.Integration/
    AgentForge.Host.Architecture/
  docs/superpowers/
```

Auf Repo-Ebene liegen ausschließlich `src`, `tests`, `docs` sowie die Dateien, die dort liegen müssen: Solution, `global.json`, die beiden `Directory.*.props` und `.gitignore`.

Testprojekte heißen `<Projekt>.<Testart>`. Die Architekturtests hängen am Host, weil nur er alle Assemblies referenziert und der Referenzgraph sonst nicht vollständig sichtbar ist.

Das in der Bereichs-Unterscheidung genannte `workspaces/` wird hier **nicht** angelegt. Es wäre leer und git-ignoriert; es entsteht in Teilprojekt 4, wo Agenten tatsächlich hineinschreiben. Der `.gitignore`-Eintrag wird trotzdem schon gesetzt, damit später nichts versehentlich eingecheckt wird.

Zielframework ist .NET 10, da das SDK auf dem Entwicklungsrechner vorliegt. Nullable-Referenztypen und `TreatWarningsAsErrors` sind in `Directory.Build.props` für alle Projekte aktiviert.

## Area-Konvention

Ein Bereich ist eine Klassenbibliothek mit genau einer öffentlichen Implementierung von `IArea`:

```csharp
public interface IArea
{
    string Slug { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder routes);
    Task MigrateAsync(IServiceProvider sp, CancellationToken ct);
}
```

`Slug` bestimmt das Routen-Präfix: der Agents-Bereich mountet unter `/api/agents`. Der Slug ist kleingeschrieben, alphanumerisch mit Bindestrichen; der Host prüft das beim Start und bricht bei Verstoß ab.

Drei Regeln machen den Bereich zum Bereich:

**Registrierung ist explizit.** Der Host ruft `builder.AddArea<AgentsArea>()`. Kein Assembly-Scanning. Alle Bereiche sind in einer Datei sichtbar, es gibt keine Reflection-Überraschungen, und ein neuer Bereich kostet eine Zeile.

**Bereiche kennen einander nicht.** Ein Bereichsprojekt referenziert ausschließlich `AgentForge.Core` und `AgentForge.Areas.Abstractions`. Braucht ein Bereich einen anderen, geschieht das über ein `AgentForge.Areas.<Name>.Contracts`-Projekt — ausschließlich Interfaces und DTOs, keine Implementierung. Der implementierende Bereich registriert die Umsetzung in seinem `ConfigureServices`. Dieser Schnitt erlaubt es, einen Bereich später als eigenen Dienst herauszulösen, ohne Aufrufer anzufassen.

Solange es nur einen Bereich gibt, hat niemand etwas zu veröffentlichen. Das erste Contracts-Projekt entsteht deshalb mit dem zweiten Bereich; hier wird nur die Regel festgelegt und im Architekturtest verankert. Ein leeres Projekt anzulegen, das nichts enthält und niemand referenziert, wäre Ballast.

**Die Grenze wird erzwungen.** `AgentForge.Architecture.Tests` prüft die Referenzgraphen der geladenen Assemblies und schlägt fehl, wenn ein Bereich einen anderen Bereich außerhalb von dessen Contracts referenziert oder wenn ein Bereich den Host referenziert. Ohne diesen Test ist die Konvention eine Bitte, kein Vertrag, und sie erodiert beim ersten „nur dieses eine Mal".

## Datenmodell des Agents-Bereichs

Alle Ids sind `Guid.CreateVersion7()` — zeitsortiert und damit indexfreundlich. Alle Zeitstempel sind `DateTimeOffset` in UTC, bezogen über `IClock` aus `Core`, damit Tests die Zeit kontrollieren können.

### Agent

Die wiederverwendbare Definition.

| Feld | Typ | Anmerkung |
|---|---|---|
| `Id` | Guid | Primärschlüssel |
| `OwnerId` | string(100) | indiziert, aus `ICurrentUser` |
| `Name` | string(100) | eindeutig je `(OwnerId, Name)` unter nicht archivierten |
| `Description` | string(1000)? | |
| `SystemPrompt` | string | Pflicht |
| `Model` | string(100) | Pflicht, Modellbezeichner |
| `Temperature` | double | Vorgabe 0.7, gültig 0–2 |
| `MaxOutputTokens` | int | Vorgabe 4096, gültig 1–200000 |
| `MaxTurns` | int | Vorgabe 20, gültig 1–200 |
| `AllowedTools` | string[] | JSON-Textspalte, Vorgabe leer |
| `CreatedAt` / `UpdatedAt` | DateTimeOffset | |
| `ArchivedAt` | DateTimeOffset? | gesetzt heißt gelöscht |
| `ConcurrencyToken` | Guid | bei jedem Speichern neu gesetzt |

`AllowedTools` bleibt in diesem Teilprojekt eine reine Zeichenkettenliste ohne Bedeutung. Sie wird gespeichert und ausgeliefert, aber von nichts ausgewertet; Teilprojekt 4 gibt ihr Wirkung.

### Run

Eine Ausführung eines Agenten.

| Feld | Typ | Anmerkung |
|---|---|---|
| `Id` | Guid | Primärschlüssel |
| `OwnerId` | string(100) | indiziert |
| `AgentId` | Guid | Fremdschlüssel, kein Kaskadenlöschen |
| `AgentSnapshot` | JSON | eingefrorene Konfiguration zum Startzeitpunkt |
| `Objective` | string | der Auftrag als Text, Pflicht |
| `Status` | string | `Pending`, `Running`, `Completed`, `Failed`, `Cancelled` |
| `CreatedAt` | DateTimeOffset | |
| `StartedAt` | DateTimeOffset? | in v1 stets leer |
| `CompletedAt` | DateTimeOffset? | in v1 nur beim Abbruch gesetzt |
| `Error` | string? | |
| `PromptTokens` / `CompletionTokens` | int? | in v1 stets leer |
| `CostEstimate` | decimal? | in v1 stets leer |
| `ConcurrencyToken` | Guid | |

`AgentSnapshot` enthält `Name`, `SystemPrompt`, `Model`, `Temperature`, `MaxOutputTokens`, `MaxTurns` und `AllowedTools` als JSON-Textspalte. Statt Agent-Versionierung friert jeder Run seine Konfiguration ein. Änderst du später den System-Prompt, bleibt der alte Run erklärbar. Das ist erheblich weniger Maschinerie als eine Versionstabelle und löst dasselbe Problem.

### RunMessage

Der Gesprächsverlauf, geschnitten nach dem OpenAI-Nachrichtenformat, damit Teilprojekt 3 die Tabelle nur noch füllen muss statt sie umzubauen.

| Feld | Typ | Anmerkung |
|---|---|---|
| `Id` | Guid | Primärschlüssel |
| `RunId` | Guid | Fremdschlüssel, Kaskadenlöschen |
| `Sequence` | int | eindeutig je `(RunId, Sequence)`, lückenlos ab 0 |
| `Role` | string | `System`, `User`, `Assistant`, `Tool` |
| `Content` | string? | bei reinen Werkzeugaufrufen leer |
| `ToolCallsJson` | string? | |
| `ToolCallId` | string(100)? | nur bei `Role = Tool` |
| `CreatedAt` | DateTimeOffset | |

Beim Anlegen eines Runs schreibt der Bereich zwei Nachrichten: Sequence 0 mit `Role = System` und dem System-Prompt aus dem Snapshot, Sequence 1 mit `Role = User` und dem `Objective`. Damit liefert der Nachrichten-Endpunkt schon in v1 echte Daten, und das Format ist erprobt, bevor das Sprachmodell dazukommt.

### Zustandsübergänge

In diesem Teilprojekt gibt es genau einen erlaubten Übergang: `Pending` → `Cancelled`. Jeder andere Übergang wird abgelehnt. Die Zustandsmaschine liegt als eigene, direkt testbare Einheit vor; der Motor, der sie über `Running` nach `Completed` oder `Failed` treibt, kommt in Teilprojekt 3.

### Löschen

`DELETE` auf eine Agent-Definition setzt `ArchivedAt` statt zu löschen. Archivierte Agenten erscheinen nicht in Listen, bleiben aber über ihre Id abrufbar, und ihre Runs bleiben vollständig erhalten. Ein neuer Run auf einen archivierten Agenten wird mit 409 abgelehnt. Die Alternative — echtes Löschen mit Blockade bei vorhandenen Runs — hätte bedeutet, dass ein einmal benutzter Agent nie mehr verschwinden kann. Der Preis der gewählten Lösung ist eine nullbare Spalte und ein Query-Filter.

## Persistenz, Konfiguration, Auth

### Datenbank

SQLite lokal, PostgreSQL (Neon) später, umgeschaltet über `Database:Provider` mit den Werten `sqlite` und `postgres`. Tests laufen gegen SQLite im Modus `:memory:`, frisch je Test. Die lokale Entwicklung nutzt eine Datei unter `./.data/agentforge.db`, damit der Inhalt einsehbar bleibt.

Der EF-Core-`InMemory`-Provider wird bewusst nicht verwendet. Er erzwingt weder Unique-Constraints noch Fremdschlüssel, kennt keine Transaktionen und übersetzt kein SQL; Entwicklung gegen ihn verschiebt sämtliche Constraint-Verletzungen auf den Tag des Providerwechsels. SQLite ist genauso flüchtig und schnell, aber relational echt.

Der Preis der Portabilität: keine PostgreSQL-Spezialitäten im Modell. JSON-Felder liegen als Textspalten statt als `jsonb`, und das Concurrency-Token ist ein von der Anwendung gesetzter `Guid` statt `xmin`. Beides ist beim Wechsel auf Neon ohne Datenverlust nachschärfbar.

Jeder Bereich bringt seinen eigenen `DbContext` mit, dessen Tabellen mit dem Slug präfigiert sind (`agents_agent`, `agents_run`, `agents_run_message`). Bereichsübergreifende Transaktionen gibt es dadurch nicht — das ist beabsichtigt und stützt die Grenze.

### Migrationen

In diesem Teilprojekt bewusst keine. Solange das Modell noch wackelt, kosten zwei Migrationssätze — einer je Provider — mehr, als sie einbringen. `IArea.MigrateAsync` ruft vorerst `EnsureCreatedAsync()`. Die erste echte Migration entsteht in dem Teilprojekt, das auf Neon umstellt, aus dem dann stabilen Modell. Der Seam bleibt bestehen, nur die Implementierung ist trivial.

### Auth

`ICurrentUser` in `Core` liefert eine `OwnerId`. Die einzige Implementierung in diesem Teilprojekt ist `LocalSingleUser`, die eine feste, über `Auth:LocalOwnerId` konfigurierbare Id zurückgibt. Alle Bereichs-Endpunkte hängen an einer benannten Autorisierungs-Policy, die hier immer zustimmt.

Jede Entität trägt `OwnerId`, und jeder `DbContext` setzt einen globalen Query-Filter darauf. Der spätere Wechsel auf echte Anmeldung ist damit ein Austausch zweier Registrierungen im Host statt einer Änderung an jeder Abfrage.

Mandantenfähigkeit, Rollen und ein Berechtigungsmodell sind ausdrücklich nicht Teil dieses Entwurfs.

### Konfiguration

Jeder Bereich bindet einen eigenen Abschnitt (`Areas:Agents:*`) auf eine typisierte Options-Klasse mit `ValidateOnStart`. Fehlkonfiguration bricht den Start ab, statt beim ersten Aufruf zu überraschen.

## API

Minimal APIs. Der Bereich mountet unter seinem Slug, Ressourcen liegen darunter; das vermeidet `/api/agents/agents`.

```
GET    /api/agents/definitions            Liste, seitenweise
POST   /api/agents/definitions            anlegen
GET    /api/agents/definitions/{id}
PUT    /api/agents/definitions/{id}
DELETE /api/agents/definitions/{id}       archiviert
GET    /api/agents/runs?agentId=&status=  Liste, seitenweise
POST   /api/agents/runs                   {agentId, objective}
GET    /api/agents/runs/{id}
POST   /api/agents/runs/{id}/cancel
GET    /api/agents/runs/{id}/messages
```

Vom Host selbst:

```
GET    /_health                           Liveness
GET    /_health/ready                     Readiness, prüft die Datenbank
GET    /api/areas                         registrierte Bereiche mit Slug
```

`/api/areas` kostet fast nichts und macht die Area-Konvention von außen sichtbar; die spätere UI baut ihre Navigation daraus.

Listen sind seitenweise über `skip` und `take` mit Vorgabe 50 und Obergrenze 200; die Antwort enthält die Gesamtzahl. Das OpenAPI-Dokument entsteht über die in .NET 10 eingebaute Unterstützung, Scalar dient als Oberfläche und wird ausschließlich in der Development-Umgebung eingehängt.

`PUT /api/agents/definitions/{id}` und `POST /api/agents/runs/{id}/cancel` verlangen das aktuelle `ConcurrencyToken` im Rumpf. Weicht es vom gespeicherten Wert ab, antwortet der Endpunkt mit 409, ohne zu schreiben. Alle übrigen schreibenden Endpunkte erzeugen neue Datensätze und brauchen kein Token.

## Fehlerbehandlung

Fachliche Fehler sind keine Ausnahmen. Die Anwendungsschicht gibt einen `Result<T>` aus `Core` zurück; der Endpunkt übersetzt ihn:

| Fall | Status |
|---|---|
| Entität nicht gefunden oder fremdem Owner zugeordnet | 404 |
| Namenskollision | 409 |
| Concurrency-Konflikt | 409 |
| Unzulässiger Zustandsübergang | 409 |
| Run auf archivierten Agenten | 409 |
| Eingabevalidierung fehlgeschlagen | 400 |

Ein fremder Owner ergibt bewusst 404 statt 403, damit die Existenz fremder Datensätze nicht preisgegeben wird — mit dem globalen Query-Filter fällt das ohnehin zusammen.

Alle Antworten sind ProblemDetails nach RFC 9457, erzeugt über `AddProblemDetails()`. Ein globaler `IExceptionHandler` fängt Unerwartetes, protokolliert es mit Korrelations-Id und antwortet mit 500, ohne Interna nach außen zu geben. Eingabevalidierung geschieht über DataAnnotations mit der in .NET 10 eingebauten Minimal-API-Validierung.

## Tests

Vier Projekte auf xUnit v3, benannt nach dem Muster `<Projekt>.<Testart>`. Attrappen nur dort, wo eine echte Implementierung nicht verfügbar ist; für alles andere echte Objekte.

**`AgentForge.Core.Unit`** prüft die Bausteine aus `Core`, vor allem das Verhalten von `Result<T>`.

**`AgentForge.Host.Architecture`** prüft die Referenzgraphen: kein Bereich referenziert einen anderen Bereich außerhalb von dessen Contracts, kein Bereich referenziert den Host, jeder Bereich implementiert genau ein `IArea`, jeder Slug ist eindeutig und formgültig. Die Tests hängen am Host, weil nur er alle Assemblies referenziert.

**`AgentForge.Areas.Agents.Unit`** prüft die Fachregeln ohne HTTP: Zustandsübergänge, Snapshot-Erzeugung, Namenseindeutigkeit, Archivierungsverhalten, Sequence-Vergabe der Nachrichten.

**`AgentForge.Host.Integration`** fährt über `WebApplicationFactory` die echte Anwendung gegen SQLite `:memory:` hoch und geht die Endpunkte durch, einschließlich aller Fehlerfälle aus der Tabelle oben und der Health- und Areas-Endpunkte.

Protokollierung läuft über die eingebaute Abstraktion; in Produktionsumgebungen schreibt der Console-Provider strukturiert als JSON.

## Fertigstellungskriterien

1. `dotnet build` und `dotnet test` laufen ohne Fehler und ohne Warnungen durch.
2. Der Host startet; `/_health` und `/_health/ready` antworten mit 200.
3. `GET /api/areas` liefert genau einen Eintrag mit Slug `agents`.
4. Ein Integrationstest legt eine Agent-Definition an, liest, ändert, listet und archiviert sie; die archivierte Definition fehlt in der Liste und ist per Id weiterhin abrufbar.
5. Ein Integrationstest legt einen Run an, prüft Status `Pending` und den eingefrorenen Snapshot, ruft die zwei erzeugten Nachrichten ab, bricht den Run ab und erhält beim zweiten Abbruch 409.
6. Jeder Fehlerfall aus der Fehlertabelle ist durch einen Integrationstest belegt und antwortet als ProblemDetails.
7. Die Wirksamkeit der Architekturtests wird einmalig manuell belegt, indem eine verbotene Projektreferenz vorübergehend hinzugefügt wird und der Test rot wird.

## Annahmen

- .NET 10 ist die Zielplattform; das SDK liegt vor (10.0.110).
- Die Entwicklung findet unter Windows statt; das Repo liegt unter `C:\Users\NEWA002\source\repos\agentforge`.
- Als Sprachmodell-Anbieter ist NanoGPT über dessen OpenAI-kompatible Schnittstelle vorgesehen. In diesem Teilprojekt wird davon nichts benötigt und nichts konfiguriert.
- Für die spätere Container-Ausführung wird Docker auf dem Zielsystem vorausgesetzt. Ob es vorhanden ist, wurde nicht geprüft und ist erst in Teilprojekt 4 relevant.
