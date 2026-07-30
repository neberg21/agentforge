# AgentForge — Workspace-Werkzeuge (Teilprojekt 4)

**Datum:** 2026-07-30
**Status:** Entwurf zur Umsetzung freigegeben
**Umfang:** Teilprojekt 4 von 6 (angepasst: ohne Container)

## Ziel

Die Stub-Werkzeuge für `read_file`, `write_file` und `run_shell` werden durch echte Host-seitige Werkzeuge ersetzt. Jeder Run arbeitet in einem eigenen **Git-Worktree** eines konfigurierten Repositories. Bei erfolgreichem Tool-/LLM-Durchlauf pusht die Runtime den Branch und setzt den Run erst danach auf `Completed`. Der Worktree wird danach entfernt. Der bestehende `RunLoop` und die `ITool`-Naht bleiben im Kern erhalten; bei aktivem Workspace wird der Übergang nach `Completed` hinter den Push verschoben.

Am Ende dieses Teilprojekts gilt: ein Run mit diesen AllowedTools kann Dateien im Worktree lesen und schreiben, Shell-Befehle (z. B. `dotnet test`, `git commit`) ausführen, und bei `Completed` erscheint der Branch `run/{runId}` auf dem Remote.

## Abgrenzung

**Enthalten:** Konfiguration von Remote-URL und lokalem Clone-Pfad, Sicherstellen des Clones, Worktree pro Run von einem Base-Ref, Pfadjail für Dateiwerkzeuge, `run_shell` mit Timeout und Ausgabebegrenzung, Push vor `Completed`, Worktree-Cleanup in jedem Terminalzustand, Unit-/Integrationstests ohne Live-Remote in CI, optionales echtes Git gegen ein lokales Bare-Repo.

**Nicht enthalten:** Docker/Container-Executor (verschoben; Titel „Container“ der Skelett-Roadmap wird hier bewusst nicht erfüllt), Auto-Commit, PR-Erzeugung, Force-Push, Credential-UI, LLM-generierte Branchnamen, Multi-Repo, per-Agent-Remotes, Netz-Allowlists, Übergang `Completed→Failed`.

## Einordnung

Teilprojekte 1–3 sind umgesetzt: Host, Agents-Bereich, Runtime mit Stub-Tools und SSE. Die Spec zu Teilprojekt 3 sah vor, Stubs an derselben `ITool`-Naht durch echte Semantik zu ersetzen; Container waren dort als Folgeprojekt gedacht. Dieses Dokument spezifiziert die **Git-Worktree- und Host-Tool-Variante** zuerst, weil die Zielarbeit an einem vorhandenen Checkout stattfindet. Ein späteres Teilprojekt kann Shell/Datei in Docker bind-mounten, ohne den Loop erneut umzubauen.

## Grundentscheidungen

1. **Konfigurierter Remote + lokaler Pfad.** `Areas:Agents:Workspace` enthält `RemoteUrl` und `LocalPath`. Fehlt der Clone, wird er angelegt; existiert er, wird er wiederverwendet (`fetch` vor Worktree).
2. **Ein Worktree pro Run.** Branch-Name fest: `run/{runId}` (Guid-Zeichenkette). Basis: konfigurierbares `BaseRef` (Vorgabe `main`).
3. **Host-Prozess, kein Docker.** Tools laufen im Host; Pfade werden kanonisch aufgelöst und müssen unter dem Worktree-Root bleiben.
4. **Agent committed, Runtime pusht.** Commits entstehen nur über `run_shell` (`git add`/`git commit`). Die Runtime pusht nur, wenn der Loop erfolgreich enden würde (kein weiteres Tool-Calling, kein MaxTurns-/Transportfehler, nicht abgebrochen).
5. **Push vor Completed.** Solange Workspace aktiv ist, belässt der Loop den Run bei Erfolg in `Running`. `FinishAsync` pusht; bei Erfolg → `Complete`, bei Push-Fehler → `Fail` (weiterhin aus `Running`). Bei `Failed`/`Cancelled` kein Push. Es gibt keinen Übergang `Completed→Failed`.
6. **Cleanup immer.** Worktree wird nach dem Finish-Schritt entfernt, auch wenn der Push fehlschlägt.
7. **Begin-Fehler aus Pending.** Schlägt Clone/Worktree fehl, gilt `Pending→Failed` (Domain um diesen Übergang erweitern) mit klarer `Error`-Meldung; Loop startet nicht.
8. **Tests ohne Netz.** Standardpipeline nutzt Fake-`IGitWorkspace` oder ein lokales Bare-Repo; kein Pflicht-Push gegen ein öffentliches Remote in CI.
9. **Workspace abschaltbar.** Flag `Enabled`: aus → bisheriges Stub-Verhalten und Complete im Loop wie heute; an → echte Tools und deferred Complete. ValidateOnStart, wenn enabled.

## Architektur

```
RunWorker.ProcessAsync(runId)
  └─ IRunWorkspaceSession.BeginAsync(runId)
        ensure clone at LocalPath from RemoteUrl
        git worktree add -b run/{runId} {WorktreesRoot}/{runId} BaseRef
        bind AsyncLocal session (Root, BranchName)
        on failure → Pending→Failed; return
  └─ RunLoop.ExecuteAsync(runId)
        Workspace.Enabled: on natural success leave Running (do not Complete)
        Workspace disabled: Complete inside loop as today
  └─ IRunWorkspaceSession.FinishAsync(runId)
        if Running and success-pending → push; Complete or Fail
        if Failed/Cancelled → no push
        always → worktree remove
```

| Baustein | Verantwortung |
|---|---|
| `WorkspaceOptions` | `Enabled`, `RemoteUrl`, `LocalPath`, `BaseRef`, `WorktreesRoot`, `ShellTimeout`, Ausgabe-Cap |
| `IGitWorkspace` | Clone/Fetch, Worktree add/remove, Push — dünne Prozess-Hülle um `git` |
| `IRunWorkspaceSession` | Begin/Finish, Session-Kontext für Tools |
| `ReadFileTool` / `WriteFileTool` / `RunShellTool` | `ITool`-Implementierungen |
| `ToolRegistry` | Die drei echten Tools bei Start registrieren; `EnsureStubs` nur für übrige AllowedTools-Namen |

Alles liegt unter `src/Areas/AgentForge.Areas.Agents/Runtime/` (z. B. `Workspace/`, Erweiterung von `Tools/`). Registrierung in `AgentsArea.ConfigureServices`. Der Host kennt weiterhin nur `AddArea<AgentsArea>()`.

**Deferred Complete:** `RunLoop` liest `IOptions<AgentsOptions>.Workspace.Enabled`. Ist es true und würde der Loop heute `Complete` aufrufen, speichert er den Erfolg ohne Statuswechsel (weiter `Running`) und beendet. `FinishAsync` erkennt „Loop endete ohne Failed/Cancelled“ am Status `Running` nach Rückkehr aus dem Loop und führt Push + Complete aus. Schlägt der Loop fehl oder bricht ab, bleibt Finish bei Cleanup ohne Push.

### Session-Bindung

Für die Dauer von `ProcessAsync` setzt die Session einen `AsyncLocal`-Kontext (Root-Pfad, RunId, BranchName). Tools lesen nur diesen Kontext; fehlt er, liefern sie einen strukturierten Tool-Fehler — kein Prozessabsturz.

## Werkzeugverträge

Argumente und Ergebnisse sind JSON-Zeichenketten wie bei den Stubs.

| Name | Argumente | Verhalten |
|---|---|---|
| `read_file` | `{ "path": "..." }` | Textdatei unter dem Worktree lesen; Escape (`..`, absolute Pfade außerhalb) ablehnen |
| `write_file` | `{ "path": "...", "content": "..." }` | Anlegen/Überschreiben; Elternverzeichnisse erzeugen |
| `run_shell` | `{ "command": "..." }` | Prozess mit `WorkingDirectory` = Worktree-Root; Timeout; Ergebnis `{ "exitCode", "stdout", "stderr" }`; stdout/stderr auf konfigurierbare Maximalgröße kürzen (Vorgabe 64 KiB je Stream) |

Keine Befehls-Allow-/Denylist. Nicht-null Exitcodes und Timeouts sind Tool-Ergebnisse, kein automatisches Run-`Failed`.

Unbekannte oder andere AllowedTools-Namen behalten das heutige Stub-/Fehlerverhalten.

## Konfiguration

Abschnitt `Areas:Agents:Workspace` (an `AgentsOptions` nested, `ValidateOnStart` wenn `Enabled`):

| Schlüssel | Bedeutung |
|---|---|
| `Enabled` | Schaltet echte Workspace-Tools und deferred Complete ein |
| `RemoteUrl` | Git-Remote (clone/push) |
| `LocalPath` | Absoluter Pfad zum lokalen Clone (empfohlen) oder relativ zum Content-Root des Hosts |
| `BaseRef` | Ausgangs-Ref für Worktrees (Vorgabe `main`) |
| `WorktreesRoot` | Verzeichnis für Run-Worktrees (absolut oder relativ zum Host-Content-Root; git-ignoriert; typisch Repo-`workspaces/`) |
| `ShellTimeout` | Maximaldauer eines `run_shell`-Aufrufs |
| `MaxOutputChars` | Kürzung von stdout/stderr (optional, mit Vorgabe) |

Credentials: vorhandene Git-Credential-Hilfe / Umgebung des Hostprozesses. Keine API-Keys für Git in `appsettings` in diesem Teilprojekt.

Development: `Enabled` kann false bleiben, bis Remote und Clone konfiguriert sind. Testing: Fake oder disabled.

## Fehlerbehandlung

| Situation | Verhalten |
|---|---|
| Clone/Fetch/Worktree bei Begin schlägt fehl | `Pending→Failed` mit `Error`; Loop wird nicht aufgerufen |
| Pfad escape / Datei fehlt | Tool-JSON `{ "ok": false, ... }`; Loop läuft weiter |
| Shell-Timeout / Exit ≠ 0 | Tool-JSON mit Codes/Streams; kein automatisches Run-Failed |
| Push schlägt fehl (Run noch `Running`) | `Running→Failed` mit Push-Fehler; Worktree entfernen |
| Worktree-Remove schlägt fehl | Warnung loggen; Run-Status nicht überschreiben |
| `Enabled` und Pflichtfelder fehlen | Prozessstart bricht mit Validierungsfehler ab |

## Tests

- **Unit:** Pfadjail; read/write; Shell-Cwd und Timeout; Branchname aus RunId; Finish pusht nur bei Erfolgsweg aus `Running`; Push-Fehler → Failed ohne jemals Completed gewesen zu sein (Fake-`IGitWorkspace`).
- **Domain:** `Pending→Failed` erlaubt; Transitionstests anpassen.
- **Integration:** Run mit Fake-LLM und Fake-Git: Begin → Tool → Push → Completed → Cleanup. Mit echtem `git` optional gegen temp Bare-Remote, überspringbar wenn `git` fehlt.
- Architekturtests der Bereichsgrenzen bleiben grün.
- Kein Live-Push gegen externe Remotes in der Standard-CI.

## Fertigstellungskriterien

1. `dotnet build` und `dotnet test` ohne Fehler und ohne Warnungen.
2. Bei `Workspace:Enabled` sind `read_file` / `write_file` / `run_shell` echte Implementierungen; andere AllowedTools bleiben Stub/Fehler.
3. Pro Run entsteht ein Worktree von `BaseRef` auf Branch `run/{runId}`.
4. Push erfolgt nur auf dem Erfolgsweg; danach erst `Completed`. Bei `Failed`/`Cancelled` kein Push.
5. Worktree wird in jedem Terminalzustand entfernt (Best Effort).
6. Push-Fehler führt zu `Failed` aus `Running`, nie aus `Completed`.
7. RunLoop bleibt an der `ITool`-Naht stabil; keine Docker-Abhängigkeit.
8. README beschreibt Workspace-Konfiguration und den Unterschied zu späteren Container-Werkzeugen.

## Bewusste Nicht-Ziele / Roadmap-Hinweis

- Der Skelett-Name „Container-Executor und Werkzeuge“ wird in diesem Teilprojekt **nicht** eingelöst. Container folgen in einem eigenen Schritt, idealerweise mit denselben Tool-Namen und Worktree-/Mount-Semantik.
- Kein separates Runtime-Projekt; keine Host-eigene Worker-Logik außerhalb von `AgentsArea`.
- Keine Branchnamen aus dem LLM.
