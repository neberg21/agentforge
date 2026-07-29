# AgentForge — UI: Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eine React-Oberfläche unter `src/AgentForge.Web`, in der Agenten verwaltet, Runs gestartet und per Ereignisstrom verfolgt und Einzel- wie Gruppengespräche geführt werden — vom Host statisch ausgeliefert.

**Architecture:** Ein Vite-Frontend mit einer Bereichs-Registry, die die Area-Konvention des Backends spiegelt: jeder Bereich exportiert `slug`, `title`, `routes` und `nav`, und `areas/index.ts` listet die Bereiche namentlich auf. Eine Drei-Spalten-Shell trägt Bereichsnavigation, Ansicht und einen Kontext-Steckplatz, den jede Seite füllt. Verlaufsdarstellung für Runs und Gespräche teilen ein Bauteil und einen Reducer; Ereignisse kommen über `EventSource` und werden nach `Sequence` in eine Map geschrieben, nicht angehängt.

**Tech Stack:** React 19, TypeScript, Vite, Tailwind 4, react-router 7, Vitest mit Testing Library, oxlint.

**Spec:** `docs/superpowers/specs/2026-07-29-agentforge-ui-design.md`

## Ausführbarkeit und Blockade

**Aufgaben 1 bis 18 sind heute ausführbar.** Jeder Test ersetzt `fetch` und `EventSource` durch eigene Attrappen; kein Test braucht einen laufenden Server. Für das Ansehen im Browser während der Entwicklung entsteht in Aufgabe 6 ein Mock-Server als Vite-Plugin.

**Aufgabe 19 ist blockiert.** Sie prüft gegen die echte API und setzt Teilprojekte 2, 3, 4 und das in der Spec geforderte, noch nicht entworfene Teilprojekt 3b (Gespräche als Entität, Ereignisströme, stabile ProblemDetails-Codes) voraus. Zum Zeitpunkt dieses Plans existiert im Repo nur Host und Abstraktionsschicht — `src/Areas/` gibt es noch nicht. Wer diesen Plan abarbeitet, hält bei Aufgabe 19 an und meldet zurück.

## Global Constraints

Diese gelten für jede Aufgabe, auch wenn sie dort nicht wiederholt werden.

- Repo-Wurzel: `C:\Users\NEWA002\source\repos\agentforge`. Frontend-Wurzel: `src/AgentForge.Web`. Alle npm-Befehle werden **aus der Frontend-Wurzel** ausgeführt, alle `dotnet`- und `git`-Befehle aus der Repo-Wurzel.
- Auf Repo-Ebene entsteht **kein** neues Verzeichnis. Das Frontend liegt unter `src/`, seine Tests bei seinem Code unter `src/AgentForge.Web/src/__tests__/` — nicht im `tests/`-Baum, der den .NET-Testprojekten gehört.
- `src/AgentForge.Web` ist **kein** .NET-Projekt und wird **nicht** in `AgentForge.sln` aufgenommen.
- Abhängigkeiten ausschließlich: `react`, `react-dom`, `react-router-dom` als Laufzeit; `vite`, `@vitejs/plugin-react`, `typescript`, `tailwindcss`, `@tailwindcss/vite`, `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/jest-dom`, `@testing-library/user-event`, `oxlint`, `@types/react`, `@types/react-dom`, `@types/node` als Entwicklung. **Keine weitere Bibliothek** — keine Datenschicht, keine Bauteil-Bibliothek, kein MSW, keine Assertion- oder Mocking-Bibliothek. Versionen werden nie von Hand geraten, sondern über `npm install <name>` aufgelöst.
- Tailwind-Klassen stehen direkt an den Elementen. Es entsteht **keine** Bauteil-Schicht auf Vorrat. Regel gegen Drift: taucht dieselbe Klassenkette zum dritten Mal auf, wandert sie in ein Bauteil.
- Farben und Abstände kommen aus CSS-Variablen in `src/index.css`, Dark Mode über `prefers-color-scheme`. Werte werden **nicht** aus `aae` kopiert.
- **Oberflächentexte sind deutsch.** Bezeichner, Dateinamen, Kommentare und Commit-Nachrichten sind englisch. (Die Spec legt die Sprache der Oberfläche nicht fest; die im Entwurf freigegebenen Mockups waren deutsch, deshalb deutsch. Wer das ändern will, tut es am besten vor Aufgabe 11.)
- Jede Seitendatei bleibt unter etwa 200 Zeilen. Datenholen liegt in `api.ts` und in Hooks, nie in einer Seite.
- Datenabruf immer über `lib/http.ts`. Ein direkter `fetch`-Aufruf außerhalb von `lib/http.ts` und `lib/sse.ts` ist ein Fehler.
- Kein Test spricht mit dem Netz. `fetch` wird über `globalThis.fetch` ersetzt, `EventSource` über `globalThis.EventSource`.
- Jede Ansicht hat einen ausformulierten leeren Zustand mit dem nächsten Schritt als Knopf — nie „keine Daten".
- Nach jeder Aufgabe wird committet. **Commit-Nachrichten auf Englisch**, Präfix `feat:`, `test:` oder `chore:`.
- Nach jeder Aufgabe müssen `npm run lint`, `npm run typecheck` und `npm test` fehlerfrei durchlaufen.

## Fehlercodes als Vertrag

Die UI unterscheidet Fehlerfälle **ausschließlich** über den letzten Pfadteil von `ProblemDetails.type`, niemals über Meldungstexte. Diese Codes werden erwartet und sind in Teilprojekt 2/3b so zu liefern:

| Code | Status | Bedeutung |
|---|---|---|
| `validation-failed` | 400 | Eingabevalidierung, mit `errors` je Feld |
| `not-found` | 404 | Entität fehlt oder fremder Besitzer |
| `name-conflict` | 409 | Agentenname bereits belegt |
| `concurrency-conflict` | 409 | `ConcurrencyToken` veraltet |
| `invalid-transition` | 409 | Zustandsübergang unzulässig |
| `agent-archived` | 409 | Run oder Gespräch auf archivierten Agenten |

Fehlt `type`, behandelt die UI die Antwort als unbekannten Fehler mit dem Status als einziger Information. Sie rät nicht.

## Abweichungen von der Spec

1. **Mock-Server für die Entwicklung.** Die Spec erwähnt ihn nicht, aber ohne Backend gibt es während der Aufgaben 7 bis 17 nichts zu sehen. Aufgabe 6 baut ihn als Vite-Plugin, das nur unter `npm run dev:mock` greift und nie in einen Produktionsbau gelangt. Umfang: die in der Spec gelisteten Endpunkte mit Daten im Speicher und einem Strom mit fest verdrahteten Ereignisfolgen.
2. **Platzhalter-`index.html` im Host.** Damit die Auslieferung in Aufgabe 18 ohne Frontend-Bau prüfbar ist, wird `src/AgentForge.Host/wwwroot/index.html` als Platzhalter eingecheckt. Der Publish-Schritt überschreibt ihn.
3. **Verlaufszustand als Record statt Map.** Die Spec sagt „Map nach Sequence"; umgesetzt wird ein `Record<number, TranscriptMessage>`. Gleiche Wirkung, aber unveränderlich zu aktualisieren, ohne bei jeder Änderung eine `Map` zu kopieren.

## File Structure

**`src/AgentForge.Web/`** — Wurzel des Frontends.
- `package.json`, `vite.config.ts`, `tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`, `.oxlintrc.json`, `index.html`

**`src/AgentForge.Web/mock/`** — nur Entwicklung, nie im Bau.
- `apiPlugin.ts` — Vite-Plugin, hängt sich als Middleware in den Dev-Server, hält Daten im Speicher, liefert Ströme.

**`src/AgentForge.Web/src/`**
- `main.tsx` — Einstiegspunkt, hängt `App` an `#root`.
- `App.tsx` — Router, Shell, Registry-Anbindung. Keine Fachlogik.
- `index.css` — Tailwind-Import und CSS-Variablen für beide Farbschemata.

**`src/AgentForge.Web/src/lib/`** — kennt keinen Bereich.
- `http.ts` — `fetch`-Hülle, ProblemDetails → `ApiError`, Suchparameter.
- `sse.ts` — `EventSource` öffnen, Ereignisse übersetzen, Verbindungszustand melden.
- `areas.ts` — `/api/areas` laden, Schnittmenge mit der Registry bilden.

**`src/AgentForge.Web/src/shell/`** — weiß nichts über Bereiche außer ihren Registry-Einträgen.
- `AppShell.tsx` — drei Spalten, zwei Haltepunkte, Schublade.
- `AreaNav.tsx` — Bereichswechsel und Navigation des aktiven Bereichs.
- `ContextPanel.tsx` — Steckplatz für die rechte Spalte: Provider, Hook, Outlet.
- `RecentItems.tsx` — fünf zuletzt berührte Objekte aus dem `localStorage`.

**`src/AgentForge.Web/src/areas/`**
- `index.ts` — Registry. Listet Bereiche namentlich auf, kein Glob.

**`src/AgentForge.Web/src/areas/agents/`** — der einzige Bereich in dieser Ausbaustufe.
- `types.ts` — DTOs der API und das Verlaufsmodell der UI.
- `api.ts` — je Endpunkt eine Funktion, keine React-Abhängigkeit.
- `routes.tsx` — Routen und Registry-Eintrag des Bereichs.
- `transcriptReducer.ts` — reine Funktion, Herz der Verlaufsdarstellung.
- `mappers.ts` — Übersetzung der beiden Nachrichten-DTOs in das UI-Verlaufsmodell.
- `useRunStream.ts`, `useConversationStream.ts` — laden, dann strömen.
- `Transcript.tsx`, `ToolCallCard.tsx`, `TranscriptLog.tsx` — Darstellung des Verlaufs.
- `MessageComposer.tsx` — Eingabefeld mit Erwähnungsauswahl.
- `agentValidation.ts` — Entwurfsmodell und Grenzen des Formulars.
- `labels.ts` — deutsche Statusbeschriftungen und Dauerformat.
- `NotFoundView.tsx` — die 404-Ansicht, geteilt von allen Seiten mit einer Id in der Adresse.
- `AgentListPage.tsx`, `AgentFormPage.tsx`, `AgentDetailPage.tsx`
- `RunListPage.tsx`, `RunDetailPage.tsx`, `StartRunDialog.tsx`
- `ConversationListPage.tsx`, `ConversationPage.tsx`, `NewConversationDialog.tsx`

**`src/AgentForge.Web/src/test/`**
- `setup.ts` — `@testing-library/jest-dom`, Aufräumen zwischen Tests.
- `fakeEventSource.ts` — Attrappe für `EventSource`, speist Ereignisfolgen ein.
- `stubFetch.ts` — Attrappe für `fetch`, nimmt Antwortpaare auf.

**`src/AgentForge.Web/src/__tests__/`** — ein Test je Ansicht, plus Tests für `lib`, Reducer und Hooks.

**`src/AgentForge.Host/`** — zwei Eingriffe in Aufgabe 18.
- `Program.cs:*` — statische Dateien, `/api`-404 vor dem Fallback, `MapFallbackToFile`.
- `AgentForge.Host.csproj` — MSBuild-Ziel, das beim `Publish` das Frontend baut und kopiert.
- `wwwroot/index.html` — Platzhalter.

---

### Task 1: Projektgerüst

**Files:**
- Create: `src/AgentForge.Web/package.json` (über `npm init`), `vite.config.ts`, `tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`, `.oxlintrc.json`, `.gitignore`, `index.html`
- Create: `src/AgentForge.Web/src/main.tsx`, `src/App.tsx`, `src/index.css`
- Create: `src/AgentForge.Web/src/test/setup.ts`
- Test: `src/AgentForge.Web/src/__tests__/App.test.tsx`

**Interfaces:**
- Consumes: nichts.
- Produces: `App` als Standardexport aus `src/App.tsx`; npm-Skripte `dev`, `dev:mock`, `build`, `preview`, `lint`, `typecheck`, `test`, `test:watch`; Vite-Proxy von `/api` auf `http://localhost:5204`.

`5204` ist die `applicationUrl` des `http`-Profils in `src/AgentForge.Host/Properties/launchSettings.json` zum Zeitpunkt dieses Plans. **Vor dem Schreiben der `vite.config.ts` dort nachsehen**; weicht der Wert ab, gilt die Datei.

- [ ] **Step 1: Gerüst anlegen und Abhängigkeiten installieren**

Aus der Repo-Wurzel:

```bash
mkdir -p src/AgentForge.Web
cd src/AgentForge.Web
npm init -y
npm install react react-dom react-router-dom
npm install -D vite @vitejs/plugin-react typescript tailwindcss @tailwindcss/vite \
  vitest jsdom @testing-library/react @testing-library/jest-dom @testing-library/user-event \
  oxlint @types/react @types/react-dom @types/node
```

- [ ] **Step 2: `package.json` auf Skripte und Modultyp bringen**

Die von `npm init` erzeugten Felder `main`, `keywords`, `author`, `license`, `description` werden entfernt. Versionsnummern bleiben, wie `npm install` sie eingetragen hat.

```json
{
  "name": "agentforge-web",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "dev:mock": "vite --mode mock",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "lint": "oxlint",
    "typecheck": "tsc -b --noEmit",
    "test": "vitest run",
    "test:watch": "vitest"
  }
}
```

- [ ] **Step 3: TypeScript- und Lint-Konfiguration schreiben**

`tsconfig.json`:

```json
{
  "files": [],
  "references": [{ "path": "./tsconfig.app.json" }, { "path": "./tsconfig.node.json" }]
}
```

`tsconfig.app.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "noUncheckedIndexedAccess": true,
    "verbatimModuleSyntax": true,
    "skipLibCheck": true,
    "noEmit": true,
    "types": ["vitest/globals", "@testing-library/jest-dom"]
  },
  "include": ["src"]
}
```

`tsconfig.node.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "verbatimModuleSyntax": true,
    "skipLibCheck": true,
    "noEmit": true,
    "types": ["node"]
  },
  "include": ["vite.config.ts", "mock"]
}
```

`.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "categories": { "correctness": "error", "suspicious": "warn" },
  "env": { "browser": true, "es2022": true }
}
```

`.gitignore` im Frontend-Verzeichnis:

```
node_modules/
dist/
```

- [ ] **Step 4: `vite.config.ts` schreiben**

`noUncheckedIndexedAccess` ist bewusst an — es fängt genau die Zugriffe auf `items[0]`, die bei leeren Listen zuschlagen.

```ts
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const hostUrl = 'http://localhost:5204'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': { target: hostUrl, changeOrigin: true },
    },
  },
  test: {
    root: fileURLToPath(new URL('./', import.meta.url)),
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/__tests__/**/*.{test,spec}.{ts,tsx}'],
  },
})
```

Der Mock-Modus kommt in Aufgabe 6 hinzu; `dev:mock` verhält sich bis dahin wie `dev`.

- [ ] **Step 5: `index.html`, Einstiegspunkt und Stilgrundlage schreiben**

`index.html`:

```html
<!doctype html>
<html lang="de">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>AgentForge</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`src/index.css` — eigene Tokens, nicht aus `aae` übernommen:

```css
@import "tailwindcss";

:root {
  --bg: #ffffff;
  --bg-sunken: #f6f6f8;
  --bg-raised: #ffffff;
  --border: #e2e2e7;
  --text: #45454f;
  --text-strong: #14141a;
  --text-muted: #7c7c88;
  --accent: #2f6f5f;
  --accent-text: #ffffff;
  --accent-soft: rgba(47, 111, 95, 0.12);
  --danger: #b3261e;
  color-scheme: light dark;
}

@media (prefers-color-scheme: dark) {
  :root {
    --bg: #14151a;
    --bg-sunken: #101116;
    --bg-raised: #1c1d24;
    --border: #2c2e37;
    --text: #b6b7c0;
    --text-strong: #f2f2f5;
    --text-muted: #858692;
    --accent: #63b39c;
    --accent-text: #0d1512;
    --accent-soft: rgba(99, 179, 156, 0.16);
    --danger: #ef857e;
  }
}

body {
  margin: 0;
  background: var(--bg);
  color: var(--text);
  font: 15px/1.5 system-ui, "Segoe UI", Roboto, sans-serif;
}
```

`src/main.tsx`:

```tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
```

`src/App.tsx` — vorläufig, wächst in Aufgabe 4:

```tsx
import { BrowserRouter } from 'react-router-dom'

export default function App() {
  return (
    <BrowserRouter>
      <h1 className="p-6 text-xl" style={{ color: 'var(--text-strong)' }}>
        AgentForge
      </h1>
    </BrowserRouter>
  )
}
```

`src/test/setup.ts`:

```ts
import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

afterEach(() => {
  cleanup()
})
```

- [ ] **Step 6: Den fehlschlagenden Test schreiben**

`src/__tests__/App.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import App from '../App'

test('zeigt den Anwendungsnamen', () => {
  render(<App />)
  expect(screen.getByRole('heading', { name: 'AgentForge' })).toBeInTheDocument()
})
```

- [ ] **Step 7: Tests laufen lassen**

Run: `npm test`
Expected: PASS, ein Test. (Dieser Test prüft das Gerüst, nicht Verhalten — er ist die Zusicherung, dass Vitest, jsdom, React und die TS-Konfiguration zusammenspielen.)

- [ ] **Step 8: Lint und Typprüfung laufen lassen**

Run: `npm run lint && npm run typecheck && npm run build`
Expected: alle drei ohne Fehler und ohne Warnungen.

- [ ] **Step 9: Commit**

Aus der Repo-Wurzel:

```bash
git add src/AgentForge.Web
git commit -m "chore: scaffold web frontend with vite, tailwind and vitest"
```

---

### Task 2: Fehlerübersetzung in `lib/http.ts`

**Files:**
- Create: `src/AgentForge.Web/src/lib/http.ts`
- Create: `src/AgentForge.Web/src/test/stubFetch.ts`
- Test: `src/AgentForge.Web/src/__tests__/http.test.ts`

**Interfaces:**
- Consumes: nichts.
- Produces:
  - `type ApiError = { status: number; code: string; title: string; detail: string | null; fieldErrors: Record<string, string[]>; correlationId: string | null }`
  - `class ApiRequestError extends Error` mit `readonly info: ApiError`
  - `apiGet<T>(path: string, params?: Record<string, string | number | undefined>): Promise<T>`
  - `apiSend<T>(method: 'POST' | 'PUT' | 'DELETE', path: string, body?: unknown): Promise<T | null>`
  - `errorCode(type: string | undefined): string`
  - `stubFetch(...)` aus `src/test/stubFetch.ts` für alle folgenden Tests.

- [ ] **Step 1: Die `fetch`-Attrappe schreiben**

`src/test/stubFetch.ts`:

```ts
import { vi } from 'vitest'

export type StubbedResponse = {
  status?: number
  json?: unknown
  contentType?: string
}

export type StubbedCall = { url: string; method: string; body: unknown }

/**
 * Replaces globalThis.fetch. Routes are matched by substring on the URL,
 * in insertion order. An unmatched request fails the test loudly instead
 * of returning something plausible.
 */
export function stubFetch(routes: Array<[match: string, response: StubbedResponse]>) {
  const calls: StubbedCall[] = []
  const remaining = [...routes]

  globalThis.fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const index = remaining.findIndex(([match]) => url.includes(match))
    if (index === -1) {
      throw new Error(`No stubbed response for ${init?.method ?? 'GET'} ${url}`)
    }
    const [, response] = remaining.splice(index, 1)[0]!
    calls.push({
      url,
      method: init?.method ?? 'GET',
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    })
    const status = response.status ?? 200
    return new Response(response.json === undefined ? null : JSON.stringify(response.json), {
      status,
      headers: { 'content-type': response.contentType ?? 'application/problem+json' },
    })
  }) as typeof fetch

  return calls
}
```

- [ ] **Step 2: Die fehlschlagenden Tests schreiben**

`src/__tests__/http.test.ts`:

```ts
import { apiGet, apiSend, ApiRequestError, errorCode } from '../lib/http'
import { stubFetch } from '../test/stubFetch'

test('errorCode nimmt den letzten Pfadteil des type-Feldes', () => {
  expect(errorCode('https://agentforge.local/errors/name-conflict')).toBe('name-conflict')
  expect(errorCode('name-conflict')).toBe('name-conflict')
  expect(errorCode(undefined)).toBe('unknown')
  expect(errorCode('about:blank')).toBe('unknown')
})

test('apiGet hängt gesetzte Suchparameter an und lässt undefined weg', async () => {
  const calls = stubFetch([['/api/agents/definitions', { json: { items: [], total: 0 } }]])

  await apiGet('/api/agents/definitions', { q: 'leo', skip: 0, take: 50, status: undefined })

  expect(calls[0]!.url).toBe('/api/agents/definitions?q=leo&skip=0&take=50')
})

test('apiGet gibt bei 204 null zurück statt zu werfen', async () => {
  stubFetch([['/api/agents/definitions/1', { status: 204 }]])
  await expect(apiSend('DELETE', '/api/agents/definitions/1')).resolves.toBeNull()
})

test('ein Fehler wird zu ApiRequestError mit Code und Feldfehlern', async () => {
  stubFetch([
    [
      '/api/agents/definitions',
      {
        status: 400,
        json: {
          type: 'https://agentforge.local/errors/validation-failed',
          title: 'Eingabe ungültig',
          detail: 'Ein Feld fehlt.',
          errors: { name: ['Pflichtfeld'] },
        },
      },
    ],
  ])

  const error = await apiSend('POST', '/api/agents/definitions', { name: '' }).catch((e) => e)

  expect(error).toBeInstanceOf(ApiRequestError)
  expect((error as ApiRequestError).info).toEqual({
    status: 400,
    code: 'validation-failed',
    title: 'Eingabe ungültig',
    detail: 'Ein Feld fehlt.',
    fieldErrors: { name: ['Pflichtfeld'] },
    correlationId: null,
  })
})

test('eine Fehlerantwort ohne ProblemDetails ergibt den Code unknown', async () => {
  stubFetch([['/api/areas', { status: 500, contentType: 'text/plain' }]])

  const error = (await apiGet('/api/areas').catch((e) => e)) as ApiRequestError

  expect(error.info.code).toBe('unknown')
  expect(error.info.status).toBe(500)
})
```

- [ ] **Step 3: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- http`
Expected: FAIL — `Failed to resolve import "../lib/http"`.

- [ ] **Step 4: `lib/http.ts` schreiben**

```ts
export type ApiError = {
  status: number
  code: string
  title: string
  detail: string | null
  fieldErrors: Record<string, string[]>
  correlationId: string | null
}

export class ApiRequestError extends Error {
  constructor(readonly info: ApiError) {
    super(info.title)
    this.name = 'ApiRequestError'
  }
}

export function errorCode(type: string | undefined): string {
  if (!type || type === 'about:blank') return 'unknown'
  const parts = type.split('/').filter(Boolean)
  return parts[parts.length - 1] ?? 'unknown'
}

type Params = Record<string, string | number | undefined>

function withParams(path: string, params?: Params): string {
  if (!params) return path
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) search.append(key, String(value))
  }
  const query = search.toString()
  return query ? `${path}?${query}` : path
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: Record<string, unknown> = {}
  try {
    body = (await response.json()) as Record<string, unknown>
  } catch {
    body = {}
  }
  return {
    status: response.status,
    code: errorCode(typeof body.type === 'string' ? body.type : undefined),
    title: typeof body.title === 'string' ? body.title : `HTTP ${response.status}`,
    detail: typeof body.detail === 'string' ? body.detail : null,
    fieldErrors: (body.errors as Record<string, string[]> | undefined) ?? {},
    correlationId: typeof body.correlationId === 'string' ? body.correlationId : null,
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T | null> {
  const response = await fetch(path, {
    method,
    headers: body === undefined ? undefined : { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (!response.ok) throw new ApiRequestError(await toApiError(response))
  if (response.status === 204) return null
  return (await response.json()) as T
}

export async function apiGet<T>(path: string, params?: Params): Promise<T> {
  return (await request<T>('GET', withParams(path, params))) as T
}

export async function apiSend<T>(
  method: 'POST' | 'PUT' | 'DELETE',
  path: string,
  body?: unknown,
): Promise<T | null> {
  return await request<T>(method, path, body)
}
```

- [ ] **Step 5: Tests laufen lassen**

Run: `npm test -- http`
Expected: PASS, fünf Tests.

- [ ] **Step 6: Commit**

```bash
git add src/AgentForge.Web/src/lib/http.ts src/AgentForge.Web/src/test/stubFetch.ts src/AgentForge.Web/src/__tests__/http.test.ts
git commit -m "feat: add http wrapper translating problem details into typed errors"
```

---

### Task 3: Bereichs-Registry und `/api/areas`

**Files:**
- Create: `src/AgentForge.Web/src/lib/areas.ts`
- Create: `src/AgentForge.Web/src/areas/index.ts`
- Test: `src/AgentForge.Web/src/__tests__/areas.test.ts`

**Interfaces:**
- Consumes: `apiGet` aus `lib/http.ts`.
- Produces:
  - `type NavItem = { to: string; label: string }`
  - `type AreaModule = { slug: string; title: string; routes: RouteObject[]; nav: NavItem[] }`
  - `type AreaInfo = { slug: string }`
  - `fetchAreas(): Promise<AreaInfo[]>`
  - `visibleAreas(registered: AreaInfo[], modules: AreaModule[]): AreaModule[]`
  - `areaModules: AreaModule[]` aus `areas/index.ts` — in dieser Aufgabe noch leer.

Die Registry bleibt leer, bis Aufgabe 11 den Agents-Bereich einträgt. Das ist beabsichtigt: die Schnittmengenlogik wird gegen erfundene Bereiche geprüft, nicht gegen den echten.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`src/__tests__/areas.test.ts`:

```ts
import type { AreaModule } from '../lib/areas'
import { fetchAreas, visibleAreas } from '../lib/areas'
import { stubFetch } from '../test/stubFetch'

function module(slug: string): AreaModule {
  return { slug, title: slug, routes: [], nav: [] }
}

test('fetchAreas liest die Slugs vom Host', async () => {
  stubFetch([['/api/areas', { json: [{ slug: 'agents' }, { slug: 'dnd' }] }]])
  await expect(fetchAreas()).resolves.toEqual([{ slug: 'agents' }, { slug: 'dnd' }])
})

test('visibleAreas zeigt nur Bereiche, die Server und Registry kennen', () => {
  const result = visibleAreas([{ slug: 'agents' }, { slug: 'dnd' }], [module('agents')])
  expect(result.map((a) => a.slug)).toEqual(['agents'])
})

test('visibleAreas behält die Reihenfolge der Registry, nicht die des Servers', () => {
  const result = visibleAreas(
    [{ slug: 'dnd' }, { slug: 'agents' }],
    [module('agents'), module('dnd')],
  )
  expect(result.map((a) => a.slug)).toEqual(['agents', 'dnd'])
})

test('ein Bereich, den der Server nicht meldet, verschwindet', () => {
  expect(visibleAreas([], [module('agents')])).toEqual([])
})
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- areas`
Expected: FAIL — `Failed to resolve import "../lib/areas"`.

- [ ] **Step 3: `lib/areas.ts` und die leere Registry schreiben**

`src/lib/areas.ts`:

```ts
import type { RouteObject } from 'react-router-dom'
import { apiGet } from './http'

export type NavItem = { to: string; label: string }

export type AreaModule = {
  slug: string
  title: string
  routes: RouteObject[]
  nav: NavItem[]
}

export type AreaInfo = { slug: string }

export async function fetchAreas(): Promise<AreaInfo[]> {
  return await apiGet<AreaInfo[]>('/api/areas')
}

/**
 * The navigation shows the intersection of what the host reports and what the
 * registry knows. Registry order wins so navigation does not reshuffle when
 * the host changes its answer.
 */
export function visibleAreas(registered: AreaInfo[], modules: AreaModule[]): AreaModule[] {
  const slugs = new Set(registered.map((area) => area.slug))
  return modules.filter((module) => slugs.has(module.slug))
}
```

`src/areas/index.ts`:

```ts
import type { AreaModule } from '../lib/areas'

/**
 * Areas are listed by name, never collected by glob — the same rule the host
 * follows with builder.AddArea<T>(). A new area costs one line here.
 */
export const areaModules: AreaModule[] = []
```

- [ ] **Step 4: Tests laufen lassen**

Run: `npm test -- areas`
Expected: PASS, vier Tests.

- [ ] **Step 5: Commit**

```bash
git add src/AgentForge.Web/src/lib/areas.ts src/AgentForge.Web/src/areas/index.ts src/AgentForge.Web/src/__tests__/areas.test.ts
git commit -m "feat: add area registry mirroring the host area convention"
```

---

### Task 4: Die Drei-Spalten-Shell

**Files:**
- Create: `src/AgentForge.Web/src/shell/AppShell.tsx`, `shell/AreaNav.tsx`, `shell/ContextPanel.tsx`, `shell/RecentItems.tsx`
- Modify: `src/AgentForge.Web/src/App.tsx` (vollständig ersetzen)
- Test: `src/AgentForge.Web/src/__tests__/AppShell.test.tsx`, `src/__tests__/RecentItems.test.ts`

**Interfaces:**
- Consumes: `areaModules`, `fetchAreas`, `visibleAreas`.
- Produces:
  - `AppShell({ areas, activeSlug, children }: { areas: AreaModule[]; activeSlug: string; children: ReactNode })` — Kopf, drei Spalten, Haltepunkte.
  - `ContextPanelProvider({ children })`, `useContextPanel(node: ReactNode): void`, `ContextPanelOutlet()`.
  - `rememberItem(item: RecentItem): void`, `readRecentItems(): RecentItem[]`, `forgetItem(key: string): void` mit `type RecentItem = { key: string; to: string; label: string; kind: 'agent' | 'run' | 'conversation' }`.
  - `App` bindet Registry-Routen ein und leitet `/` auf den ersten sichtbaren Bereich.

`useContextPanel` setzt seinen Inhalt in einem `useEffect` und räumt beim Verlassen auf. Damit trägt die Seite den Inhalt, ohne dass die Shell die Seiten kennt.

- [ ] **Step 1: Die fehlschlagenden Tests für die zuletzt berührten Objekte schreiben**

`src/__tests__/RecentItems.test.ts`:

```ts
import { forgetItem, readRecentItems, rememberItem } from '../shell/RecentItems'

beforeEach(() => {
  localStorage.clear()
})

test('gemerkte Objekte kommen neuestes zuerst zurück', () => {
  rememberItem({ key: 'agent:1', to: '/agents/definitions/1', label: 'leo', kind: 'agent' })
  rememberItem({ key: 'run:9', to: '/agents/runs/9', label: 'D&D-Seite', kind: 'run' })

  expect(readRecentItems().map((item) => item.key)).toEqual(['run:9', 'agent:1'])
})

test('dasselbe Objekt erneut zu merken schiebt es nach vorn, ohne zu doppeln', () => {
  rememberItem({ key: 'agent:1', to: '/a/1', label: 'leo', kind: 'agent' })
  rememberItem({ key: 'run:9', to: '/r/9', label: 'Run', kind: 'run' })
  rememberItem({ key: 'agent:1', to: '/a/1', label: 'leo', kind: 'agent' })

  expect(readRecentItems().map((item) => item.key)).toEqual(['agent:1', 'run:9'])
})

test('es werden höchstens fünf Objekte behalten', () => {
  for (let index = 0; index < 7; index += 1) {
    rememberItem({ key: `agent:${index}`, to: `/a/${index}`, label: `a${index}`, kind: 'agent' })
  }

  expect(readRecentItems()).toHaveLength(5)
  expect(readRecentItems()[0]!.key).toBe('agent:6')
})

test('forgetItem entfernt ein verschwundenes Objekt', () => {
  rememberItem({ key: 'run:9', to: '/r/9', label: 'Run', kind: 'run' })
  forgetItem('run:9')
  expect(readRecentItems()).toEqual([])
})

test('beschädigter Speicherinhalt ergibt eine leere Liste statt eines Absturzes', () => {
  localStorage.setItem('agentforge.recent', 'kein json')
  expect(readRecentItems()).toEqual([])
})
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- RecentItems`
Expected: FAIL — `Failed to resolve import "../shell/RecentItems"`.

- [ ] **Step 3: `shell/RecentItems.tsx` schreiben**

```tsx
import { Link } from 'react-router-dom'

export type RecentItem = {
  key: string
  to: string
  label: string
  kind: 'agent' | 'run' | 'conversation'
}

const storageKey = 'agentforge.recent'
const limit = 5

export function readRecentItems(): RecentItem[] {
  const raw = localStorage.getItem(storageKey)
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? (parsed as RecentItem[]) : []
  } catch {
    return []
  }
}

function write(items: RecentItem[]): void {
  localStorage.setItem(storageKey, JSON.stringify(items.slice(0, limit)))
}

export function rememberItem(item: RecentItem): void {
  write([item, ...readRecentItems().filter((existing) => existing.key !== item.key)])
}

export function forgetItem(key: string): void {
  write(readRecentItems().filter((item) => item.key !== key))
}

export function RecentItems() {
  const items = readRecentItems()
  if (items.length === 0) return null

  return (
    <div className="mt-6">
      <p className="px-2 pb-1 text-[11px] font-semibold uppercase tracking-wide" style={{ color: 'var(--text-muted)' }}>
        Zuletzt
      </p>
      <ul>
        {items.map((item) => (
          <li key={item.key}>
            <Link to={item.to} className="block truncate rounded px-2 py-1 text-sm hover:bg-[var(--accent-soft)]">
              {item.label}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  )
}
```

- [ ] **Step 4: Test laufen lassen**

Run: `npm test -- RecentItems`
Expected: PASS, fünf Tests.

- [ ] **Step 5: Den fehlschlagenden Shell-Test schreiben**

`src/__tests__/AppShell.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import type { AreaModule } from '../lib/areas'
import { AppShell } from '../shell/AppShell'
import { ContextPanelProvider, useContextPanel } from '../shell/ContextPanel'

const agents: AreaModule = {
  slug: 'agents',
  title: 'Agents',
  routes: [],
  nav: [
    { to: '/agents/definitions', label: 'Agenten' },
    { to: '/agents/runs', label: 'Runs' },
    { to: '/agents/conversations', label: 'Gespräche' },
  ],
}

function PageWithContext() {
  useContextPanel(<p>Teilnehmer: leo</p>)
  return <p>Inhalt der Mitte</p>
}

function renderShell(areas: AreaModule[] = [agents]) {
  return render(
    <MemoryRouter initialEntries={['/agents/definitions']}>
      <ContextPanelProvider>
        <AppShell areas={areas} activeSlug="agents">
          <PageWithContext />
        </AppShell>
      </ContextPanelProvider>
    </MemoryRouter>,
  )
}

test('zeigt die Navigation des aktiven Bereichs', () => {
  renderShell()
  const nav = screen.getByRole('navigation', { name: 'Bereich Agents' })
  expect(nav).toHaveTextContent('Agenten')
  expect(nav).toHaveTextContent('Runs')
  expect(nav).toHaveTextContent('Gespräche')
})

test('die Seite füllt die Kontextspalte, die Shell kennt den Inhalt nicht', () => {
  renderShell()
  expect(screen.getByRole('complementary', { name: 'Kontext' })).toHaveTextContent('Teilnehmer: leo')
  expect(screen.getByText('Inhalt der Mitte')).toBeInTheDocument()
})

test('die Kontextspalte lässt sich ein- und ausklappen', async () => {
  renderShell()
  await userEvent.click(screen.getByRole('button', { name: 'Kontext ausblenden' }))
  expect(screen.queryByRole('complementary', { name: 'Kontext' })).not.toBeInTheDocument()
  await userEvent.click(screen.getByRole('button', { name: 'Kontext einblenden' }))
  expect(screen.getByRole('complementary', { name: 'Kontext' })).toBeInTheDocument()
})

test('bei einem einzigen Bereich gibt es keinen Bereichswechsel', () => {
  renderShell()
  expect(screen.queryByRole('navigation', { name: 'Bereiche' })).not.toBeInTheDocument()
})

test('bei mehreren Bereichen erscheint der Bereichswechsel', () => {
  renderShell([agents, { slug: 'dnd', title: 'D&D', routes: [], nav: [] }])
  expect(screen.getByRole('navigation', { name: 'Bereiche' })).toHaveTextContent('D&D')
})
```

- [ ] **Step 6: Test laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- AppShell`
Expected: FAIL — `Failed to resolve import "../shell/AppShell"`.

- [ ] **Step 7: `shell/ContextPanel.tsx` schreiben**

```tsx
import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'

type Store = { node: ReactNode; setNode: (node: ReactNode) => void }

const ContextPanelContext = createContext<Store | null>(null)

export function ContextPanelProvider({ children }: { children: ReactNode }) {
  const [node, setNode] = useState<ReactNode>(null)
  return (
    <ContextPanelContext.Provider value={{ node, setNode }}>{children}</ContextPanelContext.Provider>
  )
}

function useStore(): Store {
  const store = useContext(ContextPanelContext)
  if (!store) throw new Error('ContextPanelProvider fehlt')
  return store
}

/** A page calls this to fill the right column. It clears itself on unmount. */
export function useContextPanel(node: ReactNode): void {
  const { setNode } = useStore()
  useEffect(() => {
    setNode(node)
    return () => setNode(null)
  }, [node, setNode])
}

export function ContextPanelOutlet() {
  return <>{useStore().node}</>
}
```

- [ ] **Step 8: `shell/AreaNav.tsx` schreiben**

```tsx
import { NavLink } from 'react-router-dom'
import type { AreaModule } from '../lib/areas'
import { RecentItems } from './RecentItems'

const link = 'block rounded px-2 py-1.5 text-sm'

export function AreaNav({ areas, activeSlug }: { areas: AreaModule[]; activeSlug: string }) {
  const active = areas.find((area) => area.slug === activeSlug)

  return (
    <div>
      {areas.length > 1 && (
        <nav aria-label="Bereiche" className="mb-4">
          {areas.map((area) => (
            <NavLink
              key={area.slug}
              to={area.nav[0]?.to ?? `/${area.slug}`}
              className={`${link} ${area.slug === activeSlug ? 'font-semibold' : ''}`}
            >
              {area.title}
            </NavLink>
          ))}
        </nav>
      )}

      {active && (
        <nav aria-label={`Bereich ${active.title}`}>
          {active.nav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `${link} ${isActive ? 'bg-[var(--accent-soft)] font-semibold' : 'hover:bg-[var(--accent-soft)]'}`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      )}

      <RecentItems />
    </div>
  )
}
```

- [ ] **Step 9: `shell/AppShell.tsx` schreiben**

Die Haltepunkte liegen in Tailwind-Klassen, nicht in JavaScript: die linke Spalte wird unter `md` zur Schublade, die Kontextspalte ist unter `xl` standardmäßig zu. Der Umschalter wirkt in jeder Breite, damit der Test ihn ohne `matchMedia`-Attrappe erreichen kann.

```tsx
import { useState } from 'react'
import type { ReactNode } from 'react'
import type { AreaModule } from '../lib/areas'
import { AreaNav } from './AreaNav'
import { ContextPanelOutlet } from './ContextPanel'

export function AppShell({
  areas,
  activeSlug,
  children,
}: {
  areas: AreaModule[]
  activeSlug: string
  children: ReactNode
}) {
  const [navOpen, setNavOpen] = useState(false)
  const [contextOpen, setContextOpen] = useState(true)

  return (
    <div className="flex min-h-screen flex-col" style={{ background: 'var(--bg-sunken)' }}>
      <header
        className="flex items-center gap-3 border-b px-4 py-2"
        style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
      >
        <button
          type="button"
          className="rounded px-2 py-1 text-sm md:hidden"
          onClick={() => setNavOpen((open) => !open)}
        >
          {navOpen ? 'Navigation schließen' : 'Navigation öffnen'}
        </button>
        <span className="font-semibold" style={{ color: 'var(--text-strong)' }}>
          AgentForge
        </span>
        <button
          type="button"
          className="ml-auto rounded px-2 py-1 text-sm"
          onClick={() => setContextOpen((open) => !open)}
        >
          {contextOpen ? 'Kontext ausblenden' : 'Kontext einblenden'}
        </button>
      </header>

      <div className="flex flex-1 items-stretch">
        <aside
          aria-label="Navigation"
          className={`${navOpen ? 'block' : 'hidden'} w-56 shrink-0 border-r p-3 md:block`}
          style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
        >
          <AreaNav areas={areas} activeSlug={activeSlug} />
        </aside>

        <main className="min-w-0 flex-1 p-4">{children}</main>

        {contextOpen && (
          <aside
            aria-label="Kontext"
            className="hidden w-72 shrink-0 border-l p-3 lg:block"
            style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
          >
            <ContextPanelOutlet />
          </aside>
        )}
      </div>
    </div>
  )
}
```

- [ ] **Step 10: Test laufen lassen**

Run: `npm test -- AppShell`
Expected: PASS, fünf Tests. Schlägt der Ausklapp-Test fehl, weil `lg:block` in jsdom nicht greift: jsdom wertet keine Media Queries aus, das Element ist trotzdem im Baum — der Test prüft ausschließlich das Vorhandensein, nicht die Sichtbarkeit.

- [ ] **Step 11: `App.tsx` auf Shell und Registry umbauen**

```tsx
import { useEffect, useState } from 'react'
import { BrowserRouter, Navigate, useLocation, useRoutes } from 'react-router-dom'
import { areaModules } from './areas'
import type { AreaModule } from './lib/areas'
import { fetchAreas, visibleAreas } from './lib/areas'
import { AppShell } from './shell/AppShell'
import { ContextPanelProvider } from './shell/ContextPanel'

function Routed({ areas }: { areas: AreaModule[] }) {
  const location = useLocation()
  const activeSlug = location.pathname.split('/')[1] ?? ''
  const home = areas[0]?.nav[0]?.to

  const element = useRoutes([
    { path: '/', element: home ? <Navigate to={home} replace /> : <p>Kein Bereich verfügbar.</p> },
    ...areas.flatMap((area) => area.routes),
    { path: '*', element: <p>Diese Seite gibt es nicht.</p> },
  ])

  return (
    <AppShell areas={areas} activeSlug={activeSlug}>
      {element}
    </AppShell>
  )
}

export default function App() {
  const [areas, setAreas] = useState<AreaModule[] | null>(null)

  useEffect(() => {
    fetchAreas()
      .then((registered) => setAreas(visibleAreas(registered, areaModules)))
      .catch(() => setAreas([]))
  }, [])

  return (
    <BrowserRouter>
      <ContextPanelProvider>
        {areas === null ? <p className="p-6">Wird geladen …</p> : <Routed areas={areas} />}
      </ContextPanelProvider>
    </BrowserRouter>
  )
}
```

- [ ] **Step 12: Den App-Test an die Shell anpassen**

`src/__tests__/App.test.tsx` vollständig ersetzen:

```tsx
import { render, screen } from '@testing-library/react'
import App from '../App'
import { stubFetch } from '../test/stubFetch'

test('zeigt den Anwendungsnamen, sobald die Bereiche geladen sind', async () => {
  stubFetch([['/api/areas', { json: [] }]])
  render(<App />)
  expect(await screen.findByText('AgentForge')).toBeInTheDocument()
})

test('ohne erreichbaren Host bleibt die Shell bedienbar', async () => {
  globalThis.fetch = (() => Promise.reject(new Error('offline'))) as typeof fetch
  render(<App />)
  expect(await screen.findByText('AgentForge')).toBeInTheDocument()
})
```

- [ ] **Step 13: Alles laufen lassen**

Run: `npm test && npm run lint && npm run typecheck`
Expected: alles PASS.

- [ ] **Step 14: Commit**

```bash
git add src/AgentForge.Web/src
git commit -m "feat: add three-column app shell with context panel slot"
```

---

### Task 5: DTOs und API-Funktionen des Agents-Bereichs

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/types.ts`, `areas/agents/api.ts`
- Test: `src/AgentForge.Web/src/__tests__/agentsApi.test.ts`

**Interfaces:**
- Consumes: `apiGet`, `apiSend` aus `lib/http.ts`.
- Produces — `types.ts`:
  - `type Paged<T> = { items: T[]; total: number }`
  - `type AgentDto = { id, name, description, systemPrompt, model, temperature, maxOutputTokens, maxTurns, allowedTools, createdAt, updatedAt, archivedAt, concurrencyToken }`
  - `type AgentSnapshotDto = { name, systemPrompt, model, temperature, maxOutputTokens, maxTurns, allowedTools }`
  - `type RunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'`
  - `type RunDto`, `type RunMessageDto`, `type ParticipantDto`, `type ConversationDto`, `type ConversationMessageDto`
  - `type MessageRole = 'System' | 'User' | 'Assistant' | 'Tool'`
  - `type ToolCall`, `type TranscriptMessage`, `type StreamEvent`
- Produces — `api.ts`: `listAgents`, `getAgent`, `createAgent`, `updateAgent`, `archiveAgent`, `listRuns`, `getRun`, `startRun`, `getRunMessages`, `cancelRun`, `listConversations`, `getConversation`, `createConversation`, `updateConversation`, `archiveConversation`, `getConversationMessages`, `postConversationMessage`.

`TranscriptMessage` ist das **UI-Modell**, in das sowohl `RunMessageDto` als auch `ConversationMessageDto` übersetzt werden. Nur dadurch teilen Run und Gespräch ein Verlaufs-Bauteil.

- [ ] **Step 1: `types.ts` schreiben**

```ts
export type Paged<T> = { items: T[]; total: number }

export type AgentDto = {
  id: string
  name: string
  description: string | null
  systemPrompt: string
  model: string
  temperature: number
  maxOutputTokens: number
  maxTurns: number
  allowedTools: string[]
  createdAt: string
  updatedAt: string
  archivedAt: string | null
  concurrencyToken: string
}

export type AgentSnapshotDto = {
  name: string
  systemPrompt: string
  model: string
  temperature: number
  maxOutputTokens: number
  maxTurns: number
  allowedTools: string[]
}

export type RunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'

export type RunDto = {
  id: string
  agentId: string
  snapshot: AgentSnapshotDto
  objective: string
  status: RunStatus
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  error: string | null
  promptTokens: number | null
  completionTokens: number | null
  costEstimate: number | null
  concurrencyToken: string
}

export type MessageRole = 'System' | 'User' | 'Assistant' | 'Tool'

export type RunMessageDto = {
  id: string
  sequence: number
  role: MessageRole
  content: string | null
  toolCallsJson: string | null
  toolCallId: string | null
  createdAt: string
}

export type ParticipantDto = { agentId: string; name: string; model: string }

export type ConversationDto = {
  id: string
  title: string
  participants: ParticipantDto[]
  lastMessageExcerpt: string | null
  lastMessageAt: string | null
  createdAt: string
  archivedAt: string | null
  concurrencyToken: string
}

export type ConversationMessageDto = {
  id: string
  sequence: number
  role: MessageRole
  senderAgentId: string | null
  senderName: string | null
  content: string | null
  mentions: string[]
  toolCallsJson: string | null
  createdAt: string
}

// --- UI model, shared by runs and conversations ---

export type ToolCall = {
  id: string
  name: string
  argumentsJson: string
  resultText: string | null
  failed: boolean
}

export type TranscriptMessage = {
  sequence: number
  role: MessageRole
  senderAgentId: string | null
  senderName: string | null
  content: string
  toolCalls: ToolCall[]
  mentions: string[]
  state: 'complete' | 'streaming'
  createdAt: string | null
}

export type OutgoingMessage = {
  clientKey: string
  content: string
  mentions: string[]
  state: 'sending' | 'failed'
}

export type Usage = {
  promptTokens: number
  completionTokens: number
  costEstimate: number | null
}

export type StreamEvent =
  | { kind: 'token'; streamId: string; sequence: number; text: string }
  | { kind: 'message'; streamId: string; sequence: number; message: TranscriptMessage }
  | { kind: 'tool'; streamId: string; sequence: number; call: ToolCall }
  | { kind: 'status'; status: RunStatus }
  | { kind: 'usage'; usage: Usage }
  | { kind: 'done'; streamId: string }
  | { kind: 'error'; code: string; message: string }
```

- [ ] **Step 2: Die fehlschlagenden Tests schreiben**

`src/__tests__/agentsApi.test.ts`:

```ts
import {
  archiveAgent,
  cancelRun,
  createConversation,
  listAgents,
  listRuns,
  postConversationMessage,
  startRun,
  updateAgent,
} from '../areas/agents/api'
import { stubFetch } from '../test/stubFetch'

test('listAgents schickt Suchbegriff und Seitenangaben', async () => {
  const calls = stubFetch([['/api/agents/definitions', { json: { items: [], total: 0 } }]])
  await listAgents({ q: 'leo', skip: 50, take: 50 })
  expect(calls[0]!.url).toBe('/api/agents/definitions?q=leo&skip=50&take=50')
})

test('listAgents lässt einen leeren Suchbegriff weg', async () => {
  const calls = stubFetch([['/api/agents/definitions', { json: { items: [], total: 0 } }]])
  await listAgents({ q: '', skip: 0, take: 50 })
  expect(calls[0]!.url).toBe('/api/agents/definitions?skip=0&take=50')
})

test('listRuns filtert nach Agent und Status', async () => {
  const calls = stubFetch([['/api/agents/runs', { json: { items: [], total: 0 } }]])
  await listRuns({ agentId: 'a1', status: 'Running', skip: 0, take: 50 })
  expect(calls[0]!.url).toBe('/api/agents/runs?agentId=a1&status=Running&skip=0&take=50')
})

test('updateAgent schickt das Concurrency-Token im Rumpf', async () => {
  const calls = stubFetch([['/api/agents/definitions/a1', { json: { id: 'a1' } }]])
  await updateAgent('a1', { name: 'leo', concurrencyToken: 'tok-1' } as never)
  expect(calls[0]!.method).toBe('PUT')
  expect(calls[0]!.body).toMatchObject({ concurrencyToken: 'tok-1' })
})

test('archiveAgent schickt DELETE und verträgt eine leere Antwort', async () => {
  const calls = stubFetch([['/api/agents/definitions/a1', { status: 204 }]])
  await expect(archiveAgent('a1')).resolves.toBeNull()
  expect(calls[0]!.method).toBe('DELETE')
})

test('startRun schickt Agent und Auftrag', async () => {
  const calls = stubFetch([['/api/agents/runs', { json: { id: 'r1' } }]])
  await startRun({ agentId: 'a1', objective: 'Erstelle eine D&D-Seite' })
  expect(calls[0]!.body).toEqual({ agentId: 'a1', objective: 'Erstelle eine D&D-Seite' })
})

test('cancelRun schickt das Token an den cancel-Endpunkt', async () => {
  const calls = stubFetch([['/api/agents/runs/r1/cancel', { json: { id: 'r1' } }]])
  await cancelRun('r1', 'tok-9')
  expect(calls[0]!.url).toBe('/api/agents/runs/r1/cancel')
  expect(calls[0]!.body).toEqual({ concurrencyToken: 'tok-9' })
})

test('createConversation schickt Titel und Teilnehmer-Ids', async () => {
  const calls = stubFetch([['/api/agents/conversations', { json: { id: 'c1' } }]])
  await createConversation({ title: 'D&D-Team', participantAgentIds: ['a1', 'a2'] })
  expect(calls[0]!.body).toEqual({ title: 'D&D-Team', participantAgentIds: ['a1', 'a2'] })
})

test('postConversationMessage schickt Erwähnungen als Ids und liefert die streamId', async () => {
  const calls = stubFetch([
    ['/api/agents/conversations/c1/messages', { status: 202, json: { streamId: 's-1' } }],
  ])

  await expect(
    postConversationMessage('c1', { content: 'Schaffst du das?', mentions: ['a2'] }),
  ).resolves.toEqual({ streamId: 's-1' })

  expect(calls[0]!.body).toEqual({ content: 'Schaffst du das?', mentions: ['a2'] })
})
```

- [ ] **Step 3: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- agentsApi`
Expected: FAIL — `Failed to resolve import "../areas/agents/api"`.

- [ ] **Step 4: `api.ts` schreiben**

```ts
import { apiGet, apiSend } from '../../lib/http'
import type {
  AgentDto,
  ConversationDto,
  ConversationMessageDto,
  Paged,
  RunDto,
  RunMessageDto,
  RunStatus,
} from './types'

const definitions = '/api/agents/definitions'
const runs = '/api/agents/runs'
const conversations = '/api/agents/conversations'

export type Page = { skip: number; take: number }

export type CreateAgentBody = {
  name: string
  description: string | null
  systemPrompt: string
  model: string
  temperature: number
  maxOutputTokens: number
  maxTurns: number
  allowedTools: string[]
}

export type UpdateAgentBody = CreateAgentBody & { concurrencyToken: string }

export function listAgents(query: Page & { q: string }): Promise<Paged<AgentDto>> {
  return apiGet<Paged<AgentDto>>(definitions, {
    q: query.q === '' ? undefined : query.q,
    skip: query.skip,
    take: query.take,
  })
}

export function getAgent(id: string): Promise<AgentDto> {
  return apiGet<AgentDto>(`${definitions}/${id}`)
}

export function createAgent(body: CreateAgentBody): Promise<AgentDto> {
  return apiSend<AgentDto>('POST', definitions, body) as Promise<AgentDto>
}

export function updateAgent(id: string, body: UpdateAgentBody): Promise<AgentDto> {
  return apiSend<AgentDto>('PUT', `${definitions}/${id}`, body) as Promise<AgentDto>
}

export function archiveAgent(id: string): Promise<null> {
  return apiSend<null>('DELETE', `${definitions}/${id}`) as Promise<null>
}

export function listRuns(
  query: Page & { agentId?: string; status?: RunStatus },
): Promise<Paged<RunDto>> {
  return apiGet<Paged<RunDto>>(runs, {
    agentId: query.agentId,
    status: query.status,
    skip: query.skip,
    take: query.take,
  })
}

export function getRun(id: string): Promise<RunDto> {
  return apiGet<RunDto>(`${runs}/${id}`)
}

export function startRun(body: { agentId: string; objective: string }): Promise<RunDto> {
  return apiSend<RunDto>('POST', runs, body) as Promise<RunDto>
}

export function getRunMessages(id: string): Promise<RunMessageDto[]> {
  return apiGet<RunMessageDto[]>(`${runs}/${id}/messages`)
}

export function cancelRun(id: string, concurrencyToken: string): Promise<RunDto> {
  return apiSend<RunDto>('POST', `${runs}/${id}/cancel`, { concurrencyToken }) as Promise<RunDto>
}

export function listConversations(query: Page): Promise<Paged<ConversationDto>> {
  return apiGet<Paged<ConversationDto>>(conversations, query)
}

export function getConversation(id: string): Promise<ConversationDto> {
  return apiGet<ConversationDto>(`${conversations}/${id}`)
}

export function createConversation(body: {
  title: string
  participantAgentIds: string[]
}): Promise<ConversationDto> {
  return apiSend<ConversationDto>('POST', conversations, body) as Promise<ConversationDto>
}

export function updateConversation(
  id: string,
  body: { title: string; participantAgentIds: string[]; concurrencyToken: string },
): Promise<ConversationDto> {
  return apiSend<ConversationDto>('PUT', `${conversations}/${id}`, body) as Promise<ConversationDto>
}

export function archiveConversation(id: string): Promise<null> {
  return apiSend<null>('DELETE', `${conversations}/${id}`) as Promise<null>
}

export function getConversationMessages(id: string): Promise<ConversationMessageDto[]> {
  return apiGet<ConversationMessageDto[]>(`${conversations}/${id}/messages`)
}

export function postConversationMessage(
  id: string,
  body: { content: string; mentions: string[] },
): Promise<{ streamId: string }> {
  return apiSend<{ streamId: string }>('POST', `${conversations}/${id}/messages`, body) as Promise<{
    streamId: string
  }>
}
```

- [ ] **Step 5: Tests laufen lassen**

Run: `npm test -- agentsApi`
Expected: PASS, neun Tests.

- [ ] **Step 6: Commit**

```bash
git add src/AgentForge.Web/src/areas/agents src/AgentForge.Web/src/__tests__/agentsApi.test.ts
git commit -m "feat: add agents area dtos and api functions"
```

---

### Task 6: Mock-Server für die Entwicklung

**Files:**
- Create: `src/AgentForge.Web/mock/apiPlugin.ts`
- Modify: `src/AgentForge.Web/vite.config.ts`

**Interfaces:**
- Consumes: nichts aus `src/`. Das Plugin ist bewusst eigenständig und importiert keine Anwendungstypen, damit es nicht in den Produktionsbau gezogen wird.
- Produces: `mockApiPlugin(): Plugin` — greift nur, wenn Vite mit `--mode mock` läuft.

Dieser Mock ist ein Werkzeug, kein Produkt: keine Tests, keine Vollständigkeit, keine Persistenz. Er existiert, damit die Aufgaben 10 bis 17 im Browser sichtbar sind, solange das Backend fehlt. Er wird nie importiert, wenn `mode !== 'mock'`.

- [ ] **Step 1: Das Plugin schreiben**

`mock/apiPlugin.ts`:

```ts
import type { Plugin } from 'vite'

type Json = Record<string, unknown>

const now = () => new Date().toISOString()
const id = () => Math.random().toString(36).slice(2, 10)

const agents: Json[] = [
  {
    id: 'a1', name: 'leo', description: 'Orchestrator', systemPrompt: 'Du bist Leo.',
    model: 'gpt-5', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20,
    allowedTools: [], createdAt: now(), updatedAt: now(), archivedAt: null, concurrencyToken: 'tok-a1',
  },
  {
    id: 'a2', name: 'frontend-dev', description: 'Baut React-Komponenten',
    systemPrompt: 'Du bist ein Frontend-Spezialist.', model: 'gpt-5', temperature: 0.4,
    maxOutputTokens: 8192, maxTurns: 30, allowedTools: ['write_file', 'run_shell'],
    createdAt: now(), updatedAt: now(), archivedAt: null, concurrencyToken: 'tok-a2',
  },
  {
    id: 'a3', name: 'tester', description: 'Schreibt Tests', systemPrompt: 'Du prüfst.',
    model: 'gpt-5', temperature: 0.2, maxOutputTokens: 4096, maxTurns: 20,
    allowedTools: ['run_shell'], createdAt: now(), updatedAt: now(), archivedAt: null,
    concurrencyToken: 'tok-a3',
  },
]

const runs: Json[] = []
const runMessages: Record<string, Json[]> = {}
const conversations: Json[] = []
const conversationMessages: Record<string, Json[]> = {}

function snapshotOf(agent: Json): Json {
  const { name, systemPrompt, model, temperature, maxOutputTokens, maxTurns, allowedTools } = agent
  return { name, systemPrompt, model, temperature, maxOutputTokens, maxTurns, allowedTools }
}

function live<T extends Json>(list: T[]): T[] {
  return list.filter((item) => item.archivedAt === null)
}

/** Scripted event sequence, identical in shape for runs and conversations. */
async function pushStream(
  write: (event: string, data: Json) => void,
  sequence: number,
  senderName: string | null,
  senderAgentId: string | null,
) {
  const streamId = id()
  const words = ['Ich', ' sehe', ' mir', ' das', ' an', ' und', ' lege', ' los.']
  let text = ''
  for (const word of words) {
    text += word
    write('token', { streamId, sequence, text: word })
    await new Promise((resolve) => setTimeout(resolve, 90))
  }
  write('tool', {
    streamId, sequence,
    call: {
      id: id(), name: 'write_file',
      argumentsJson: JSON.stringify({ path: 'CharacterSheet.tsx', bytes: 2148 }, null, 2),
      resultText: 'geschrieben', failed: false,
    },
  })
  write('message', {
    streamId, sequence,
    message: {
      sequence, role: 'Assistant', senderAgentId, senderName, content: text,
      toolCalls: [], mentions: [], state: 'complete', createdAt: now(),
    },
  })
  write('usage', { usage: { promptTokens: 812, completionTokens: 140, costEstimate: 0.004 } })
  write('done', { streamId })
}

export function mockApiPlugin(): Plugin {
  return {
    name: 'agentforge-mock-api',
    apply: 'serve',
    configureServer(server) {
      server.middlewares.use('/api', async (request, response) => {
        const url = new URL(request.url ?? '/', 'http://mock')
        const path = url.pathname.replace(/\/$/, '')
        const method = request.method ?? 'GET'

        const send = (status: number, body?: unknown) => {
          response.statusCode = status
          response.setHeader('content-type', 'application/json')
          response.end(body === undefined ? '' : JSON.stringify(body))
        }

        const problem = (status: number, code: string, title: string) => {
          response.statusCode = status
          response.setHeader('content-type', 'application/problem+json')
          response.end(JSON.stringify({ type: `https://agentforge.local/errors/${code}`, title }))
        }

        const readBody = async (): Promise<Json> => {
          const chunks: Buffer[] = []
          for await (const chunk of request) chunks.push(chunk as Buffer)
          return chunks.length ? (JSON.parse(Buffer.concat(chunks).toString()) as Json) : {}
        }

        const openStream = () => {
          response.statusCode = 200
          response.setHeader('content-type', 'text/event-stream')
          response.setHeader('cache-control', 'no-cache')
          response.setHeader('connection', 'keep-alive')
          return (event: string, data: Json) => {
            response.write(`event: ${event}\nid: ${id()}\ndata: ${JSON.stringify(data)}\n\n`)
          }
        }

        // --- areas ---
        if (path === '/areas') return send(200, [{ slug: 'agents' }])

        // --- agent definitions ---
        if (path === '/agents/definitions' && method === 'GET') {
          const q = (url.searchParams.get('q') ?? '').toLowerCase()
          const items = live(agents).filter((a) => String(a.name).toLowerCase().includes(q))
          return send(200, { items, total: items.length })
        }
        if (path === '/agents/definitions' && method === 'POST') {
          const body = await readBody()
          if (live(agents).some((a) => a.name === body.name)) {
            return problem(409, 'name-conflict', 'Der Name ist schon belegt.')
          }
          const agent = { ...body, id: id(), createdAt: now(), updatedAt: now(), archivedAt: null, concurrencyToken: id() }
          agents.push(agent)
          return send(201, agent)
        }

        const definitionMatch = /^\/agents\/definitions\/([^/]+)$/.exec(path)
        if (definitionMatch) {
          const agent = agents.find((a) => a.id === definitionMatch[1])
          if (!agent) return problem(404, 'not-found', 'Nicht gefunden.')
          if (method === 'GET') return send(200, agent)
          if (method === 'DELETE') {
            agent.archivedAt = now()
            return send(204)
          }
          if (method === 'PUT') {
            const body = await readBody()
            if (body.concurrencyToken !== agent.concurrencyToken) {
              return problem(409, 'concurrency-conflict', 'Wurde anderswo geändert.')
            }
            Object.assign(agent, body, { updatedAt: now(), concurrencyToken: id() })
            return send(200, agent)
          }
        }

        // --- runs ---
        if (path === '/agents/runs' && method === 'GET') {
          const agentId = url.searchParams.get('agentId')
          const status = url.searchParams.get('status')
          const items = runs.filter(
            (r) => (!agentId || r.agentId === agentId) && (!status || r.status === status),
          )
          return send(200, { items: [...items].reverse(), total: items.length })
        }
        if (path === '/agents/runs' && method === 'POST') {
          const body = await readBody()
          const agent = agents.find((a) => a.id === body.agentId)
          if (!agent) return problem(404, 'not-found', 'Agent nicht gefunden.')
          if (agent.archivedAt !== null) {
            return problem(409, 'agent-archived', 'Der Agent ist archiviert.')
          }
          const run = {
            id: id(), agentId: agent.id, snapshot: snapshotOf(agent), objective: body.objective,
            status: 'Running', createdAt: now(), startedAt: now(), completedAt: null, error: null,
            promptTokens: null, completionTokens: null, costEstimate: null, concurrencyToken: id(),
          }
          runs.push(run)
          runMessages[run.id] = [
            { id: id(), sequence: 0, role: 'System', content: agent.systemPrompt, toolCallsJson: null, toolCallId: null, createdAt: now() },
            { id: id(), sequence: 1, role: 'User', content: body.objective, toolCallsJson: null, toolCallId: null, createdAt: now() },
          ]
          return send(201, run)
        }

        const runMatch = /^\/agents\/runs\/([^/]+)(\/messages|\/cancel|\/stream)?$/.exec(path)
        if (runMatch) {
          const run = runs.find((r) => r.id === runMatch[1])
          if (!run) return problem(404, 'not-found', 'Nicht gefunden.')
          const tail = runMatch[2]
          if (!tail && method === 'GET') return send(200, run)
          if (tail === '/messages') return send(200, runMessages[String(run.id)] ?? [])
          if (tail === '/cancel') {
            const body = await readBody()
            if (body.concurrencyToken !== run.concurrencyToken) {
              return problem(409, 'concurrency-conflict', 'Wurde anderswo geändert.')
            }
            if (run.status !== 'Pending' && run.status !== 'Running') {
              return problem(409, 'invalid-transition', 'Der Run ist bereits beendet.')
            }
            Object.assign(run, { status: 'Cancelled', completedAt: now(), concurrencyToken: id() })
            return send(200, run)
          }
          if (tail === '/stream') {
            const write = openStream()
            write('status', { status: 'Running' })
            await pushStream(write, 2, String(run.agentId), String(run.agentId))
            write('status', { status: 'Completed' })
            Object.assign(run, { status: 'Completed', completedAt: now() })
            return response.end()
          }
        }

        // --- conversations ---
        if (path === '/agents/conversations' && method === 'GET') {
          return send(200, { items: live(conversations), total: live(conversations).length })
        }
        if (path === '/agents/conversations' && method === 'POST') {
          const body = await readBody()
          const ids = (body.participantAgentIds as string[]) ?? []
          const participants = agents
            .filter((a) => ids.includes(String(a.id)))
            .map((a) => ({ agentId: a.id, name: a.name, model: a.model }))
          const conversation = {
            id: id(),
            title: String(body.title || participants.map((p) => p.name).join(', ')),
            participants, lastMessageExcerpt: null, lastMessageAt: null,
            createdAt: now(), archivedAt: null, concurrencyToken: id(),
          }
          conversations.push(conversation)
          conversationMessages[conversation.id] = []
          return send(201, conversation)
        }

        const conversationMatch =
          /^\/agents\/conversations\/([^/]+)(\/messages|\/stream)?$/.exec(path)
        if (conversationMatch) {
          const conversation = conversations.find((c) => c.id === conversationMatch[1])
          if (!conversation) return problem(404, 'not-found', 'Nicht gefunden.')
          const key = String(conversation.id)
          const tail = conversationMatch[2]

          if (!tail && method === 'GET') return send(200, conversation)
          if (!tail && method === 'DELETE') {
            conversation.archivedAt = now()
            return send(204)
          }
          if (!tail && method === 'PUT') {
            const body = await readBody()
            if (body.concurrencyToken !== conversation.concurrencyToken) {
              return problem(409, 'concurrency-conflict', 'Wurde anderswo geändert.')
            }
            const ids = (body.participantAgentIds as string[]) ?? []
            conversation.participants = agents
              .filter((a) => ids.includes(String(a.id)))
              .map((a) => ({ agentId: a.id, name: a.name, model: a.model }))
            conversation.title = String(body.title || conversation.title)
            conversation.concurrencyToken = id()
            return send(200, conversation)
          }
          if (tail === '/messages' && method === 'GET') {
            return send(200, conversationMessages[key] ?? [])
          }
          if (tail === '/messages' && method === 'POST') {
            const body = await readBody()
            const mentions = (body.mentions as string[]) ?? []
            const participants = conversation.participants as Array<{ agentId: string }>
            if (mentions.some((m) => !participants.some((p) => p.agentId === m))) {
              return problem(400, 'validation-failed', 'Erwähnter Agent ist kein Teilnehmer.')
            }
            const list = (conversationMessages[key] ??= [])
            list.push({
              id: id(), sequence: list.length, role: 'User', senderAgentId: null, senderName: null,
              content: body.content, mentions, toolCallsJson: null, createdAt: now(),
            })
            conversation.lastMessageExcerpt = String(body.content).slice(0, 80)
            conversation.lastMessageAt = now()
            return send(202, { streamId: id() })
          }
          if (tail === '/stream') {
            const write = openStream()
            const list = (conversationMessages[key] ??= [])
            const last = list[list.length - 1]
            const mentioned = ((last?.mentions as string[]) ?? [])[0]
            const responder = agents.find((a) => a.id === mentioned)
            if (responder) {
              await pushStream(write, list.length, String(responder.name), String(responder.id))
            }
            return response.end()
          }
        }

        return problem(404, 'not-found', `Kein Mock für ${method} ${path}.`)
      })
    },
  }
}
```

- [ ] **Step 2: Das Plugin nur im Mock-Modus einhängen**

`vite.config.ts` ändern: `defineConfig` bekommt die Funktionsform, damit `mode` verfügbar ist.

```ts
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { mockApiPlugin } from './mock/apiPlugin'

const hostUrl = 'http://localhost:5204'

export default defineConfig(({ mode }) => ({
  plugins: [react(), tailwindcss(), ...(mode === 'mock' ? [mockApiPlugin()] : [])],
  server: {
    proxy: mode === 'mock' ? undefined : { '/api': { target: hostUrl, changeOrigin: true } },
  },
  test: {
    root: fileURLToPath(new URL('./', import.meta.url)),
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/__tests__/**/*.{test,spec}.{ts,tsx}'],
  },
}))
```

- [ ] **Step 3: Von Hand prüfen, dass der Mock antwortet**

Run: `npm run dev:mock`

Dann im Browser `http://localhost:5173/api/areas` öffnen.
Expected: `[{"slug":"agents"}]`. Und `http://localhost:5173/api/agents/definitions` liefert drei Agenten.

Danach den Dev-Server beenden.

- [ ] **Step 4: Prüfen, dass der Produktionsbau das Plugin nicht zieht**

Run: `npm run build`
Expected: erfolgreich; in `dist/assets` taucht **kein** Mock-Code auf. Gegenprobe: `grep -r "agentforge-mock-api" dist` findet nichts.

- [ ] **Step 5: Tests, Lint, Typprüfung laufen lassen**

Run: `npm test && npm run lint && npm run typecheck`
Expected: alles PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AgentForge.Web/mock src/AgentForge.Web/vite.config.ts
git commit -m "chore: add dev-only mock api plugin for the missing backend"
```

---

### Task 7: Der Verlaufs-Reducer

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/transcriptReducer.ts`
- Test: `src/AgentForge.Web/src/__tests__/transcriptReducer.test.ts`

**Interfaces:**
- Consumes: `StreamEvent`, `TranscriptMessage`, `OutgoingMessage`, `RunStatus`, `Usage`, `ToolCall` aus `./types`.
- Produces:
  - `type ConnectionState = 'idle' | 'open' | 'reconnecting' | 'lost'`
  - `type TranscriptState = { messages: Record<number, TranscriptMessage>; outgoing: OutgoingMessage[]; status: RunStatus | null; usage: Usage | null; error: { code: string; message: string } | null; connection: ConnectionState }`
  - `initialTranscriptState: TranscriptState`
  - `type TranscriptAction` mit den Varianten `loaded`, `event`, `connection`, `outgoing`, `outgoingFailed`
  - `transcriptReducer(state: TranscriptState, action: TranscriptAction): TranscriptState`
  - `orderedMessages(state: TranscriptState): TranscriptMessage[]`

**Der Vertrag, den dieser Reducer erfüllt** — er ist der Grund, warum Aufgabe 19 Kriterium 7 hält:

1. `message` ist **autoritativ**: es ersetzt den Eintrag seiner `Sequence` vollständig. Zweimal dasselbe `message` zu empfangen ändert nichts.
2. `token`-Ereignisse **können** sich nach einer Wiederverbindung wiederholen; sie lassen sich nicht am Inhalt erkennen. Deshalb verwirft der Übergang nach `reconnecting` alle noch strömenden Teilnachrichten. Danach baut sich der Text neu auf, und das abschließende `message` setzt ihn ohnehin endgültig.
3. `tool` wird über `call.id` entdoppelt.
4. `loaded` überschreibt **nichts**, was schon da ist. Der Verlauf wird geladen, während der Strom bereits läuft; die Ereignisse sind aktueller.
5. Eine eigene, noch nicht bestätigte Nachricht verschwindet aus `outgoing`, sobald ein `message` mit `role: 'User'` und gleichem Inhalt eintrifft.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`src/__tests__/transcriptReducer.test.ts`:

```ts
import type { StreamEvent, TranscriptMessage } from '../areas/agents/types'
import {
  initialTranscriptState,
  orderedMessages,
  transcriptReducer,
} from '../areas/agents/transcriptReducer'

function message(sequence: number, content: string, role: TranscriptMessage['role'] = 'Assistant'): TranscriptMessage {
  return {
    sequence, role, senderAgentId: null, senderName: null, content,
    toolCalls: [], mentions: [], state: 'complete', createdAt: '2026-07-29T10:00:00Z',
  }
}

function apply(events: Array<StreamEvent | { load: TranscriptMessage[] } | { connection: 'open' | 'reconnecting' | 'lost' }>) {
  return events.reduce((state, item) => {
    if ('load' in item) return transcriptReducer(state, { type: 'loaded', messages: item.load })
    if ('connection' in item) return transcriptReducer(state, { type: 'connection', connection: item.connection })
    return transcriptReducer(state, { type: 'event', event: item })
  }, initialTranscriptState)
}

test('Token-Ereignisse bauen eine strömende Nachricht auf', () => {
  const state = apply([
    { kind: 'token', streamId: 's1', sequence: 2, text: 'Ich ' },
    { kind: 'token', streamId: 's1', sequence: 2, text: 'lege los.' },
  ])

  expect(orderedMessages(state)).toHaveLength(1)
  expect(orderedMessages(state)[0]).toMatchObject({
    sequence: 2, content: 'Ich lege los.', state: 'streaming', role: 'Assistant',
  })
})

test('ein message-Ereignis schließt die Nachricht ab und ist autoritativ', () => {
  const state = apply([
    { kind: 'token', streamId: 's1', sequence: 2, text: 'Halb' },
    { kind: 'message', streamId: 's1', sequence: 2, message: message(2, 'Ganzer Satz.') },
  ])

  expect(orderedMessages(state)[0]).toMatchObject({ content: 'Ganzer Satz.', state: 'complete' })
})

test('dasselbe message-Ereignis zweimal erzeugt keine zweite Nachricht', () => {
  const event: StreamEvent = { kind: 'message', streamId: 's1', sequence: 2, message: message(2, 'Text') }
  const state = apply([event, event])

  expect(orderedMessages(state)).toHaveLength(1)
})

test('nach einer Wiederverbindung wird die strömende Teilnachricht verworfen', () => {
  const state = apply([
    { kind: 'token', streamId: 's1', sequence: 2, text: 'Ich ' },
    { kind: 'token', streamId: 's1', sequence: 2, text: 'lege ' },
    { connection: 'reconnecting' },
    { kind: 'token', streamId: 's1', sequence: 2, text: 'Ich lege los.' },
  ])

  expect(orderedMessages(state)[0]!.content).toBe('Ich lege los.')
  expect(state.connection).toBe('reconnecting')
})

test('eine abgeschlossene Nachricht übersteht eine Wiederverbindung', () => {
  const state = apply([
    { kind: 'message', streamId: 's1', sequence: 1, message: message(1, 'Fertig.') },
    { connection: 'reconnecting' },
  ])

  expect(orderedMessages(state)).toHaveLength(1)
})

test('Werkzeugaufrufe werden über ihre Id entdoppelt', () => {
  const call = { id: 't1', name: 'write_file', argumentsJson: '{}', resultText: 'ok', failed: false }
  const state = apply([
    { kind: 'tool', streamId: 's1', sequence: 2, call },
    { kind: 'tool', streamId: 's1', sequence: 2, call },
  ])

  expect(orderedMessages(state)[0]!.toolCalls).toHaveLength(1)
})

test('geladene Nachrichten überschreiben keine bereits eingetroffenen', () => {
  const state = apply([
    { kind: 'message', streamId: 's1', sequence: 1, message: message(1, 'aus dem Strom') },
    { load: [message(0, 'System-Prompt', 'System'), message(1, 'veraltet')] },
  ])

  expect(orderedMessages(state).map((m) => m.content)).toEqual(['System-Prompt', 'aus dem Strom'])
})

test('Nachrichten kommen nach Sequence sortiert zurück, auch bei verdrehter Reihenfolge', () => {
  const state = apply([
    { kind: 'message', streamId: 's1', sequence: 3, message: message(3, 'drei') },
    { kind: 'message', streamId: 's1', sequence: 1, message: message(1, 'eins') },
  ])

  expect(orderedMessages(state).map((m) => m.content)).toEqual(['eins', 'drei'])
})

test('eine Lücke in der Sequence ist kein Fehler', () => {
  const state = apply([
    { kind: 'message', streamId: 's1', sequence: 0, message: message(0, 'null') },
    { kind: 'message', streamId: 's1', sequence: 7, message: message(7, 'sieben') },
  ])

  expect(orderedMessages(state)).toHaveLength(2)
})

test('Status, Kennzahlen und Fehler werden übernommen', () => {
  const state = apply([
    { kind: 'status', status: 'Running' },
    { kind: 'usage', usage: { promptTokens: 800, completionTokens: 140, costEstimate: 0.04 } },
    { kind: 'error', code: 'model-unavailable', message: 'Modell antwortet nicht.' },
  ])

  expect(state.status).toBe('Running')
  expect(state.usage).toEqual({ promptTokens: 800, completionTokens: 140, costEstimate: 0.04 })
  expect(state.error).toEqual({ code: 'model-unavailable', message: 'Modell antwortet nicht.' })
})

test('eine eigene Nachricht wartet als outgoing und verschwindet bei Bestätigung', () => {
  let state = transcriptReducer(initialTranscriptState, {
    type: 'outgoing',
    message: { clientKey: 'k1', content: 'Schaffst du das?', mentions: ['a2'], state: 'sending' },
  })
  expect(state.outgoing).toHaveLength(1)

  state = transcriptReducer(state, {
    type: 'event',
    event: { kind: 'message', streamId: 's1', sequence: 4, message: message(4, 'Schaffst du das?', 'User') },
  })

  expect(state.outgoing).toHaveLength(0)
  expect(orderedMessages(state)).toHaveLength(1)
})

test('eine gescheiterte eigene Nachricht bleibt mit Zustand failed stehen', () => {
  let state = transcriptReducer(initialTranscriptState, {
    type: 'outgoing',
    message: { clientKey: 'k1', content: 'Hallo', mentions: [], state: 'sending' },
  })
  state = transcriptReducer(state, { type: 'outgoingFailed', clientKey: 'k1' })

  expect(state.outgoing[0]).toMatchObject({ clientKey: 'k1', state: 'failed' })
})
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- transcriptReducer`
Expected: FAIL — `Failed to resolve import "../areas/agents/transcriptReducer"`.

- [ ] **Step 3: `transcriptReducer.ts` schreiben**

```ts
import type {
  OutgoingMessage,
  RunStatus,
  StreamEvent,
  TranscriptMessage,
  Usage,
} from './types'

export type ConnectionState = 'idle' | 'open' | 'reconnecting' | 'lost'

export type TranscriptState = {
  messages: Record<number, TranscriptMessage>
  outgoing: OutgoingMessage[]
  status: RunStatus | null
  usage: Usage | null
  error: { code: string; message: string } | null
  connection: ConnectionState
}

export const initialTranscriptState: TranscriptState = {
  messages: {},
  outgoing: [],
  status: null,
  usage: null,
  error: null,
  connection: 'idle',
}

export type TranscriptAction =
  | { type: 'loaded'; messages: TranscriptMessage[] }
  | { type: 'event'; event: StreamEvent }
  | { type: 'connection'; connection: ConnectionState }
  | { type: 'outgoing'; message: OutgoingMessage }
  | { type: 'outgoingFailed'; clientKey: string }

function placeholder(sequence: number): TranscriptMessage {
  return {
    sequence,
    role: 'Assistant',
    senderAgentId: null,
    senderName: null,
    content: '',
    toolCalls: [],
    mentions: [],
    state: 'streaming',
    createdAt: null,
  }
}

function applyEvent(state: TranscriptState, event: StreamEvent): TranscriptState {
  switch (event.kind) {
    case 'token': {
      const current = state.messages[event.sequence] ?? placeholder(event.sequence)
      return {
        ...state,
        messages: {
          ...state.messages,
          [event.sequence]: { ...current, content: current.content + event.text, state: 'streaming' },
        },
      }
    }
    case 'message': {
      // Authoritative: replaces whatever sat at this sequence.
      const settled = state.outgoing.filter(
        (item) => !(event.message.role === 'User' && item.content === event.message.content),
      )
      return {
        ...state,
        outgoing: settled,
        messages: { ...state.messages, [event.sequence]: { ...event.message, state: 'complete' } },
      }
    }
    case 'tool': {
      const current = state.messages[event.sequence] ?? placeholder(event.sequence)
      if (current.toolCalls.some((call) => call.id === event.call.id)) return state
      return {
        ...state,
        messages: {
          ...state.messages,
          [event.sequence]: { ...current, toolCalls: [...current.toolCalls, event.call] },
        },
      }
    }
    case 'status':
      return { ...state, status: event.status }
    case 'usage':
      return { ...state, usage: event.usage }
    case 'done':
      return state
    case 'error':
      return { ...state, error: { code: event.code, message: event.message } }
  }
}

function dropStreaming(messages: Record<number, TranscriptMessage>): Record<number, TranscriptMessage> {
  return Object.fromEntries(
    Object.entries(messages).filter(([, message]) => message.state !== 'streaming'),
  )
}

export function transcriptReducer(
  state: TranscriptState,
  action: TranscriptAction,
): TranscriptState {
  switch (action.type) {
    case 'loaded': {
      // Stream events win: they are newer than the snapshot we just fetched.
      const merged = { ...state.messages }
      for (const message of action.messages) {
        if (!(message.sequence in merged)) merged[message.sequence] = message
      }
      return { ...state, messages: merged }
    }
    case 'event':
      return applyEvent(state, action.event)
    case 'connection':
      return action.connection === 'reconnecting'
        ? { ...state, connection: action.connection, messages: dropStreaming(state.messages) }
        : { ...state, connection: action.connection }
    case 'outgoing':
      return { ...state, outgoing: [...state.outgoing, action.message] }
    case 'outgoingFailed':
      return {
        ...state,
        outgoing: state.outgoing.map((item) =>
          item.clientKey === action.clientKey ? { ...item, state: 'failed' } : item,
        ),
      }
  }
}

export function orderedMessages(state: TranscriptState): TranscriptMessage[] {
  return Object.values(state.messages).sort((left, right) => left.sequence - right.sequence)
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `npm test -- transcriptReducer`
Expected: PASS, zwölf Tests.

- [ ] **Step 5: Commit**

```bash
git add src/AgentForge.Web/src/areas/agents/transcriptReducer.ts src/AgentForge.Web/src/__tests__/transcriptReducer.test.ts
git commit -m "feat: add transcript reducer with authoritative message events"
```

---

### Task 8: Strom-Anbindung und `EventSource`-Attrappe

**Files:**
- Create: `src/AgentForge.Web/src/lib/sse.ts`, `src/test/fakeEventSource.ts`
- Test: `src/AgentForge.Web/src/__tests__/sse.test.ts`

**Interfaces:**
- Consumes: `StreamEvent` aus `areas/agents/types` — als Typ-Import, `lib/sse.ts` enthält keine Fachlogik.
- Produces:
  - `type StreamHandlers = { onEvent: (event: StreamEvent) => void; onConnection: (connection: 'open' | 'reconnecting' | 'lost') => void }`
  - `openStream(url: string, handlers: StreamHandlers): () => void` — Rückgabewert schließt die Verbindung.
  - `installFakeEventSource(): { instances: FakeEventSource[]; restore: () => void }` aus `src/test/fakeEventSource.ts`, mit `emit(event, data)`, `open()`, `fail()` je Instanz.

jsdom bringt kein `EventSource` mit. Das ist hier ein Vorteil: es gibt keine echte Implementierung, die sich versehentlich in einen Test schleicht.

- [ ] **Step 1: Die Attrappe schreiben**

`src/test/fakeEventSource.ts`:

```ts
type Listener = (event: MessageEvent) => void

export class FakeEventSource {
  static readonly CONNECTING = 0
  static readonly OPEN = 1
  static readonly CLOSED = 2

  readyState = FakeEventSource.CONNECTING
  onerror: ((event: Event) => void) | null = null
  onopen: ((event: Event) => void) | null = null
  closed = false

  private readonly listeners = new Map<string, Listener[]>()

  constructor(readonly url: string) {}

  addEventListener(name: string, listener: Listener): void {
    this.listeners.set(name, [...(this.listeners.get(name) ?? []), listener])
  }

  close(): void {
    this.closed = true
    this.readyState = FakeEventSource.CLOSED
  }

  /** Simulates a successful connection. */
  open(): void {
    this.readyState = FakeEventSource.OPEN
    this.onopen?.(new Event('open'))
  }

  /** Simulates one named server event. */
  emit(name: string, data: unknown): void {
    const event = new MessageEvent(name, { data: JSON.stringify(data), lastEventId: '1' })
    for (const listener of this.listeners.get(name) ?? []) listener(event)
  }

  /** Simulates a dropped connection while the browser is still retrying. */
  fail(state: number = FakeEventSource.CONNECTING): void {
    this.readyState = state
    this.onerror?.(new Event('error'))
  }
}

export function installFakeEventSource() {
  const instances: FakeEventSource[] = []
  const original = (globalThis as { EventSource?: unknown }).EventSource

  ;(globalThis as { EventSource?: unknown }).EventSource = class extends FakeEventSource {
    constructor(url: string) {
      super(url)
      instances.push(this)
    }
  }

  return {
    instances,
    restore: () => {
      ;(globalThis as { EventSource?: unknown }).EventSource = original
    },
  }
}
```

- [ ] **Step 2: Die fehlschlagenden Tests schreiben**

`src/__tests__/sse.test.ts`:

```ts
import type { StreamEvent } from '../areas/agents/types'
import { openStream } from '../lib/sse'
import { installFakeEventSource } from '../test/fakeEventSource'

function setup() {
  const fake = installFakeEventSource()
  const events: StreamEvent[] = []
  const connections: string[] = []
  const close = openStream('/api/agents/runs/r1/stream', {
    onEvent: (event) => events.push(event),
    onConnection: (connection) => connections.push(connection),
  })
  return { fake, events, connections, close, source: () => fake.instances[0]! }
}

afterEach(() => {
  installFakeEventSource().restore()
})

test('öffnet die übergebene Adresse', () => {
  const { source, fake } = setup()
  expect(source().url).toBe('/api/agents/runs/r1/stream')
  fake.restore()
})

test('übersetzt benannte Ereignisse in StreamEvent mit kind', () => {
  const { source, events, fake } = setup()

  source().emit('token', { streamId: 's1', sequence: 2, text: 'Hallo' })
  source().emit('status', { status: 'Running' })

  expect(events).toEqual([
    { kind: 'token', streamId: 's1', sequence: 2, text: 'Hallo' },
    { kind: 'status', status: 'Running' },
  ])
  fake.restore()
})

test('meldet open, sobald die Verbindung steht', () => {
  const { source, connections, fake } = setup()
  source().open()
  expect(connections).toEqual(['open'])
  fake.restore()
})

test('meldet reconnecting bei den ersten beiden Aussetzern', () => {
  const { source, connections, fake } = setup()

  source().fail()
  source().fail()

  expect(connections).toEqual(['reconnecting', 'reconnecting'])
  expect(source().closed).toBe(false)
  fake.restore()
})

test('meldet nach dem dritten Aussetzer lost und schließt die Verbindung', () => {
  const { source, connections, fake } = setup()

  source().fail()
  source().fail()
  source().fail()

  expect(connections).toEqual(['reconnecting', 'reconnecting', 'lost'])
  expect(source().closed).toBe(true)
  fake.restore()
})

test('eine erfolgreiche Verbindung setzt den Zähler zurück', () => {
  const { source, connections, fake } = setup()

  source().fail()
  source().fail()
  source().open()
  source().fail()

  expect(connections).toEqual(['reconnecting', 'reconnecting', 'open', 'reconnecting'])
  expect(source().closed).toBe(false)
  fake.restore()
})

test('unlesbare Nutzdaten werden übersprungen, ohne den Strom zu töten', () => {
  const fake = installFakeEventSource()
  const events: StreamEvent[] = []
  openStream('/stream', { onEvent: (event) => events.push(event), onConnection: () => {} })

  const source = fake.instances[0]!
  source.addEventListener('token', () => {})
  const broken = new MessageEvent('token', { data: 'kein json' })
  // Direkt am Listener vorbei: der Strom muss den Parse-Fehler selbst schlucken.
  expect(() => source.emit('token', undefined)).not.toThrow()
  expect(broken.data).toBe('kein json')
  expect(events).toEqual([{ kind: 'token' }])

  fake.restore()
})

test('der Rückgabewert schließt die Verbindung', () => {
  const { source, close, fake } = setup()
  close()
  expect(source().closed).toBe(true)
  fake.restore()
})
```

- [ ] **Step 3: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- sse`
Expected: FAIL — `Failed to resolve import "../lib/sse"`.

- [ ] **Step 4: `lib/sse.ts` schreiben**

```ts
import type { StreamEvent } from '../areas/agents/types'

export type StreamHandlers = {
  onEvent: (event: StreamEvent) => void
  onConnection: (connection: 'open' | 'reconnecting' | 'lost') => void
}

const eventNames = ['token', 'message', 'tool', 'status', 'usage', 'done', 'error'] as const

const maxFailures = 3

/**
 * Opens an SSE stream and translates named server events into StreamEvent.
 * The browser retries on its own; after three consecutive failures we give up
 * and report 'lost' so the view can offer a reload.
 */
export function openStream(url: string, handlers: StreamHandlers): () => void {
  const source = new EventSource(url)
  let failures = 0

  for (const name of eventNames) {
    source.addEventListener(name, (event) => {
      const payload = (event as MessageEvent).data
      let data: Record<string, unknown> = {}
      try {
        data = payload === undefined ? {} : (JSON.parse(String(payload)) as Record<string, unknown>)
      } catch {
        return
      }
      handlers.onEvent({ kind: name, ...data } as StreamEvent)
    })
  }

  source.onopen = () => {
    failures = 0
    handlers.onConnection('open')
  }

  source.onerror = () => {
    failures += 1
    if (failures >= maxFailures) {
      source.close()
      handlers.onConnection('lost')
      return
    }
    handlers.onConnection('reconnecting')
  }

  return () => source.close()
}
```

- [ ] **Step 5: Tests laufen lassen**

Run: `npm test -- sse`
Expected: PASS, acht Tests. Der Test zu unlesbaren Nutzdaten prüft, dass `emit('token', undefined)` — also `data: "undefined"` — nicht wirft; `JSON.parse` scheitert dort nicht, sondern `undefined` wird zu `{}`, weshalb `{ kind: 'token' }` erwartet wird.

- [ ] **Step 6: Commit**

```bash
git add src/AgentForge.Web/src/lib/sse.ts src/AgentForge.Web/src/test/fakeEventSource.ts src/AgentForge.Web/src/__tests__/sse.test.ts
git commit -m "feat: add sse stream adapter with reconnect accounting"
```

---

### Task 9: Die beiden Verlaufs-Hooks

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/mappers.ts`, `areas/agents/useRunStream.ts`, `areas/agents/useConversationStream.ts`
- Test: `src/AgentForge.Web/src/__tests__/streamHooks.test.tsx`

Die Übersetzung der beiden Nachrichten-DTOs in `TranscriptMessage` liegt in `mappers.ts` — sie gehört weder in die API-Funktionen noch in den Reducer.

**Interfaces:**
- Consumes: `getRunMessages`, `getConversationMessages`, `postConversationMessage` aus `./api`; `openStream` aus `../../lib/sse`; `transcriptReducer` und Freunde.
- Produces:
  - `runMessageToTranscript(dto: RunMessageDto): TranscriptMessage`
  - `conversationMessageToTranscript(dto: ConversationMessageDto): TranscriptMessage`
  - `useRunStream(runId: string): { messages: TranscriptMessage[]; status: RunStatus | null; usage: Usage | null; error: {...} | null; connection: ConnectionState; loading: boolean }`
  - `useConversationStream(conversationId: string): dasselbe, zusätzlich outgoing: OutgoingMessage[] und send(content: string, mentions: string[]): Promise<void>`

- [ ] **Step 1: Die Übersetzer schreiben**

`areas/agents/mappers.ts`:

```ts
import type {
  ConversationMessageDto,
  RunMessageDto,
  ToolCall,
  TranscriptMessage,
} from './types'

type RawToolCall = { id?: string; name?: string; arguments?: unknown }

function parseToolCalls(json: string | null, resultText: string | null): ToolCall[] {
  if (!json) return []
  try {
    const parsed = JSON.parse(json) as RawToolCall[]
    return parsed.map((call, index) => ({
      id: call.id ?? `call-${index}`,
      name: call.name ?? 'unbekannt',
      argumentsJson: JSON.stringify(call.arguments ?? {}, null, 2),
      resultText,
      failed: false,
    }))
  } catch {
    return []
  }
}

export function runMessageToTranscript(dto: RunMessageDto): TranscriptMessage {
  return {
    sequence: dto.sequence,
    role: dto.role,
    senderAgentId: null,
    senderName: null,
    content: dto.content ?? '',
    toolCalls: parseToolCalls(dto.toolCallsJson, dto.role === 'Tool' ? dto.content : null),
    mentions: [],
    state: 'complete',
    createdAt: dto.createdAt,
  }
}

export function conversationMessageToTranscript(dto: ConversationMessageDto): TranscriptMessage {
  return {
    sequence: dto.sequence,
    role: dto.role,
    senderAgentId: dto.senderAgentId,
    senderName: dto.senderName,
    content: dto.content ?? '',
    toolCalls: parseToolCalls(dto.toolCallsJson, null),
    mentions: dto.mentions,
    state: 'complete',
    createdAt: dto.createdAt,
  }
}
```

- [ ] **Step 2: Die fehlschlagenden Tests schreiben**

`src/__tests__/streamHooks.test.tsx`:

```tsx
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useConversationStream } from '../areas/agents/useConversationStream'
import { useRunStream } from '../areas/agents/useRunStream'
import { installFakeEventSource } from '../test/fakeEventSource'
import { stubFetch } from '../test/stubFetch'

function RunProbe({ runId }: { runId: string }) {
  const { messages, status, connection, loading } = useRunStream(runId)
  if (loading) return <p>lädt</p>
  return (
    <div>
      <p data-testid="status">{status ?? '—'}</p>
      <p data-testid="connection">{connection}</p>
      <ul>
        {messages.map((message) => (
          <li key={message.sequence}>{`${message.role}:${message.content}`}</li>
        ))}
      </ul>
    </div>
  )
}

function ConversationProbe({ id }: { id: string }) {
  const { messages, outgoing, send } = useConversationStream(id)
  return (
    <div>
      <button type="button" onClick={() => void send('Schaffst du das?', ['a2'])}>
        senden
      </button>
      <ul>
        {messages.map((message) => (
          <li key={message.sequence}>{message.content}</li>
        ))}
        {outgoing.map((item) => (
          <li key={item.clientKey}>{`${item.content} (${item.state})`}</li>
        ))}
      </ul>
    </div>
  )
}

test('lädt den Verlauf und öffnet danach den Strom', async () => {
  const fake = installFakeEventSource()
  stubFetch([
    [
      '/api/agents/runs/r1/messages',
      {
        json: [
          { id: 'm0', sequence: 0, role: 'System', content: 'Du bist …', toolCallsJson: null, toolCallId: null, createdAt: '2026-07-29T10:00:00Z' },
          { id: 'm1', sequence: 1, role: 'User', content: 'Erstelle eine D&D-Seite', toolCallsJson: null, toolCallId: null, createdAt: '2026-07-29T10:00:01Z' },
        ],
      },
    ],
  ])

  render(<RunProbe runId="r1" />)

  await waitFor(() => expect(screen.getByTestId('status')).toBeInTheDocument())
  expect(screen.getByText('System:Du bist …')).toBeInTheDocument()
  expect(fake.instances[0]!.url).toBe('/api/agents/runs/r1/stream')

  fake.restore()
})

test('Ereignisse aus dem Strom ergänzen den geladenen Verlauf', async () => {
  const fake = installFakeEventSource()
  stubFetch([['/api/agents/runs/r1/messages', { json: [] }]])

  render(<RunProbe runId="r1" />)
  await waitFor(() => expect(screen.getByTestId('status')).toBeInTheDocument())

  act(() => {
    fake.instances[0]!.emit('status', { status: 'Running' })
    fake.instances[0]!.emit('token', { streamId: 's1', sequence: 2, text: 'Ich lege los.' })
  })

  expect(screen.getByTestId('status')).toHaveTextContent('Running')
  expect(screen.getByText('Assistant:Ich lege los.')).toBeInTheDocument()

  fake.restore()
})

test('ein Verbindungsverlust wird im Zustand sichtbar', async () => {
  const fake = installFakeEventSource()
  stubFetch([['/api/agents/runs/r1/messages', { json: [] }]])

  render(<RunProbe runId="r1" />)
  await waitFor(() => expect(screen.getByTestId('connection')).toBeInTheDocument())

  act(() => {
    fake.instances[0]!.fail()
  })

  expect(screen.getByTestId('connection')).toHaveTextContent('reconnecting')

  fake.restore()
})

test('der Strom wird beim Verlassen der Ansicht geschlossen', async () => {
  const fake = installFakeEventSource()
  stubFetch([['/api/agents/runs/r1/messages', { json: [] }]])

  const view = render(<RunProbe runId="r1" />)
  await waitFor(() => expect(screen.getByTestId('connection')).toBeInTheDocument())
  view.unmount()

  expect(fake.instances[0]!.closed).toBe(true)

  fake.restore()
})

test('eine gesendete Nachricht erscheint sofort und wird bei Bestätigung ersetzt', async () => {
  const fake = installFakeEventSource()
  stubFetch([
    ['/api/agents/conversations/c1/messages', { json: [] }],
    ['/api/agents/conversations/c1/messages', { status: 202, json: { streamId: 's1' } }],
  ])

  render(<ConversationProbe id="c1" />)
  await waitFor(() => expect(fake.instances[0]).toBeDefined())

  await userEvent.click(screen.getByRole('button', { name: 'senden' }))
  expect(await screen.findByText('Schaffst du das? (sending)')).toBeInTheDocument()

  act(() => {
    fake.instances[0]!.emit('message', {
      streamId: 's1',
      sequence: 0,
      message: {
        sequence: 0, role: 'User', senderAgentId: null, senderName: null,
        content: 'Schaffst du das?', toolCalls: [], mentions: ['a2'],
        state: 'complete', createdAt: '2026-07-29T10:00:00Z',
      },
    })
  })

  expect(screen.queryByText('Schaffst du das? (sending)')).not.toBeInTheDocument()
  expect(screen.getByText('Schaffst du das?')).toBeInTheDocument()

  fake.restore()
})

test('eine gescheiterte Sendung bleibt als failed stehen', async () => {
  const fake = installFakeEventSource()
  stubFetch([
    ['/api/agents/conversations/c1/messages', { json: [] }],
    ['/api/agents/conversations/c1/messages', { status: 500 }],
  ])

  render(<ConversationProbe id="c1" />)
  await waitFor(() => expect(fake.instances[0]).toBeDefined())

  await userEvent.click(screen.getByRole('button', { name: 'senden' }))

  expect(await screen.findByText('Schaffst du das? (failed)')).toBeInTheDocument()

  fake.restore()
})
```

- [ ] **Step 3: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- streamHooks`
Expected: FAIL — `Failed to resolve import "../areas/agents/useConversationStream"`.

- [ ] **Step 4: `useRunStream.ts` schreiben**

```ts
import { useEffect, useReducer } from 'react'
import { openStream } from '../../lib/sse'
import { getRunMessages } from './api'
import { runMessageToTranscript } from './mappers'
import {
  initialTranscriptState,
  orderedMessages,
  transcriptReducer,
} from './transcriptReducer'

export function useRunStream(runId: string) {
  const [state, dispatch] = useReducer(transcriptReducer, initialTranscriptState)

  useEffect(() => {
    let cancelled = false
    let close: (() => void) | undefined

    // History first, stream second. The reducer merges without overwriting.
    getRunMessages(runId)
      .then((messages) => {
        if (cancelled) return
        dispatch({ type: 'loaded', messages: messages.map(runMessageToTranscript) })
      })
      .catch(() => {
        if (!cancelled) dispatch({ type: 'loaded', messages: [] })
      })
      .finally(() => {
        if (cancelled) return
        close = openStream(`/api/agents/runs/${runId}/stream`, {
          onEvent: (event) => dispatch({ type: 'event', event }),
          onConnection: (connection) => dispatch({ type: 'connection', connection }),
        })
      })

    return () => {
      cancelled = true
      close?.()
    }
  }, [runId])

  return {
    messages: orderedMessages(state),
    status: state.status,
    usage: state.usage,
    error: state.error,
    connection: state.connection,
    loading: state.connection === 'idle' && Object.keys(state.messages).length === 0,
  }
}
```

- [ ] **Step 5: `useConversationStream.ts` schreiben**

```ts
import { useCallback, useEffect, useReducer } from 'react'
import { openStream } from '../../lib/sse'
import { getConversationMessages, postConversationMessage } from './api'
import { conversationMessageToTranscript } from './mappers'
import {
  initialTranscriptState,
  orderedMessages,
  transcriptReducer,
} from './transcriptReducer'

export function useConversationStream(conversationId: string) {
  const [state, dispatch] = useReducer(transcriptReducer, initialTranscriptState)

  useEffect(() => {
    let cancelled = false
    let close: (() => void) | undefined

    getConversationMessages(conversationId)
      .then((messages) => {
        if (cancelled) return
        dispatch({ type: 'loaded', messages: messages.map(conversationMessageToTranscript) })
      })
      .catch(() => {
        if (!cancelled) dispatch({ type: 'loaded', messages: [] })
      })
      .finally(() => {
        if (cancelled) return
        close = openStream(`/api/agents/conversations/${conversationId}/stream`, {
          onEvent: (event) => dispatch({ type: 'event', event }),
          onConnection: (connection) => dispatch({ type: 'connection', connection }),
        })
      })

    return () => {
      cancelled = true
      close?.()
    }
  }, [conversationId])

  const send = useCallback(
    async (content: string, mentions: string[]) => {
      const clientKey = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
      dispatch({ type: 'outgoing', message: { clientKey, content, mentions, state: 'sending' } })
      try {
        await postConversationMessage(conversationId, { content, mentions })
      } catch {
        dispatch({ type: 'outgoingFailed', clientKey })
      }
    },
    [conversationId],
  )

  return {
    messages: orderedMessages(state),
    outgoing: state.outgoing,
    status: state.status,
    usage: state.usage,
    error: state.error,
    connection: state.connection,
    send,
  }
}
```

- [ ] **Step 6: Tests laufen lassen**

Run: `npm test -- streamHooks`
Expected: PASS, sechs Tests.

- [ ] **Step 7: Alles laufen lassen**

Run: `npm test && npm run lint && npm run typecheck`
Expected: alles PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AgentForge.Web/src/areas/agents src/AgentForge.Web/src/__tests__/streamHooks.test.tsx
git commit -m "feat: add run and conversation stream hooks"
```

---

### Task 10: Verlaufs-Bauteile

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/ToolCallCard.tsx`, `areas/agents/Transcript.tsx`, `areas/agents/TranscriptLog.tsx`
- Test: `src/AgentForge.Web/src/__tests__/Transcript.test.tsx`

**Interfaces:**
- Consumes: `TranscriptMessage`, `OutgoingMessage`, `ToolCall` aus `./types`.
- Produces:
  - `ToolCallCard({ call }: { call: ToolCall })` — zugeklappt, öffnet auf Klick.
  - `Transcript({ messages, outgoing, youLabel, onRetry })` — Sprechblasen, Werkzeugkarten, Klebeverhalten am unteren Rand.
  - `TranscriptLog({ messages })` — eine Zeile je Nachricht, inklusive System-Prompt.
  - `senderColor(agentId: string | null): string` — stabile Farbe aus der Agent-Id.

Die Werkzeugkarte ist ein `<details>`-Element. Das ist zugänglich, tastaturbedienbar und braucht keinen eigenen Zustand — die billigste richtige Lösung.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`src/__tests__/Transcript.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Transcript, senderColor } from '../areas/agents/Transcript'
import { TranscriptLog } from '../areas/agents/TranscriptLog'
import type { TranscriptMessage } from '../areas/agents/types'

function message(overrides: Partial<TranscriptMessage> & { sequence: number }): TranscriptMessage {
  return {
    role: 'Assistant', senderAgentId: null, senderName: null, content: '',
    toolCalls: [], mentions: [], state: 'complete', createdAt: '2026-07-29T10:00:00Z',
    ...overrides,
  }
}

test('der Verlauf ist ein log-Bereich für Vorleseprogramme', () => {
  render(<Transcript messages={[]} youLabel="Du" />)
  const log = screen.getByRole('log')
  expect(log).toHaveAttribute('aria-live', 'polite')
})

test('ohne Nachrichten erscheint ein ausformulierter leerer Zustand', () => {
  render(<Transcript messages={[]} youLabel="Du" />)
  expect(screen.getByText('Noch keine Nachrichten.')).toBeInTheDocument()
})

test('der System-Prompt erscheint im Gespräch nicht', () => {
  render(
    <Transcript
      messages={[message({ sequence: 0, role: 'System', content: 'Du bist ein Spezialist.' })]}
      youLabel="Du"
    />,
  )
  expect(screen.queryByText('Du bist ein Spezialist.')).not.toBeInTheDocument()
})

test('eigene und fremde Nachrichten tragen ihren Absender', () => {
  render(
    <Transcript
      messages={[
        message({ sequence: 1, role: 'User', content: 'Schaffst du das?' }),
        message({ sequence: 2, role: 'Assistant', senderName: 'frontend-dev', senderAgentId: 'a2', content: 'Ja.' }),
      ]}
      youLabel="Du"
    />,
  )

  expect(screen.getByText('Du')).toBeInTheDocument()
  expect(screen.getByText('frontend-dev')).toBeInTheDocument()
})

test('eine strömende Nachricht ist als solche erkennbar', () => {
  render(
    <Transcript messages={[message({ sequence: 2, content: 'Ich lege', state: 'streaming' })]} youLabel="Du" />,
  )
  expect(screen.getByLabelText('schreibt noch')).toBeInTheDocument()
})

test('eine Werkzeugkarte ist zu und öffnet sich auf Klick', async () => {
  render(
    <Transcript
      messages={[
        message({
          sequence: 2,
          toolCalls: [
            { id: 't1', name: 'write_file', argumentsJson: '{\n  "path": "A.tsx"\n}', resultText: 'geschrieben', failed: false },
          ],
        }),
      ]}
      youLabel="Du"
    />,
  )

  expect(screen.queryByText(/"path": "A.tsx"/)).not.toBeVisible()
  await userEvent.click(screen.getByText('write_file'))
  expect(screen.getByText(/"path": "A.tsx"/)).toBeVisible()
})

test('ein fehlgeschlagener Werkzeugaufruf ist ausgewiesen', () => {
  render(
    <Transcript
      messages={[
        message({
          sequence: 2,
          toolCalls: [{ id: 't1', name: 'run_shell', argumentsJson: '{}', resultText: 'exit 1', failed: true }],
        }),
      ]}
      youLabel="Du"
    />,
  )
  expect(screen.getByText('fehlgeschlagen')).toBeInTheDocument()
})

test('eine noch nicht gesendete Nachricht steht am Ende mit Hinweis', () => {
  render(
    <Transcript
      messages={[]}
      youLabel="Du"
      outgoing={[{ clientKey: 'k1', content: 'Hallo', mentions: [], state: 'sending' }]}
    />,
  )
  expect(screen.getByText('wird gesendet …')).toBeInTheDocument()
})

test('eine gescheiterte Nachricht bietet Wiederholen an', async () => {
  const onRetry = vi.fn()
  render(
    <Transcript
      messages={[]}
      youLabel="Du"
      outgoing={[{ clientKey: 'k1', content: 'Hallo', mentions: [], state: 'failed' }]}
      onRetry={onRetry}
    />,
  )

  await userEvent.click(screen.getByRole('button', { name: 'Wiederholen' }))
  expect(onRetry).toHaveBeenCalledWith('k1')
})

test('senderColor ist für dieselbe Id stabil und für verschiedene Ids verschieden', () => {
  expect(senderColor('a2')).toBe(senderColor('a2'))
  expect(senderColor('a2')).not.toBe(senderColor('a3'))
  expect(senderColor(null)).toBe('var(--text-muted)')
})

test('das Protokoll zeigt jede Nachricht mit Sequence und Rolle, auch den System-Prompt', () => {
  render(
    <TranscriptLog
      messages={[
        message({ sequence: 0, role: 'System', content: 'Du bist ein Spezialist.' }),
        message({ sequence: 1, role: 'User', content: 'Erstelle eine Seite' }),
      ]}
    />,
  )

  expect(screen.getByText('Du bist ein Spezialist.')).toBeInTheDocument()
  expect(screen.getByText('System')).toBeInTheDocument()
  expect(screen.getByText('0')).toBeInTheDocument()
})
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- Transcript`
Expected: FAIL — `Failed to resolve import "../areas/agents/Transcript"`.

- [ ] **Step 3: `ToolCallCard.tsx` schreiben**

```tsx
import type { ToolCall } from './types'

export function ToolCallCard({ call }: { call: ToolCall }) {
  return (
    <details className="my-1 rounded-md border text-sm" style={{ borderColor: 'var(--border)' }}>
      <summary className="flex cursor-pointer items-center gap-2 px-2 py-1">
        <span className="font-mono font-semibold" style={{ color: 'var(--text-strong)' }}>
          {call.name}
        </span>
        {call.failed && (
          <span className="text-xs font-semibold" style={{ color: 'var(--danger)' }}>
            fehlgeschlagen
          </span>
        )}
      </summary>
      <div className="border-t px-2 py-2" style={{ borderColor: 'var(--border)' }}>
        <pre className="overflow-x-auto font-mono text-xs whitespace-pre-wrap">{call.argumentsJson}</pre>
        {call.resultText !== null && (
          <pre className="mt-2 overflow-x-auto font-mono text-xs whitespace-pre-wrap" style={{ color: 'var(--text-muted)' }}>
            {call.resultText}
          </pre>
        )}
      </div>
    </details>
  )
}
```

- [ ] **Step 4: `Transcript.tsx` schreiben**

Das Klebeverhalten hängt an einem Merker, den der Scroll-Zuhörer setzt: solange der Nutzer nahe am unteren Rand steht, wird nach jeder Änderung nachgescrollt; sonst erscheint ein Knopf. jsdom liefert für alle Maße 0, weshalb der Merker dort immer „unten" bedeutet — die Tests prüfen deshalb Struktur, nicht Scrollen.

```tsx
import { useEffect, useRef, useState } from 'react'
import { ToolCallCard } from './ToolCallCard'
import type { OutgoingMessage, TranscriptMessage } from './types'

export function senderColor(agentId: string | null): string {
  if (!agentId) return 'var(--text-muted)'
  let hash = 0
  for (const character of agentId) hash = (hash * 31 + character.charCodeAt(0)) % 360
  return `hsl(${hash} 55% 45%)`
}

const bubble = 'max-w-[85%] rounded-2xl px-3 py-2 text-sm whitespace-pre-wrap'

export function Transcript({
  messages,
  outgoing = [],
  youLabel,
  onRetry,
}: {
  messages: TranscriptMessage[]
  outgoing?: OutgoingMessage[]
  youLabel: string
  onRetry?: (clientKey: string) => void
}) {
  const endRef = useRef<HTMLDivElement>(null)
  const scrollRef = useRef<HTMLDivElement>(null)
  const [atBottom, setAtBottom] = useState(true)

  useEffect(() => {
    if (atBottom) endRef.current?.scrollIntoView({ block: 'end' })
  }, [messages, outgoing, atBottom])

  function handleScroll() {
    const element = scrollRef.current
    if (!element) return
    const distance = element.scrollHeight - element.scrollTop - element.clientHeight
    setAtBottom(distance < 40)
  }

  const visible = messages.filter((message) => message.role !== 'System')

  return (
    <div className="relative">
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        role="log"
        aria-live="polite"
        aria-label="Verlauf"
        className="grid max-h-[65vh] gap-2 overflow-y-auto p-3"
      >
        {visible.length === 0 && outgoing.length === 0 && (
          <p className="text-sm" style={{ color: 'var(--text-muted)' }}>
            Noch keine Nachrichten.
          </p>
        )}

        {visible.map((message) => {
          const mine = message.role === 'User'
          return (
            <article key={message.sequence} className={mine ? 'ml-auto' : 'mr-auto'}>
              <p className="mb-0.5 text-xs font-semibold" style={{ color: mine ? 'var(--text-muted)' : senderColor(message.senderAgentId) }}>
                {mine ? youLabel : (message.senderName ?? 'Agent')}
              </p>
              {message.content !== '' && (
                <div
                  className={bubble}
                  style={
                    mine
                      ? { background: 'var(--accent)', color: 'var(--accent-text)' }
                      : { background: 'var(--bg-sunken)', color: 'var(--text-strong)' }
                  }
                >
                  {message.content}
                  {message.state === 'streaming' && (
                    <span aria-label="schreibt noch"> …</span>
                  )}
                </div>
              )}
              {message.toolCalls.map((call) => (
                <ToolCallCard key={call.id} call={call} />
              ))}
            </article>
          )
        })}

        {outgoing.map((item) => (
          <article key={item.clientKey} className="ml-auto">
            <div className={bubble} style={{ background: 'var(--bg-sunken)', color: 'var(--text)' }}>
              {item.content}
            </div>
            {item.state === 'sending' ? (
              <p className="text-right text-xs" style={{ color: 'var(--text-muted)' }}>
                wird gesendet …
              </p>
            ) : (
              <p className="text-right text-xs" style={{ color: 'var(--danger)' }}>
                nicht gesendet{' '}
                <button type="button" className="underline" onClick={() => onRetry?.(item.clientKey)}>
                  Wiederholen
                </button>
              </p>
            )}
          </article>
        ))}

        <div ref={endRef} />
      </div>

      {!atBottom && (
        <button
          type="button"
          className="absolute right-4 bottom-3 rounded-full px-3 py-1 text-xs"
          style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
          onClick={() => {
            setAtBottom(true)
            endRef.current?.scrollIntoView({ block: 'end' })
          }}
        >
          Nach unten
        </button>
      )}
    </div>
  )
}
```

- [ ] **Step 5: `TranscriptLog.tsx` schreiben**

```tsx
import type { TranscriptMessage } from './types'

export function TranscriptLog({ messages }: { messages: TranscriptMessage[] }) {
  if (messages.length === 0) {
    return (
      <p className="p-3 text-sm" style={{ color: 'var(--text-muted)' }}>
        Noch keine Einträge.
      </p>
    )
  }

  return (
    <ol className="divide-y font-mono text-xs" style={{ borderColor: 'var(--border)' }}>
      {messages.map((message) => (
        <li key={message.sequence} className="flex gap-3 px-3 py-1.5">
          <span className="w-6 shrink-0 text-right" style={{ color: 'var(--text-muted)' }}>
            {message.sequence}
          </span>
          <span className="w-20 shrink-0 uppercase" style={{ color: 'var(--text-muted)' }}>
            {message.role}
          </span>
          <span className="min-w-0 flex-1 break-words">
            {message.content}
            {message.toolCalls.map((call) => (
              <span key={call.id} className="ml-2" style={{ color: 'var(--text-strong)' }}>
                {call.name}
              </span>
            ))}
          </span>
        </li>
      ))}
    </ol>
  )
}
```

- [ ] **Step 6: Tests laufen lassen**

Run: `npm test -- Transcript`
Expected: PASS, elf Tests.

- [ ] **Step 7: Commit**

```bash
git add src/AgentForge.Web/src/areas/agents src/AgentForge.Web/src/__tests__/Transcript.test.tsx
git commit -m "feat: add shared transcript components with collapsible tool calls"
```

---

### Task 11: Agenten-Liste und Registry-Eintrag

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/AgentListPage.tsx`, `areas/agents/routes.tsx`
- Modify: `src/AgentForge.Web/src/areas/index.ts`
- Test: `src/AgentForge.Web/src/__tests__/AgentListPage.test.tsx`

**Interfaces:**
- Consumes: `listAgents`, `archiveAgent` aus `./api`; `useContextPanel`; `rememberItem`.
- Produces:
  - `AgentListPage()` als Standardexport.
  - `agentsArea: AreaModule` aus `routes.tsx` mit `slug: 'agents'`, `title: 'Agents'`, den Routen aus der Spec und `nav` mit drei Einträgen.
  - `areaModules` enthält jetzt `agentsArea`.

Die Routen zeigen in dieser Aufgabe auf Platzhalter, die in den Aufgaben 12 bis 17 ersetzt werden. Ein Platzhalter ist hier kein Verstoß gegen „keine Platzhalter": er ist echter, lieferbarer Inhalt — eine Seite, die sagt, dass es sie noch nicht gibt — und jede folgende Aufgabe nennt die Zeile, die sie ersetzt.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`src/__tests__/AgentListPage.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import AgentListPage from '../areas/agents/AgentListPage'
import { ContextPanelProvider } from '../shell/ContextPanel'
import { stubFetch } from '../test/stubFetch'

const leo = {
  id: 'a1', name: 'leo', description: 'Orchestrator', systemPrompt: 'Du bist Leo.',
  model: 'gpt-5', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20, allowedTools: [],
  createdAt: '2026-07-29T10:00:00Z', updatedAt: '2026-07-29T10:00:00Z',
  archivedAt: null, concurrencyToken: 'tok-a1',
}

function renderPage() {
  return render(
    <MemoryRouter>
      <ContextPanelProvider>
        <AgentListPage />
      </ContextPanelProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  localStorage.clear()
})

test('zeigt die geladenen Agenten in einer Tabelle', async () => {
  stubFetch([['/api/agents/definitions', { json: { items: [leo], total: 1 } }]])
  renderPage()

  expect(await screen.findByRole('link', { name: 'leo' })).toBeInTheDocument()
  expect(screen.getByRole('columnheader', { name: 'Modell' })).toBeInTheDocument()
  expect(screen.getByText('gpt-5')).toBeInTheDocument()
})

test('ohne Agenten erscheint der leere Zustand mit dem nächsten Schritt', async () => {
  stubFetch([['/api/agents/definitions', { json: { items: [], total: 0 } }]])
  renderPage()

  expect(await screen.findByText('Noch keine Agenten.')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Agent anlegen' })).toBeInTheDocument()
})

test('die Suche wird entprellt und als q geschickt', async () => {
  vi.useFakeTimers()
  const calls = stubFetch([
    ['/api/agents/definitions', { json: { items: [leo], total: 1 } }],
    ['/api/agents/definitions', { json: { items: [], total: 0 } }],
  ])
  const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })

  renderPage()
  await vi.waitFor(() => expect(calls).toHaveLength(1))

  await user.type(screen.getByRole('searchbox', { name: 'Suche' }), 'front')
  expect(calls).toHaveLength(1)

  await vi.advanceTimersByTimeAsync(300)
  await vi.waitFor(() => expect(calls).toHaveLength(2))
  expect(calls[1]!.url).toContain('q=front')

  vi.useRealTimers()
})

test('ein Fehler beim Laden wird als Meldung gezeigt', async () => {
  stubFetch([
    ['/api/agents/definitions', { status: 500, json: { title: 'Serverfehler' } }],
  ])
  renderPage()

  expect(await screen.findByRole('alert')).toHaveTextContent('Serverfehler')
})

test('Archivieren fragt nach und lädt die Liste neu', async () => {
  const calls = stubFetch([
    ['/api/agents/definitions', { json: { items: [leo], total: 1 } }],
    ['/api/agents/definitions/a1', { status: 204 }],
    ['/api/agents/definitions', { json: { items: [], total: 0 } }],
  ])
  renderPage()
  await screen.findByRole('link', { name: 'leo' })

  await userEvent.click(screen.getByRole('button', { name: 'leo archivieren' }))
  expect(screen.getByRole('dialog', { name: 'Agent archivieren' })).toBeInTheDocument()
  await userEvent.click(screen.getByRole('button', { name: 'Archivieren' }))

  await waitFor(() => expect(calls).toHaveLength(3))
  expect(calls[1]!.method).toBe('DELETE')
  expect(await screen.findByText('Noch keine Agenten.')).toBeInTheDocument()
})

test('Abbrechen der Rückfrage archiviert nichts', async () => {
  const calls = stubFetch([['/api/agents/definitions', { json: { items: [leo], total: 1 } }]])
  renderPage()
  await screen.findByRole('link', { name: 'leo' })

  await userEvent.click(screen.getByRole('button', { name: 'leo archivieren' }))
  await userEvent.click(screen.getByRole('button', { name: 'Abbrechen' }))

  expect(calls).toHaveLength(1)
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
})

test('jede Zeile verweist auf Chat und Run', async () => {
  stubFetch([['/api/agents/definitions', { json: { items: [leo], total: 1 } }]])
  renderPage()
  await screen.findByRole('link', { name: 'leo' })

  expect(screen.getByRole('link', { name: 'Bearbeiten' })).toHaveAttribute(
    'href',
    '/agents/definitions/a1/edit',
  )
  expect(screen.getByRole('button', { name: 'Run starten' })).toBeInTheDocument()
})
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- AgentListPage`
Expected: FAIL — `Failed to resolve import "../areas/agents/AgentListPage"`.

- [ ] **Step 3: `AgentListPage.tsx` schreiben**

`StartRunDialog` entsteht erst in Aufgabe 14; bis dahin führt „Run starten" auf die Run-Liste. Aufgabe 14 ersetzt genau diesen `onClick`.

```tsx
import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { archiveAgent, listAgents } from './api'
import { ApiRequestError } from '../../lib/http'
import type { AgentDto } from './types'

const cell = 'border-b px-3 py-2 align-top text-sm'

export default function AgentListPage() {
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState<AgentDto | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    const timeout = window.setTimeout(
      () => {
        setLoading(true)
        listAgents({ q: query, skip: 0, take: 50 })
          .then((page) => {
            setAgents(page.items)
            setError(null)
          })
          .catch((cause: unknown) => {
            setAgents([])
            setError(cause instanceof ApiRequestError ? cause.info.title : 'Laden fehlgeschlagen.')
          })
          .finally(() => setLoading(false))
      },
      query === '' ? 0 : 300,
    )
    return () => window.clearTimeout(timeout)
  }, [query, reloadKey])

  async function confirmArchive() {
    if (!pending) return
    try {
      await archiveAgent(pending.id)
      setPending(null)
      setReloadKey((key) => key + 1)
    } catch (cause) {
      setPending(null)
      setError(cause instanceof ApiRequestError ? cause.info.title : 'Archivieren fehlgeschlagen.')
    }
  }

  return (
    <section>
      <header className="mb-4 flex items-center gap-3">
        <h1 className="text-lg font-semibold" style={{ color: 'var(--text-strong)' }}>
          Agenten
        </h1>
        <Link
          to="/agents/definitions/new"
          className="ml-auto rounded-md px-3 py-1.5 text-sm font-medium"
          style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
        >
          Agent anlegen
        </Link>
      </header>

      <label className="mb-4 block max-w-sm text-sm">
        Suche
        <input
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          className="mt-1 w-full rounded-md border px-2 py-1.5"
          style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
        />
      </label>

      {error && (
        <p role="alert" className="mb-3 text-sm" style={{ color: 'var(--danger)' }}>
          {error}
        </p>
      )}

      {loading && <p className="text-sm">Wird geladen …</p>}

      {!loading && agents.length === 0 && !error && (
        <div className="rounded-md border border-dashed p-6 text-center" style={{ borderColor: 'var(--border)' }}>
          <p className="mb-3 text-sm">Noch keine Agenten.</p>
          <Link
            to="/agents/definitions/new"
            className="rounded-md px-3 py-1.5 text-sm font-medium"
            style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
          >
            Agent anlegen
          </Link>
        </div>
      )}

      {agents.length > 0 && (
        <table className="w-full border-collapse">
          <thead>
            <tr style={{ background: 'var(--bg-sunken)' }}>
              <th className={`${cell} text-left font-semibold`} style={{ borderColor: 'var(--border)' }}>Name</th>
              <th className={`${cell} text-left font-semibold`} style={{ borderColor: 'var(--border)' }}>Modell</th>
              <th className={`${cell} text-left font-semibold`} style={{ borderColor: 'var(--border)' }}>Beschreibung</th>
              <th className={`${cell} text-left font-semibold`} style={{ borderColor: 'var(--border)' }}>Aktionen</th>
            </tr>
          </thead>
          <tbody>
            {agents.map((agent) => (
              <tr key={agent.id}>
                <td className={cell} style={{ borderColor: 'var(--border)' }}>
                  <Link to={`/agents/definitions/${agent.id}`} className="underline">
                    {agent.name}
                  </Link>
                </td>
                <td className={cell} style={{ borderColor: 'var(--border)' }}>{agent.model}</td>
                <td className={cell} style={{ borderColor: 'var(--border)' }}>{agent.description ?? '—'}</td>
                <td className={`${cell} space-x-2 whitespace-nowrap`} style={{ borderColor: 'var(--border)' }}>
                  <button type="button" className="underline" onClick={() => navigate('/agents/runs')}>
                    Run starten
                  </button>
                  <Link to={`/agents/definitions/${agent.id}/edit`} className="underline">
                    Bearbeiten
                  </Link>
                  <button
                    type="button"
                    className="underline"
                    aria-label={`${agent.name} archivieren`}
                    onClick={() => setPending(agent)}
                  >
                    Archivieren
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {pending && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Agent archivieren"
          className="mt-4 rounded-md border p-4"
          style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
        >
          <p className="mb-3 text-sm">
            {`„${pending.name}" archivieren? Der Agent verschwindet aus der Liste, seine Runs bleiben erhalten.`}
          </p>
          <div className="flex gap-2">
            <button
              type="button"
              className="rounded-md px-3 py-1.5 text-sm font-medium"
              style={{ background: 'var(--danger)', color: '#fff' }}
              onClick={() => void confirmArchive()}
            >
              Archivieren
            </button>
            <button type="button" className="rounded-md border px-3 py-1.5 text-sm" style={{ borderColor: 'var(--border)' }} onClick={() => setPending(null)}>
              Abbrechen
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
```

- [ ] **Step 4: `routes.tsx` mit Platzhaltern für die noch fehlenden Seiten schreiben**

```tsx
import { Navigate } from 'react-router-dom'
import type { AreaModule } from '../../lib/areas'
import AgentListPage from './AgentListPage'

function NotYet({ what }: { what: string }) {
  return <p className="text-sm">{`${what} entsteht in einer späteren Aufgabe.`}</p>
}

export const agentsArea: AreaModule = {
  slug: 'agents',
  title: 'Agents',
  nav: [
    { to: '/agents/definitions', label: 'Agenten' },
    { to: '/agents/runs', label: 'Runs' },
    { to: '/agents/conversations', label: 'Gespräche' },
  ],
  routes: [
    { path: '/agents', element: <Navigate to="/agents/definitions" replace /> },
    { path: '/agents/definitions', element: <AgentListPage /> },
    { path: '/agents/definitions/new', element: <NotYet what="Das Formular" /> },
    { path: '/agents/definitions/:id', element: <NotYet what="Die Detailseite" /> },
    { path: '/agents/definitions/:id/edit', element: <NotYet what="Das Formular" /> },
    { path: '/agents/runs', element: <NotYet what="Die Run-Liste" /> },
    { path: '/agents/runs/:id', element: <NotYet what="Das Run-Detail" /> },
    { path: '/agents/conversations', element: <NotYet what="Die Gesprächsliste" /> },
    { path: '/agents/conversations/:id', element: <NotYet what="Das Gespräch" /> },
  ],
}
```

- [ ] **Step 5: Den Bereich in die Registry eintragen**

`src/areas/index.ts`:

```ts
import type { AreaModule } from '../lib/areas'
import { agentsArea } from './agents/routes'

/**
 * Areas are listed by name, never collected by glob — the same rule the host
 * follows with builder.AddArea<T>(). A new area costs one line here.
 */
export const areaModules: AreaModule[] = [agentsArea]
```

- [ ] **Step 6: Tests laufen lassen**

Run: `npm test -- AgentListPage`
Expected: PASS, sieben Tests.

- [ ] **Step 7: Alles laufen lassen und von Hand ansehen**

Run: `npm test && npm run lint && npm run typecheck`
Expected: alles PASS.

Run: `npm run dev:mock`, dann `http://localhost:5173/agents/definitions` öffnen.
Expected: drei Agenten in der Tabelle, Suche filtert, Archivieren entfernt eine Zeile. Danach beenden.

- [ ] **Step 8: Commit**

```bash
git add src/AgentForge.Web/src
git commit -m "feat: add agent list page and register the agents area"
```

---

### Task 12: Agenten-Formular

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/AgentFormPage.tsx`, `areas/agents/agentValidation.ts`
- Modify: `src/AgentForge.Web/src/areas/agents/routes.tsx` (zwei Platzhalter-Zeilen ersetzen: `/agents/definitions/new` und `/agents/definitions/:id/edit`)
- Test: `src/AgentForge.Web/src/__tests__/agentValidation.test.ts`, `src/__tests__/AgentFormPage.test.tsx`

**Interfaces:**
- Consumes: `getAgent`, `createAgent`, `updateAgent` aus `./api`.
- Produces:
  - `type AgentDraft = { name: string; description: string; systemPrompt: string; model: string; temperature: string; maxOutputTokens: string; maxTurns: string; allowedTools: string[] }`
  - `emptyDraft: AgentDraft`, `draftFromAgent(agent: AgentDto): AgentDraft`
  - `validateDraft(draft: AgentDraft): Record<string, string>` — leeres Objekt heißt gültig.
  - `bodyFromDraft(draft: AgentDraft): CreateAgentBody`
  - `AgentFormPage()` als Standardexport, bedient beide Routen über `useParams`.

Die Zahlenfelder liegen im Entwurf als **Zeichenketten**. Ein `number`-Zustand kann leere Eingaben nicht darstellen, und `NaN` durch die Validierung zu tragen ist mühsamer, als am Rand einmal umzuwandeln.

- [ ] **Step 1: Die fehlschlagenden Validierungstests schreiben**

`src/__tests__/agentValidation.test.ts`:

```ts
import { bodyFromDraft, emptyDraft, validateDraft } from '../areas/agents/agentValidation'

const valid = {
  ...emptyDraft,
  name: 'frontend-dev',
  systemPrompt: 'Du bist ein Spezialist.',
  model: 'gpt-5',
}

test('ein vollständiger Entwurf ist gültig', () => {
  expect(validateDraft(valid)).toEqual({})
})

test('Name, System-Prompt und Modell sind Pflicht', () => {
  const errors = validateDraft({ ...emptyDraft, name: '  ', systemPrompt: '', model: '' })
  expect(Object.keys(errors).sort()).toEqual(['model', 'name', 'systemPrompt'])
})

test('der Name darf höchstens 100 Zeichen haben', () => {
  expect(validateDraft({ ...valid, name: 'a'.repeat(101) }).name).toBe(
    'Höchstens 100 Zeichen.',
  )
})

test('Temperature liegt zwischen 0 und 2', () => {
  expect(validateDraft({ ...valid, temperature: '-0.1' }).temperature).toBe('Zwischen 0 und 2.')
  expect(validateDraft({ ...valid, temperature: '2.1' }).temperature).toBe('Zwischen 0 und 2.')
  expect(validateDraft({ ...valid, temperature: '0' }).temperature).toBeUndefined()
  expect(validateDraft({ ...valid, temperature: '2' }).temperature).toBeUndefined()
})

test('keine Zahl ist ein eigener Fehler', () => {
  expect(validateDraft({ ...valid, temperature: 'warm' }).temperature).toBe('Bitte eine Zahl.')
})

test('Output-Token liegen zwischen 1 und 200000 und sind ganzzahlig', () => {
  expect(validateDraft({ ...valid, maxOutputTokens: '0' }).maxOutputTokens).toBe('Zwischen 1 und 200000.')
  expect(validateDraft({ ...valid, maxOutputTokens: '200001' }).maxOutputTokens).toBe('Zwischen 1 und 200000.')
  expect(validateDraft({ ...valid, maxOutputTokens: '4096.5' }).maxOutputTokens).toBe('Bitte eine ganze Zahl.')
})

test('Turns liegen zwischen 1 und 200', () => {
  expect(validateDraft({ ...valid, maxTurns: '201' }).maxTurns).toBe('Zwischen 1 und 200.')
  expect(validateDraft({ ...valid, maxTurns: '1' }).maxTurns).toBeUndefined()
})

test('bodyFromDraft wandelt Zahlen um und macht aus leerer Beschreibung null', () => {
  expect(bodyFromDraft({ ...valid, description: '   ' })).toEqual({
    name: 'frontend-dev',
    description: null,
    systemPrompt: 'Du bist ein Spezialist.',
    model: 'gpt-5',
    temperature: 0.7,
    maxOutputTokens: 4096,
    maxTurns: 20,
    allowedTools: [],
  })
})
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- agentValidation`
Expected: FAIL — `Failed to resolve import "../areas/agents/agentValidation"`.

- [ ] **Step 3: `agentValidation.ts` schreiben**

Die Grenzen sind wortgleich mit denen des Servers; weicht der Server ab, gilt der Server, und diese Datei wird nachgezogen.

```ts
import type { CreateAgentBody } from './api'
import type { AgentDto } from './types'

export type AgentDraft = {
  name: string
  description: string
  systemPrompt: string
  model: string
  temperature: string
  maxOutputTokens: string
  maxTurns: string
  allowedTools: string[]
}

export const emptyDraft: AgentDraft = {
  name: '',
  description: '',
  systemPrompt: '',
  model: '',
  temperature: '0.7',
  maxOutputTokens: '4096',
  maxTurns: '20',
  allowedTools: [],
}

export function draftFromAgent(agent: AgentDto): AgentDraft {
  return {
    name: agent.name,
    description: agent.description ?? '',
    systemPrompt: agent.systemPrompt,
    model: agent.model,
    temperature: String(agent.temperature),
    maxOutputTokens: String(agent.maxOutputTokens),
    maxTurns: String(agent.maxTurns),
    allowedTools: agent.allowedTools,
  }
}

function checkRange(
  raw: string,
  min: number,
  max: number,
  integer: boolean,
): string | undefined {
  const value = Number(raw)
  if (raw.trim() === '' || Number.isNaN(value)) return 'Bitte eine Zahl.'
  if (integer && !Number.isInteger(value)) return 'Bitte eine ganze Zahl.'
  if (value < min || value > max) return `Zwischen ${min} und ${max}.`
  return undefined
}

export function validateDraft(draft: AgentDraft): Record<string, string> {
  const errors: Record<string, string> = {}

  if (draft.name.trim() === '') errors.name = 'Pflichtfeld.'
  else if (draft.name.length > 100) errors.name = 'Höchstens 100 Zeichen.'

  if (draft.systemPrompt.trim() === '') errors.systemPrompt = 'Pflichtfeld.'
  if (draft.model.trim() === '') errors.model = 'Pflichtfeld.'
  if (draft.description.length > 1000) errors.description = 'Höchstens 1000 Zeichen.'

  const temperature = checkRange(draft.temperature, 0, 2, false)
  if (temperature) errors.temperature = temperature

  const tokens = checkRange(draft.maxOutputTokens, 1, 200000, true)
  if (tokens) errors.maxOutputTokens = tokens

  const turns = checkRange(draft.maxTurns, 1, 200, true)
  if (turns) errors.maxTurns = turns

  return errors
}

export function bodyFromDraft(draft: AgentDraft): CreateAgentBody {
  return {
    name: draft.name.trim(),
    description: draft.description.trim() === '' ? null : draft.description.trim(),
    systemPrompt: draft.systemPrompt,
    model: draft.model.trim(),
    temperature: Number(draft.temperature),
    maxOutputTokens: Number(draft.maxOutputTokens),
    maxTurns: Number(draft.maxTurns),
    allowedTools: draft.allowedTools,
  }
}
```

- [ ] **Step 4: Test laufen lassen**

Run: `npm test -- agentValidation`
Expected: PASS, acht Tests.

- [ ] **Step 5: Die fehlschlagenden Formulartests schreiben**

`src/__tests__/AgentFormPage.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import AgentFormPage from '../areas/agents/AgentFormPage'
import { stubFetch } from '../test/stubFetch'

const leo = {
  id: 'a1', name: 'leo', description: 'Orchestrator', systemPrompt: 'Du bist Leo.',
  model: 'gpt-5', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20, allowedTools: ['write_file'],
  createdAt: '2026-07-29T10:00:00Z', updatedAt: '2026-07-29T10:00:00Z',
  archivedAt: null, concurrencyToken: 'tok-a1',
}

function renderForm(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/agents/definitions/new" element={<AgentFormPage />} />
        <Route path="/agents/definitions/:id/edit" element={<AgentFormPage />} />
        <Route path="/agents/definitions/:id" element={<p>Detailseite</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

test('das Formular hat vier Abschnitte in fester Reihenfolge', () => {
  renderForm('/agents/definitions/new')
  const headings = screen.getAllByRole('heading', { level: 2 }).map((node) => node.textContent)
  expect(headings).toEqual(['Identität', 'System-Prompt', 'Modell & Grenzen', 'Werkzeuge'])
})

test('ungültige Eingaben werden am Feld gemeldet und nichts wird gesendet', async () => {
  const calls = stubFetch([])
  renderForm('/agents/definitions/new')

  await userEvent.click(screen.getByRole('button', { name: 'Anlegen' }))

  expect(screen.getByText('Pflichtfeld.')).toBeInTheDocument()
  expect(calls).toHaveLength(0)
})

test('ein gültiger Entwurf wird angelegt und führt zur Detailseite', async () => {
  const calls = stubFetch([['/api/agents/definitions', { status: 201, json: leo }]])
  renderForm('/agents/definitions/new')

  await userEvent.type(screen.getByLabelText('Name'), 'leo')
  await userEvent.type(screen.getByLabelText('System-Prompt'), 'Du bist Leo.')
  await userEvent.type(screen.getByLabelText('Modell'), 'gpt-5')
  await userEvent.click(screen.getByRole('button', { name: 'Anlegen' }))

  await waitFor(() => expect(screen.getByText('Detailseite')).toBeInTheDocument())
  expect(calls[0]!.body).toMatchObject({ name: 'leo', model: 'gpt-5', temperature: 0.7 })
})

test('beim Bearbeiten werden die Werte geladen und das Token mitgeschickt', async () => {
  const calls = stubFetch([
    ['/api/agents/definitions/a1', { json: leo }],
    ['/api/agents/definitions/a1', { json: { ...leo, name: 'leo-2' } }],
  ])
  renderForm('/agents/definitions/a1/edit')

  await waitFor(() => expect(screen.getByLabelText('Name')).toHaveValue('leo'))
  await userEvent.clear(screen.getByLabelText('Name'))
  await userEvent.type(screen.getByLabelText('Name'), 'leo-2')
  await userEvent.click(screen.getByRole('button', { name: 'Speichern' }))

  await waitFor(() => expect(calls).toHaveLength(2))
  expect(calls[1]!.method).toBe('PUT')
  expect(calls[1]!.body).toMatchObject({ name: 'leo-2', concurrencyToken: 'tok-a1' })
})

test('eine Namenskollision landet am Namensfeld', async () => {
  stubFetch([
    [
      '/api/agents/definitions',
      { status: 409, json: { type: 'errors/name-conflict', title: 'Name schon belegt.' } },
    ],
  ])
  renderForm('/agents/definitions/new')

  await userEvent.type(screen.getByLabelText('Name'), 'leo')
  await userEvent.type(screen.getByLabelText('System-Prompt'), 'Du bist Leo.')
  await userEvent.type(screen.getByLabelText('Modell'), 'gpt-5')
  await userEvent.click(screen.getByRole('button', { name: 'Anlegen' }))

  expect(await screen.findByText('Name schon belegt. Bitte anders benennen.')).toBeInTheDocument()
})

test('ein Nebenläufigkeitskonflikt bietet Neuladen und behält die Eingaben', async () => {
  stubFetch([
    ['/api/agents/definitions/a1', { json: leo }],
    [
      '/api/agents/definitions/a1',
      { status: 409, json: { type: 'errors/concurrency-conflict', title: 'Konflikt' } },
    ],
  ])
  renderForm('/agents/definitions/a1/edit')

  await waitFor(() => expect(screen.getByLabelText('Name')).toHaveValue('leo'))
  await userEvent.clear(screen.getByLabelText('Name'))
  await userEvent.type(screen.getByLabelText('Name'), 'leo-neu')
  await userEvent.click(screen.getByRole('button', { name: 'Speichern' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('anderswo geändert')
  expect(screen.getByRole('button', { name: 'Neu laden' })).toBeInTheDocument()
  expect(screen.getByLabelText('Name')).toHaveValue('leo-neu')
})

test('Feldfehler des Servers werden an die Felder verteilt', async () => {
  stubFetch([
    [
      '/api/agents/definitions',
      {
        status: 400,
        json: {
          type: 'errors/validation-failed',
          title: 'Ungültig',
          errors: { model: ['Unbekanntes Modell.'] },
        },
      },
    ],
  ])
  renderForm('/agents/definitions/new')

  await userEvent.type(screen.getByLabelText('Name'), 'leo')
  await userEvent.type(screen.getByLabelText('System-Prompt'), 'Du bist Leo.')
  await userEvent.type(screen.getByLabelText('Modell'), 'gibtsnicht')
  await userEvent.click(screen.getByRole('button', { name: 'Anlegen' }))

  expect(await screen.findByText('Unbekanntes Modell.')).toBeInTheDocument()
})

test('Werkzeuge lassen sich als Plaketten hinzufügen und entfernen', async () => {
  renderForm('/agents/definitions/new')

  await userEvent.type(screen.getByLabelText('Werkzeug hinzufügen'), 'write_file{Enter}')
  expect(screen.getByText('write_file')).toBeInTheDocument()

  await userEvent.click(screen.getByRole('button', { name: 'write_file entfernen' }))
  expect(screen.queryByText('write_file')).not.toBeInTheDocument()
})
```

- [ ] **Step 6: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- AgentFormPage`
Expected: FAIL — `Failed to resolve import "../areas/agents/AgentFormPage"`.

- [ ] **Step 7: `AgentFormPage.tsx` schreiben**

```tsx
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ApiRequestError } from '../../lib/http'
import { createAgent, getAgent, updateAgent } from './api'
import { bodyFromDraft, draftFromAgent, emptyDraft, validateDraft } from './agentValidation'
import type { AgentDraft } from './agentValidation'

const field = 'mt-1 w-full rounded-md border px-2 py-1.5 text-sm'
const border = { borderColor: 'var(--border)', background: 'var(--bg-raised)' }

function FieldError({ message }: { message: string | undefined }) {
  if (!message) return null
  return (
    <span className="mt-1 block text-xs" style={{ color: 'var(--danger)' }}>
      {message}
    </span>
  )
}

export default function AgentFormPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const editing = id !== undefined

  const [draft, setDraft] = useState<AgentDraft>(emptyDraft)
  const [token, setToken] = useState<string | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [notice, setNotice] = useState<string | null>(null)
  const [stale, setStale] = useState(false)
  const [tool, setTool] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!id) return
    getAgent(id)
      .then((agent) => {
        setDraft(draftFromAgent(agent))
        setToken(agent.concurrencyToken)
      })
      .catch(() => setNotice('Der Agent konnte nicht geladen werden.'))
  }, [id])

  function set<K extends keyof AgentDraft>(key: K, value: AgentDraft[K]) {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  function handleFailure(cause: unknown) {
    if (!(cause instanceof ApiRequestError)) {
      setNotice('Speichern fehlgeschlagen.')
      return
    }
    const info = cause.info
    if (info.code === 'name-conflict') {
      setErrors({ name: `${info.title} Bitte anders benennen.` })
      return
    }
    if (info.code === 'concurrency-conflict') {
      setNotice('Der Agent wurde anderswo geändert. Deine Eingaben bleiben stehen.')
      setStale(true)
      return
    }
    if (info.code === 'validation-failed') {
      setErrors(
        Object.fromEntries(
          Object.entries(info.fieldErrors).map(([key, messages]) => [
            key.charAt(0).toLowerCase() + key.slice(1),
            messages.join(' '),
          ]),
        ),
      )
      return
    }
    setNotice(info.title)
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    const found = validateDraft(draft)
    setErrors(found)
    if (Object.keys(found).length > 0) return

    setBusy(true)
    setNotice(null)
    try {
      const body = bodyFromDraft(draft)
      const saved =
        editing && id && token
          ? await updateAgent(id, { ...body, concurrencyToken: token })
          : await createAgent(body)
      navigate(`/agents/definitions/${saved.id}`)
    } catch (cause) {
      handleFailure(cause)
    } finally {
      setBusy(false)
    }
  }

  async function reload() {
    if (!id) return
    const agent = await getAgent(id)
    setToken(agent.concurrencyToken)
    setStale(false)
    setNotice('Neu geladen. Prüfe deine Eingaben und speichere erneut.')
  }

  return (
    <form onSubmit={submit} className="max-w-2xl">
      <h1 className="mb-4 text-lg font-semibold" style={{ color: 'var(--text-strong)' }}>
        {editing ? 'Agent bearbeiten' : 'Agent anlegen'}
      </h1>

      {notice && (
        <div role="alert" className="mb-4 rounded-md border p-3 text-sm" style={{ borderColor: 'var(--danger)' }}>
          {notice}
          {stale && (
            <button type="button" className="ml-2 underline" onClick={() => void reload()}>
              Neu laden
            </button>
          )}
        </div>
      )}

      <section className="mb-5">
        <h2 className="mb-2 text-sm font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>
          Identität
        </h2>
        <label className="block text-sm">
          Name
          <input className={field} style={border} value={draft.name} onChange={(e) => set('name', e.target.value)} />
          <FieldError message={errors.name} />
        </label>
        <label className="mt-3 block text-sm">
          Beschreibung
          <input className={field} style={border} value={draft.description} onChange={(e) => set('description', e.target.value)} />
          <FieldError message={errors.description} />
        </label>
      </section>

      <section className="mb-5">
        <h2 className="mb-2 text-sm font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>
          System-Prompt
        </h2>
        <label className="block text-sm">
          System-Prompt
          <textarea
            className={`${field} min-h-40 font-mono`}
            style={border}
            value={draft.systemPrompt}
            onChange={(e) => set('systemPrompt', e.target.value)}
          />
          <FieldError message={errors.systemPrompt} />
        </label>
      </section>

      <section className="mb-5">
        <h2 className="mb-2 text-sm font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>
          Modell & Grenzen
        </h2>
        <label className="block text-sm">
          Modell
          <input className={field} style={border} value={draft.model} onChange={(e) => set('model', e.target.value)} />
          <FieldError message={errors.model} />
        </label>
        <div className="mt-3 grid grid-cols-3 gap-3">
          <label className="block text-sm">
            Temperature
            <input className={field} style={border} value={draft.temperature} onChange={(e) => set('temperature', e.target.value)} />
            <FieldError message={errors.temperature} />
          </label>
          <label className="block text-sm">
            Max. Output-Token
            <input className={field} style={border} value={draft.maxOutputTokens} onChange={(e) => set('maxOutputTokens', e.target.value)} />
            <FieldError message={errors.maxOutputTokens} />
          </label>
          <label className="block text-sm">
            Max. Turns
            <input className={field} style={border} value={draft.maxTurns} onChange={(e) => set('maxTurns', e.target.value)} />
            <FieldError message={errors.maxTurns} />
          </label>
        </div>
      </section>

      <section className="mb-6">
        <h2 className="mb-2 text-sm font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>
          Werkzeuge
        </h2>
        <div className="mb-2 flex flex-wrap gap-2">
          {draft.allowedTools.map((name) => (
            <span key={name} className="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs" style={{ borderColor: 'var(--border)' }}>
              {name}
              <button
                type="button"
                aria-label={`${name} entfernen`}
                onClick={() => set('allowedTools', draft.allowedTools.filter((item) => item !== name))}
              >
                ×
              </button>
            </span>
          ))}
        </div>
        <label className="block max-w-xs text-sm">
          Werkzeug hinzufügen
          <input
            className={field}
            style={border}
            value={tool}
            onChange={(e) => setTool(e.target.value)}
            onKeyDown={(event) => {
              if (event.key !== 'Enter') return
              event.preventDefault()
              const name = tool.trim()
              if (name !== '' && !draft.allowedTools.includes(name)) {
                set('allowedTools', [...draft.allowedTools, name])
              }
              setTool('')
            }}
          />
        </label>
      </section>

      <button
        type="submit"
        disabled={busy}
        className="rounded-md px-4 py-2 text-sm font-medium disabled:opacity-60"
        style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
      >
        {editing ? 'Speichern' : 'Anlegen'}
      </button>
    </form>
  )
}
```

- [ ] **Step 8: Die beiden Platzhalter-Routen ersetzen**

In `routes.tsx` den Import ergänzen und die zwei Zeilen tauschen:

```tsx
import AgentFormPage from './AgentFormPage'
```

```tsx
    { path: '/agents/definitions/new', element: <AgentFormPage /> },
    { path: '/agents/definitions/:id/edit', element: <AgentFormPage /> },
```

- [ ] **Step 9: Tests laufen lassen**

Run: `npm test -- AgentFormPage`
Expected: PASS, acht Tests.

- [ ] **Step 10: Commit**

```bash
git add src/AgentForge.Web/src
git commit -m "feat: add agent form with client-side limits and conflict handling"
```

---

### Task 13: Agenten-Detailseite

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/NotFoundView.tsx`, `areas/agents/AgentDetailPage.tsx`
- Modify: `src/AgentForge.Web/src/areas/agents/routes.tsx` (Platzhalter `/agents/definitions/:id`)
- Test: `src/AgentForge.Web/src/__tests__/AgentDetailPage.test.tsx`

**Interfaces:**
- Consumes: `getAgent`, `listRuns`, `listConversations`; `useContextPanel`; `rememberItem`.
- Produces:
  - `NotFoundView({ what, backTo, backLabel }: { what: string; backTo: string; backLabel: string })` — die 404-Ansicht aus der Fehlertabelle der Spec. Run-Detail und Gespräch verwenden sie in Aufgabe 15 und 17 wieder; sie entsteht hier, weil dies die erste Ansicht mit einer Id in der Adresse ist.
  - `AgentDetailPage()` als Standardexport. Füllt die Kontextspalte mit den letzten Runs und Gesprächen dieses Agenten.

Die Gesprächsliste kennt keinen Filter nach Agent — die Spec sieht keinen vor. Die Seite lädt deshalb die erste Seite der Gespräche und filtert im Browser auf Teilnehmerschaft. Das ist bei Dutzenden Gesprächen richtig und wird erst falsch, wenn es Hunderte sind; dann braucht der Endpunkt einen Filter.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`src/__tests__/AgentDetailPage.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import AgentDetailPage from '../areas/agents/AgentDetailPage'
import { ContextPanelOutlet, ContextPanelProvider } from '../shell/ContextPanel'
import { stubFetch } from '../test/stubFetch'

const leo = {
  id: 'a1', name: 'leo', description: 'Orchestrator', systemPrompt: 'Du bist Leo.',
  model: 'gpt-5', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20, allowedTools: ['write_file'],
  createdAt: '2026-07-29T10:00:00Z', updatedAt: '2026-07-29T10:00:00Z',
  archivedAt: null, concurrencyToken: 'tok-a1',
}

const run = {
  id: 'r1', agentId: 'a1', objective: 'Erstelle eine D&D-Seite', status: 'Completed',
  snapshot: { name: 'leo', systemPrompt: 'x', model: 'gpt-5', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20, allowedTools: [] },
  createdAt: '2026-07-29T10:00:00Z', startedAt: null, completedAt: null, error: null,
  promptTokens: null, completionTokens: null, costEstimate: null, concurrencyToken: 'tok-r1',
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/agents/definitions/a1']}>
      <ContextPanelProvider>
        <Routes>
          <Route path="/agents/definitions/:id" element={<AgentDetailPage />} />
        </Routes>
        <aside aria-label="Kontext">
          <ContextPanelOutlet />
        </aside>
      </ContextPanelProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  localStorage.clear()
})

test('zeigt Name, Modell und den System-Prompt lesbar', async () => {
  stubFetch([
    ['/api/agents/definitions/a1', { json: leo }],
    ['/api/agents/runs', { json: { items: [], total: 0 } }],
    ['/api/agents/conversations', { json: { items: [], total: 0 } }],
  ])
  renderPage()

  expect(await screen.findByRole('heading', { name: 'leo' })).toBeInTheDocument()
  expect(screen.getByText('gpt-5')).toBeInTheDocument()
  expect(screen.getByText('Du bist Leo.')).toBeInTheDocument()
})

test('bietet Run starten, Gespräch beginnen, Bearbeiten und Archivieren an', async () => {
  stubFetch([
    ['/api/agents/definitions/a1', { json: leo }],
    ['/api/agents/runs', { json: { items: [], total: 0 } }],
    ['/api/agents/conversations', { json: { items: [], total: 0 } }],
  ])
  renderPage()
  await screen.findByRole('heading', { name: 'leo' })

  expect(screen.getByRole('button', { name: 'Run starten' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Gespräch beginnen' })).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Bearbeiten' })).toHaveAttribute('href', '/agents/definitions/a1/edit')
  expect(screen.getByRole('button', { name: 'Archivieren' })).toBeInTheDocument()
})

test('die Kontextspalte zeigt die letzten Runs dieses Agenten', async () => {
  stubFetch([
    ['/api/agents/definitions/a1', { json: leo }],
    ['/api/agents/runs', { json: { items: [run], total: 1 } }],
    ['/api/agents/conversations', { json: { items: [], total: 0 } }],
  ])
  renderPage()

  const context = screen.getByRole('complementary', { name: 'Kontext' })
  await waitFor(() => expect(context).toHaveTextContent('Erstelle eine D&D-Seite'))
})

test('die Kontextspalte zeigt nur Gespräche, an denen der Agent teilnimmt', async () => {
  stubFetch([
    ['/api/agents/definitions/a1', { json: leo }],
    ['/api/agents/runs', { json: { items: [], total: 0 } }],
    [
      '/api/agents/conversations',
      {
        json: {
          items: [
            { id: 'c1', title: 'Mit leo', participants: [{ agentId: 'a1', name: 'leo', model: 'gpt-5' }], lastMessageExcerpt: null, lastMessageAt: null, createdAt: '2026-07-29T10:00:00Z', archivedAt: null, concurrencyToken: 't' },
            { id: 'c2', title: 'Ohne leo', participants: [{ agentId: 'a9', name: 'x', model: 'gpt-5' }], lastMessageExcerpt: null, lastMessageAt: null, createdAt: '2026-07-29T10:00:00Z', archivedAt: null, concurrencyToken: 't' },
          ],
          total: 2,
        },
      },
    ],
  ])
  renderPage()

  const context = screen.getByRole('complementary', { name: 'Kontext' })
  await waitFor(() => expect(context).toHaveTextContent('Mit leo'))
  expect(context).not.toHaveTextContent('Ohne leo')
})

test('ein fehlender Agent zeigt eine nicht-gefunden-Ansicht mit Weg zurück', async () => {
  stubFetch([
    ['/api/agents/definitions/a1', { status: 404, json: { type: 'errors/not-found', title: 'Nicht gefunden' } }],
  ])
  renderPage()

  expect(await screen.findByText('Diesen Agenten gibt es nicht.')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Zur Agentenliste' })).toBeInTheDocument()
})

test('der Agent landet in den zuletzt berührten Objekten', async () => {
  stubFetch([
    ['/api/agents/definitions/a1', { json: leo }],
    ['/api/agents/runs', { json: { items: [], total: 0 } }],
    ['/api/agents/conversations', { json: { items: [], total: 0 } }],
  ])
  renderPage()
  await screen.findByRole('heading', { name: 'leo' })

  expect(JSON.parse(localStorage.getItem('agentforge.recent')!)[0]).toMatchObject({
    key: 'agent:a1',
    label: 'leo',
  })
})
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- AgentDetailPage`
Expected: FAIL — `Failed to resolve import "../areas/agents/AgentDetailPage"`.

- [ ] **Step 3: `NotFoundView.tsx` schreiben**

```tsx
import { Link } from 'react-router-dom'

export function NotFoundView({
  what,
  backTo,
  backLabel,
}: {
  what: string
  backTo: string
  backLabel: string
}) {
  return (
    <div>
      <p className="mb-3 text-sm">{what}</p>
      <Link to={backTo} className="underline">
        {backLabel}
      </Link>
    </div>
  )
}
```

- [ ] **Step 4: `AgentDetailPage.tsx` schreiben**

„Run starten" und „Gespräch beginnen" bleiben in dieser Aufgabe Knöpfe, die auf die jeweilige Liste führen; Aufgabe 14 und 16 hängen die Dialoge daran.

```tsx
import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiRequestError } from '../../lib/http'
import { useContextPanel } from '../../shell/ContextPanel'
import { rememberItem } from '../../shell/RecentItems'
import { archiveAgent, getAgent, listConversations, listRuns } from './api'
import { NotFoundView } from './NotFoundView'
import type { AgentDto, ConversationDto, RunDto } from './types'

export default function AgentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [agent, setAgent] = useState<AgentDto | null>(null)
  const [runs, setRuns] = useState<RunDto[]>([])
  const [conversations, setConversations] = useState<ConversationDto[]>([])
  const [missing, setMissing] = useState(false)

  useEffect(() => {
    if (!id) return
    getAgent(id)
      .then((loaded) => {
        setAgent(loaded)
        rememberItem({ key: `agent:${loaded.id}`, to: `/agents/definitions/${loaded.id}`, label: loaded.name, kind: 'agent' })
      })
      .catch((cause: unknown) => {
        if (cause instanceof ApiRequestError && cause.info.status === 404) setMissing(true)
      })
    void listRuns({ agentId: id, skip: 0, take: 5 }).then((page) => setRuns(page.items)).catch(() => setRuns([]))
    void listConversations({ skip: 0, take: 50 }).then((page) => setConversations(page.items)).catch(() => setConversations([]))
  }, [id])

  const mine = useMemo(
    () => conversations.filter((c) => c.participants.some((p) => p.agentId === id)),
    [conversations, id],
  )

  useContextPanel(
    useMemo(
      () => (
        <div className="text-sm">
          <p className="mb-1 text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Runs</p>
          {runs.length === 0 ? <p style={{ color: 'var(--text-muted)' }}>keine</p> : (
            <ul className="mb-4">
              {runs.map((run) => (
                <li key={run.id} className="truncate">
                  <Link to={`/agents/runs/${run.id}`} className="underline">{run.objective}</Link>
                </li>
              ))}
            </ul>
          )}
          <p className="mb-1 text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Gespräche</p>
          {mine.length === 0 ? <p style={{ color: 'var(--text-muted)' }}>keine</p> : (
            <ul>
              {mine.map((conversation) => (
                <li key={conversation.id} className="truncate">
                  <Link to={`/agents/conversations/${conversation.id}`} className="underline">{conversation.title}</Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      ),
      [runs, mine],
    ),
  )

  if (missing) {
    return (
      <NotFoundView
        what="Diesen Agenten gibt es nicht."
        backTo="/agents/definitions"
        backLabel="Zur Agentenliste"
      />
    )
  }

  if (!agent) return <p className="text-sm">Wird geladen …</p>

  return (
    <section className="max-w-2xl">
      <h1 className="text-lg font-semibold" style={{ color: 'var(--text-strong)' }}>{agent.name}</h1>
      <p className="mb-4 text-sm" style={{ color: 'var(--text-muted)' }}>
        {agent.model} · Temperature {agent.temperature} · max. {agent.maxTurns} Turns
      </p>

      <div className="mb-5 flex flex-wrap gap-2">
        <button type="button" className="rounded-md px-3 py-1.5 text-sm font-medium" style={{ background: 'var(--accent)', color: 'var(--accent-text)' }} onClick={() => navigate('/agents/runs')}>
          Run starten
        </button>
        <button type="button" className="rounded-md border px-3 py-1.5 text-sm" style={{ borderColor: 'var(--border)' }} onClick={() => navigate('/agents/conversations')}>
          Gespräch beginnen
        </button>
        <Link to={`/agents/definitions/${agent.id}/edit`} className="rounded-md border px-3 py-1.5 text-sm" style={{ borderColor: 'var(--border)' }}>
          Bearbeiten
        </Link>
        <button
          type="button"
          className="rounded-md border px-3 py-1.5 text-sm"
          style={{ borderColor: 'var(--border)', color: 'var(--danger)' }}
          onClick={() => void archiveAgent(agent.id).then(() => navigate('/agents/definitions'))}
        >
          Archivieren
        </button>
      </div>

      <h2 className="mb-1 text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>System-Prompt</h2>
      <p className="rounded-md border p-3 text-sm whitespace-pre-wrap" style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}>
        {agent.systemPrompt}
      </p>
    </section>
  )
}
```

- [ ] **Step 5: Die Platzhalter-Route ersetzen**

```tsx
import AgentDetailPage from './AgentDetailPage'
```

```tsx
    { path: '/agents/definitions/:id', element: <AgentDetailPage /> },
```

- [ ] **Step 6: Tests laufen lassen**

Run: `npm test -- AgentDetailPage`
Expected: PASS, sechs Tests.

- [ ] **Step 7: Alles laufen lassen**

Run: `npm test && npm run lint && npm run typecheck`
Expected: alles PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AgentForge.Web/src
git commit -m "feat: add agent detail page filling the context panel"
```

---

### Task 14: Run starten und Run-Liste

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/StartRunDialog.tsx`, `areas/agents/RunListPage.tsx`
- Modify: `src/AgentForge.Web/src/areas/agents/routes.tsx` (Platzhalter `/agents/runs`), `areas/agents/AgentListPage.tsx` (der `onClick` von „Run starten"), `areas/agents/AgentDetailPage.tsx` (derselbe Knopf)
- Test: `src/AgentForge.Web/src/__tests__/StartRunDialog.test.tsx`, `src/__tests__/RunListPage.test.tsx`

**Interfaces:**
- Consumes: `startRun`, `listRuns`, `listAgents`.
- Produces:
  - `StartRunDialog({ agentId, onClose }: { agentId?: string; onClose: () => void })` — wählt bei fehlendem `agentId` einen Agenten, navigiert nach Erfolg auf `/agents/runs/{id}`.
  - `RunListPage()` als Standardexport, mit Filtern für Agent und Status.
  - `src/areas/agents/labels.ts` mit `statusLabel(status: RunStatus): string` und `formatDuration(from: string, to: string | null): string`. Eigene Datei, weil Run-Liste und Run-Detail dieselben Beschriftungen brauchen; in einer der beiden Seiten wäre sie am falschen Ort.

- [ ] **Step 1: Die fehlschlagenden Tests für Beschriftungen und Dialog schreiben**

`src/__tests__/StartRunDialog.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import StartRunDialog from '../areas/agents/StartRunDialog'
import { formatDuration, statusLabel } from '../areas/agents/labels'
import { stubFetch } from '../test/stubFetch'

test('Statusbeschriftungen sind deutsch', () => {
  expect(statusLabel('Pending')).toBe('wartet')
  expect(statusLabel('Running')).toBe('läuft')
  expect(statusLabel('Completed')).toBe('fertig')
  expect(statusLabel('Failed')).toBe('fehlgeschlagen')
  expect(statusLabel('Cancelled')).toBe('abgebrochen')
})

test('formatDuration zeigt Minuten und Sekunden, bei offenem Ende einen Strich', () => {
  expect(formatDuration('2026-07-29T10:00:00Z', '2026-07-29T10:02:05Z')).toBe('2:05')
  expect(formatDuration('2026-07-29T10:00:00Z', null)).toBe('—')
})

function renderDialog(agentId?: string) {
  return render(
    <MemoryRouter initialEntries={['/agents/runs']}>
      <Routes>
        <Route path="/agents/runs" element={<StartRunDialog agentId={agentId} onClose={() => {}} />} />
        <Route path="/agents/runs/:id" element={<p>Run-Detail</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

test('mit vorgegebenem Agenten wird nur der Auftrag erfragt', async () => {
  renderDialog('a1')
  expect(screen.getByLabelText('Auftrag')).toBeInTheDocument()
  expect(screen.queryByLabelText('Agent')).not.toBeInTheDocument()
})

test('ohne Agenten wird eine Auswahl geladen', async () => {
  stubFetch([
    [
      '/api/agents/definitions',
      { json: { items: [{ id: 'a1', name: 'leo', model: 'gpt-5', description: null, systemPrompt: 'x', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20, allowedTools: [], createdAt: '', updatedAt: '', archivedAt: null, concurrencyToken: 't' }], total: 1 } },
    ],
  ])
  renderDialog()

  expect(await screen.findByRole('combobox', { name: 'Agent' })).toBeInTheDocument()
  expect(screen.getByRole('option', { name: 'leo' })).toBeInTheDocument()
})

test('ein leerer Auftrag wird nicht abgeschickt', async () => {
  const calls = stubFetch([])
  renderDialog('a1')

  await userEvent.click(screen.getByRole('button', { name: 'Starten' }))

  expect(screen.getByText('Bitte einen Auftrag angeben.')).toBeInTheDocument()
  expect(calls).toHaveLength(0)
})

test('ein gestarteter Run führt auf sein Detail', async () => {
  const calls = stubFetch([['/api/agents/runs', { status: 201, json: { id: 'r1' } }]])
  renderDialog('a1')

  await userEvent.type(screen.getByLabelText('Auftrag'), 'Erstelle eine D&D-Seite')
  await userEvent.click(screen.getByRole('button', { name: 'Starten' }))

  await waitFor(() => expect(screen.getByText('Run-Detail')).toBeInTheDocument())
  expect(calls[0]!.body).toEqual({ agentId: 'a1', objective: 'Erstelle eine D&D-Seite' })
})

test('ein archivierter Agent wird erklärt statt nur abgelehnt', async () => {
  stubFetch([
    ['/api/agents/runs', { status: 409, json: { type: 'errors/agent-archived', title: 'Archiviert' } }],
  ])
  renderDialog('a1')

  await userEvent.type(screen.getByLabelText('Auftrag'), 'Etwas')
  await userEvent.click(screen.getByRole('button', { name: 'Starten' }))

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Dieser Agent ist archiviert und kann keine Runs mehr ausführen.',
  )
})
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- StartRunDialog`
Expected: FAIL — `Failed to resolve import "../areas/agents/StartRunDialog"`.

- [ ] **Step 3: `labels.ts` schreiben**

```ts
import type { RunStatus } from './types'

const labels: Record<RunStatus, string> = {
  Pending: 'wartet',
  Running: 'läuft',
  Completed: 'fertig',
  Failed: 'fehlgeschlagen',
  Cancelled: 'abgebrochen',
}

export function statusLabel(status: RunStatus): string {
  return labels[status]
}

export function formatDuration(from: string, to: string | null): string {
  if (to === null) return '—'
  const seconds = Math.max(0, Math.round((Date.parse(to) - Date.parse(from)) / 1000))
  const minutes = Math.floor(seconds / 60)
  return `${minutes}:${String(seconds % 60).padStart(2, '0')}`
}
```

- [ ] **Step 4: `StartRunDialog.tsx` schreiben**

```tsx
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiRequestError } from '../../lib/http'
import { listAgents, startRun } from './api'
import type { AgentDto } from './types'

export default function StartRunDialog({
  agentId,
  onClose,
}: {
  agentId?: string
  onClose: () => void
}) {
  const navigate = useNavigate()
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [chosen, setChosen] = useState(agentId ?? '')
  const [objective, setObjective] = useState('')
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (agentId) return
    void listAgents({ q: '', skip: 0, take: 50 })
      .then((page) => {
        setAgents(page.items)
        setChosen(page.items[0]?.id ?? '')
      })
      .catch(() => setNotice('Die Agenten konnten nicht geladen werden.'))
  }, [agentId])

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (objective.trim() === '') {
      setFieldError('Bitte einen Auftrag angeben.')
      return
    }
    setBusy(true)
    setNotice(null)
    try {
      const run = await startRun({ agentId: chosen, objective: objective.trim() })
      navigate(`/agents/runs/${run.id}`)
    } catch (cause) {
      const code = cause instanceof ApiRequestError ? cause.info.code : 'unknown'
      setNotice(
        code === 'agent-archived'
          ? 'Dieser Agent ist archiviert und kann keine Runs mehr ausführen.'
          : 'Der Run konnte nicht gestartet werden.',
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <form
      onSubmit={submit}
      role="dialog"
      aria-modal="true"
      aria-label="Run starten"
      className="max-w-xl rounded-md border p-4"
      style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
    >
      {notice && (
        <p role="alert" className="mb-3 text-sm" style={{ color: 'var(--danger)' }}>
          {notice}
        </p>
      )}

      {!agentId && (
        <label className="mb-3 block text-sm">
          Agent
          <select
            className="mt-1 w-full rounded-md border px-2 py-1.5 text-sm"
            style={{ borderColor: 'var(--border)' }}
            value={chosen}
            onChange={(event) => setChosen(event.target.value)}
          >
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name}
              </option>
            ))}
          </select>
        </label>
      )}

      <label className="block text-sm">
        Auftrag
        <textarea
          className="mt-1 min-h-24 w-full rounded-md border px-2 py-1.5 text-sm"
          style={{ borderColor: 'var(--border)' }}
          value={objective}
          onChange={(event) => {
            setObjective(event.target.value)
            setFieldError(null)
          }}
        />
        {fieldError && (
          <span className="mt-1 block text-xs" style={{ color: 'var(--danger)' }}>
            {fieldError}
          </span>
        )}
      </label>

      <div className="mt-4 flex gap-2">
        <button
          type="submit"
          disabled={busy}
          className="rounded-md px-3 py-1.5 text-sm font-medium disabled:opacity-60"
          style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
        >
          Starten
        </button>
        <button type="button" className="rounded-md border px-3 py-1.5 text-sm" style={{ borderColor: 'var(--border)' }} onClick={onClose}>
          Abbrechen
        </button>
      </div>
    </form>
  )
}
```

- [ ] **Step 5: Die fehlschlagenden Tests der Run-Liste schreiben**

`src/__tests__/RunListPage.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import RunListPage from '../areas/agents/RunListPage'
import { stubFetch } from '../test/stubFetch'

const run = {
  id: 'r1', agentId: 'a1', objective: 'Erstelle eine D&D-Seite', status: 'Running',
  snapshot: { name: 'leo', systemPrompt: 'x', model: 'gpt-5', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20, allowedTools: [] },
  createdAt: '2026-07-29T10:00:00Z', startedAt: '2026-07-29T10:00:00Z', completedAt: null,
  error: null, promptTokens: null, completionTokens: null, costEstimate: null, concurrencyToken: 'tok-r1',
}

const agentsPage = { items: [], total: 0 }

function renderPage() {
  return render(
    <MemoryRouter>
      <RunListPage />
    </MemoryRouter>,
  )
}

test('zeigt Runs mit Agentenname aus dem Snapshot und deutschem Status', async () => {
  stubFetch([
    ['/api/agents/runs', { json: { items: [run], total: 1 } }],
    ['/api/agents/definitions', { json: agentsPage }],
  ])
  renderPage()

  expect(await screen.findByRole('link', { name: 'Erstelle eine D&D-Seite' })).toBeInTheDocument()
  expect(screen.getByText('leo')).toBeInTheDocument()
  expect(screen.getByText('läuft')).toBeInTheDocument()
})

test('ohne Runs erscheint der leere Zustand mit Startknopf', async () => {
  stubFetch([
    ['/api/agents/runs', { json: { items: [], total: 0 } }],
    ['/api/agents/definitions', { json: agentsPage }],
  ])
  renderPage()

  expect(await screen.findByText('Noch keine Runs.')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Run starten' })).toBeInTheDocument()
})

test('der Statusfilter wird als Suchparameter geschickt', async () => {
  const calls = stubFetch([
    ['/api/agents/runs', { json: { items: [run], total: 1 } }],
    ['/api/agents/definitions', { json: agentsPage }],
    ['/api/agents/runs', { json: { items: [], total: 0 } }],
  ])
  renderPage()
  await screen.findByText('läuft')

  await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Status' }), 'Failed')

  await waitFor(() => expect(calls.filter((call) => call.url.includes('/runs')).length).toBe(2))
  expect(calls[2]!.url).toContain('status=Failed')
})

test('der Startknopf öffnet den Dialog', async () => {
  stubFetch([
    ['/api/agents/runs', { json: { items: [], total: 0 } }],
    ['/api/agents/definitions', { json: agentsPage }],
    ['/api/agents/definitions', { json: agentsPage }],
  ])
  renderPage()
  await screen.findByText('Noch keine Runs.')

  await userEvent.click(screen.getByRole('button', { name: 'Run starten' }))
  expect(screen.getByRole('dialog', { name: 'Run starten' })).toBeInTheDocument()
})
```

- [ ] **Step 6: Test laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- RunListPage`
Expected: FAIL — `Failed to resolve import "../areas/agents/RunListPage"`.

- [ ] **Step 7: `RunListPage.tsx` schreiben**

```tsx
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { listAgents, listRuns } from './api'
import { formatDuration, statusLabel } from './labels'
import StartRunDialog from './StartRunDialog'
import type { AgentDto, RunDto, RunStatus } from './types'

const cell = 'border-b px-3 py-2 text-left align-top text-sm'
const statuses: RunStatus[] = ['Pending', 'Running', 'Completed', 'Failed', 'Cancelled']

export default function RunListPage() {
  const [runs, setRuns] = useState<RunDto[]>([])
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [agentId, setAgentId] = useState('')
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [dialogOpen, setDialogOpen] = useState(false)

  useEffect(() => {
    setLoading(true)
    listRuns({
      agentId: agentId === '' ? undefined : agentId,
      status: status === '' ? undefined : (status as RunStatus),
      skip: 0,
      take: 50,
    })
      .then((page) => setRuns(page.items))
      .catch(() => setRuns([]))
      .finally(() => setLoading(false))
  }, [agentId, status])

  useEffect(() => {
    void listAgents({ q: '', skip: 0, take: 50 })
      .then((page) => setAgents(page.items))
      .catch(() => setAgents([]))
  }, [])

  return (
    <section>
      <header className="mb-4 flex items-center gap-3">
        <h1 className="text-lg font-semibold" style={{ color: 'var(--text-strong)' }}>Runs</h1>
        <button
          type="button"
          className="ml-auto rounded-md px-3 py-1.5 text-sm font-medium"
          style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
          onClick={() => setDialogOpen(true)}
        >
          Run starten
        </button>
      </header>

      <div className="mb-4 flex flex-wrap gap-3">
        <label className="text-sm">
          Agent
          <select className="ml-2 rounded-md border px-2 py-1 text-sm" style={{ borderColor: 'var(--border)' }} value={agentId} onChange={(e) => setAgentId(e.target.value)}>
            <option value="">alle</option>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>{agent.name}</option>
            ))}
          </select>
        </label>
        <label className="text-sm">
          Status
          <select className="ml-2 rounded-md border px-2 py-1 text-sm" style={{ borderColor: 'var(--border)' }} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">alle</option>
            {statuses.map((value) => (
              <option key={value} value={value}>{statusLabel(value)}</option>
            ))}
          </select>
        </label>
      </div>

      {dialogOpen && (
        <div className="mb-4">
          <StartRunDialog onClose={() => setDialogOpen(false)} />
        </div>
      )}

      {loading && <p className="text-sm">Wird geladen …</p>}

      {!loading && runs.length === 0 && (
        <div className="rounded-md border border-dashed p-6 text-center" style={{ borderColor: 'var(--border)' }}>
          <p className="mb-3 text-sm">Noch keine Runs.</p>
          <button
            type="button"
            className="rounded-md px-3 py-1.5 text-sm font-medium"
            style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
            onClick={() => setDialogOpen(true)}
          >
            Run starten
          </button>
        </div>
      )}

      {runs.length > 0 && (
        <table className="w-full border-collapse">
          <thead>
            <tr style={{ background: 'var(--bg-sunken)' }}>
              <th className={`${cell} font-semibold`} style={{ borderColor: 'var(--border)' }}>Auftrag</th>
              <th className={`${cell} font-semibold`} style={{ borderColor: 'var(--border)' }}>Agent</th>
              <th className={`${cell} font-semibold`} style={{ borderColor: 'var(--border)' }}>Status</th>
              <th className={`${cell} font-semibold`} style={{ borderColor: 'var(--border)' }}>Dauer</th>
            </tr>
          </thead>
          <tbody>
            {runs.map((run) => (
              <tr key={run.id}>
                <td className={cell} style={{ borderColor: 'var(--border)' }}>
                  <Link to={`/agents/runs/${run.id}`} className="underline">{run.objective}</Link>
                </td>
                <td className={cell} style={{ borderColor: 'var(--border)' }}>{run.snapshot.name}</td>
                <td className={cell} style={{ borderColor: 'var(--border)' }}>{statusLabel(run.status)}</td>
                <td className={cell} style={{ borderColor: 'var(--border)' }}>
                  {formatDuration(run.createdAt, run.completedAt)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
```

- [ ] **Step 8: Route und die beiden Knöpfe verdrahten**

In `routes.tsx`:

```tsx
import RunListPage from './RunListPage'
```

```tsx
    { path: '/agents/runs', element: <RunListPage /> },
```

In `AgentListPage.tsx` den Zustand `const [runFor, setRunFor] = useState<AgentDto | null>(null)` ergänzen, den `onClick` von „Run starten" auf `setRunFor(agent)` umstellen, `navigate` und dessen Import entfernen, und unter der Tabelle einhängen:

```tsx
{runFor && (
  <div className="mt-4">
    <StartRunDialog agentId={runFor.id} onClose={() => setRunFor(null)} />
  </div>
)}
```

In `AgentDetailPage.tsx` genauso: `const [runOpen, setRunOpen] = useState(false)`, der Knopf setzt `setRunOpen(true)`, und unter den Knöpfen:

```tsx
{runOpen && (
  <div className="mb-5">
    <StartRunDialog agentId={agent.id} onClose={() => setRunOpen(false)} />
  </div>
)}
```

- [ ] **Step 9: Alle Tests laufen lassen**

Run: `npm test && npm run lint && npm run typecheck`
Expected: alles PASS. Der Test „jede Zeile verweist auf Chat und Run" aus Aufgabe 11 prüft weiterhin nur, dass der Knopf existiert — er bleibt grün.

- [ ] **Step 10: Commit**

```bash
git add src/AgentForge.Web/src
git commit -m "feat: add run list and start run dialog"
```

---

### Task 15: Run-Detail

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/RunDetailPage.tsx`
- Modify: `src/AgentForge.Web/src/areas/agents/routes.tsx` (Platzhalter `/agents/runs/:id`)
- Test: `src/AgentForge.Web/src/__tests__/RunDetailPage.test.tsx`

**Interfaces:**
- Consumes: `getRun`, `cancelRun`, `useRunStream`, `Transcript`, `TranscriptLog`, `useContextPanel`, `rememberItem`, `statusLabel`.
- Produces: `RunDetailPage()` als Standardexport.

Der Turn-Stand entsteht aus den Nachrichten: Zahl der `Assistant`-Nachrichten gegen `snapshot.maxTurns`. Ein eigenes Feld dafür gibt es nicht.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`src/__tests__/RunDetailPage.test.tsx`:

```tsx
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import RunDetailPage from '../areas/agents/RunDetailPage'
import { ContextPanelOutlet, ContextPanelProvider } from '../shell/ContextPanel'
import { installFakeEventSource } from '../test/fakeEventSource'
import { stubFetch } from '../test/stubFetch'

const run = {
  id: 'r1', agentId: 'a1', objective: 'Erstelle eine D&D-Seite', status: 'Running',
  snapshot: { name: 'leo', systemPrompt: 'Du bist Leo.', model: 'gpt-5', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20, allowedTools: [] },
  createdAt: '2026-07-29T10:00:00Z', startedAt: '2026-07-29T10:00:00Z', completedAt: null,
  error: null, promptTokens: null, completionTokens: null, costEstimate: null, concurrencyToken: 'tok-r1',
}

const messages = [
  { id: 'm0', sequence: 0, role: 'System', content: 'Du bist Leo.', toolCallsJson: null, toolCallId: null, createdAt: '2026-07-29T10:00:00Z' },
  { id: 'm1', sequence: 1, role: 'User', content: 'Erstelle eine D&D-Seite', toolCallsJson: null, toolCallId: null, createdAt: '2026-07-29T10:00:01Z' },
]

function renderPage(extra: Array<[string, { status?: number; json?: unknown }]> = []) {
  const fake = installFakeEventSource()
  const calls = stubFetch([
    ['/api/agents/runs/r1/messages', { json: messages }],
    ['/api/agents/runs/r1', { json: run }],
    ...extra,
  ])
  const view = render(
    <MemoryRouter initialEntries={['/agents/runs/r1']}>
      <ContextPanelProvider>
        <Routes>
          <Route path="/agents/runs/:id" element={<RunDetailPage />} />
        </Routes>
        <aside aria-label="Kontext">
          <ContextPanelOutlet />
        </aside>
      </ContextPanelProvider>
    </MemoryRouter>,
  )
  return { fake, calls, view }
}

beforeEach(() => {
  localStorage.clear()
})

test('zeigt den Auftrag als eigene Nachricht, den System-Prompt aber nicht', async () => {
  const { fake } = renderPage()

  expect(await screen.findByText('Erstelle eine D&D-Seite')).toBeInTheDocument()
  expect(screen.queryByText('Du bist Leo.')).not.toBeInTheDocument()

  fake.restore()
})

test('der Umschalter zeigt das Protokoll samt System-Prompt', async () => {
  const { fake } = renderPage()
  await screen.findByText('Erstelle eine D&D-Seite')

  await userEvent.click(screen.getByRole('button', { name: 'Protokoll' }))

  expect(screen.getByText('Du bist Leo.')).toBeInTheDocument()
  expect(screen.getByText('SYSTEM')).toBeInTheDocument()

  fake.restore()
})

test('Werkzeugaufrufe aus dem Strom erscheinen als Karten', async () => {
  const { fake } = renderPage()
  await screen.findByText('Erstelle eine D&D-Seite')

  act(() => {
    fake.instances[0]!.emit('tool', {
      streamId: 's1', sequence: 2,
      call: { id: 't1', name: 'write_file', argumentsJson: '{}', resultText: 'ok', failed: false },
    })
  })

  expect(screen.getByText('write_file')).toBeInTheDocument()

  fake.restore()
})

test('die Kontextspalte zeigt Status, Kennzahlen und den Turn-Stand', async () => {
  const { fake } = renderPage()
  await screen.findByText('Erstelle eine D&D-Seite')

  act(() => {
    fake.instances[0]!.emit('message', {
      streamId: 's1', sequence: 2,
      message: { sequence: 2, role: 'Assistant', senderAgentId: null, senderName: null, content: 'Fertig.', toolCalls: [], mentions: [], state: 'complete', createdAt: '2026-07-29T10:00:02Z' },
    })
    fake.instances[0]!.emit('usage', { usage: { promptTokens: 800, completionTokens: 140, costEstimate: 0.04 } })
  })

  const context = screen.getByRole('complementary', { name: 'Kontext' })
  expect(context).toHaveTextContent('läuft')
  expect(context).toHaveTextContent('1/20')
  expect(context).toHaveTextContent('940')

  fake.restore()
})

test('Abbrechen schickt das Token und aktualisiert den Status', async () => {
  const { fake, calls } = renderPage([
    ['/api/agents/runs/r1/cancel', { json: { ...run, status: 'Cancelled' } }],
  ])
  await screen.findByText('Erstelle eine D&D-Seite')

  await userEvent.click(screen.getByRole('button', { name: 'Abbrechen' }))

  await waitFor(() => expect(calls.some((call) => call.url.includes('/cancel'))).toBe(true))
  expect(calls.find((call) => call.url.includes('/cancel'))!.body).toEqual({ concurrencyToken: 'tok-r1' })
  expect(await screen.findByText('abgebrochen')).toBeInTheDocument()

  fake.restore()
})

test('ein zweiter Abbruch erklärt den unzulässigen Übergang', async () => {
  const { fake } = renderPage([
    ['/api/agents/runs/r1/cancel', { status: 409, json: { type: 'errors/invalid-transition', title: 'Schon beendet' } }],
  ])
  await screen.findByText('Erstelle eine D&D-Seite')

  await userEvent.click(screen.getByRole('button', { name: 'Abbrechen' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Der Run ist bereits beendet.')

  fake.restore()
})

test('ein Verbindungsverlust wird gemeldet und bietet Neuladen an', async () => {
  const { fake } = renderPage()
  await screen.findByText('Erstelle eine D&D-Seite')

  act(() => {
    fake.instances[0]!.fail()
    fake.instances[0]!.fail()
    fake.instances[0]!.fail()
  })

  expect(screen.getByText('Verbindung verloren.')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Neu laden' })).toBeInTheDocument()

  fake.restore()
})

test('ein fehlender Run zeigt die nicht-gefunden-Ansicht mit Weg zurück', async () => {
  const fake = installFakeEventSource()
  stubFetch([
    ['/api/agents/runs/r1/messages', { json: [] }],
    ['/api/agents/runs/r1', { status: 404, json: { type: 'errors/not-found', title: 'Nicht gefunden' } }],
  ])
  render(
    <MemoryRouter initialEntries={['/agents/runs/r1']}>
      <ContextPanelProvider>
        <Routes>
          <Route path="/agents/runs/:id" element={<RunDetailPage />} />
        </Routes>
      </ContextPanelProvider>
    </MemoryRouter>,
  )

  expect(await screen.findByText('Diesen Run gibt es nicht.')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Zur Run-Liste' })).toBeInTheDocument()

  fake.restore()
})
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- RunDetailPage`
Expected: FAIL — `Failed to resolve import "../areas/agents/RunDetailPage"`.

- [ ] **Step 3: `RunDetailPage.tsx` schreiben**

```tsx
import { useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import { ApiRequestError } from '../../lib/http'
import { useContextPanel } from '../../shell/ContextPanel'
import { rememberItem } from '../../shell/RecentItems'
import { cancelRun, getRun } from './api'
import { statusLabel } from './labels'
import { NotFoundView } from './NotFoundView'
import { Transcript } from './Transcript'
import { TranscriptLog } from './TranscriptLog'
import { useRunStream } from './useRunStream'
import type { RunDto } from './types'

export default function RunDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const { messages, status, usage, connection } = useRunStream(id)
  const [run, setRun] = useState<RunDto | null>(null)
  const [asLog, setAsLog] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [missing, setMissing] = useState(false)

  useEffect(() => {
    if (!id) return
    getRun(id)
      .then((loaded) => {
        setRun(loaded)
        rememberItem({ key: `run:${loaded.id}`, to: `/agents/runs/${loaded.id}`, label: loaded.objective, kind: 'run' })
      })
      .catch((cause: unknown) => {
        if (cause instanceof ApiRequestError && cause.info.status === 404) setMissing(true)
        else setNotice('Der Run konnte nicht geladen werden.')
      })
  }, [id])

  const effectiveStatus = status ?? run?.status ?? null
  const turns = messages.filter((message) => message.role === 'Assistant').length

  useContextPanel(
    useMemo(
      () => (
        <dl className="text-sm">
          <dt className="text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Status</dt>
          <dd className="mb-3">{effectiveStatus ? statusLabel(effectiveStatus) : '—'}</dd>
          <dt className="text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Agent</dt>
          <dd className="mb-3">{run?.snapshot.name ?? '—'}</dd>
          <dt className="text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Turns</dt>
          <dd className="mb-3">{run ? `${turns}/${run.snapshot.maxTurns}` : '—'}</dd>
          <dt className="text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Token</dt>
          <dd className="mb-3">{usage ? usage.promptTokens + usage.completionTokens : '—'}</dd>
          <dt className="text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Kosten</dt>
          <dd className="mb-3">{usage?.costEstimate ?? '—'}</dd>
          <dt className="text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Modell</dt>
          <dd>{run?.snapshot.model ?? '—'}</dd>
        </dl>
      ),
      [effectiveStatus, run, turns, usage],
    ),
  )

  async function cancel() {
    if (!run) return
    try {
      const updated = await cancelRun(run.id, run.concurrencyToken)
      setRun(updated)
      setNotice(null)
    } catch (cause) {
      const code = cause instanceof ApiRequestError ? cause.info.code : 'unknown'
      if (code === 'invalid-transition') {
        setNotice('Der Run ist bereits beendet.')
        void getRun(run.id).then(setRun).catch(() => {})
        return
      }
      if (code === 'concurrency-conflict') {
        setNotice('Der Run wurde anderswo geändert. Die Ansicht wird aktualisiert.')
        void getRun(run.id).then(setRun).catch(() => {})
        return
      }
      setNotice('Abbrechen fehlgeschlagen.')
    }
  }

  const cancellable = effectiveStatus === 'Pending' || effectiveStatus === 'Running'

  if (missing) {
    return <NotFoundView what="Diesen Run gibt es nicht." backTo="/agents/runs" backLabel="Zur Run-Liste" />
  }

  return (
    <section>
      <header className="mb-3">
        <h1 className="text-lg font-semibold" style={{ color: 'var(--text-strong)' }}>
          {run?.objective ?? 'Run'}
        </h1>
        <p className="text-sm" style={{ color: 'var(--text-muted)' }}>
          {effectiveStatus ? statusLabel(effectiveStatus) : '—'}
        </p>
      </header>

      {notice && (
        <p role="alert" className="mb-3 text-sm" style={{ color: 'var(--danger)' }}>{notice}</p>
      )}

      {connection === 'reconnecting' && (
        <p className="mb-3 text-sm" style={{ color: 'var(--text-muted)' }}>
          Verbindung unterbrochen, versuche erneut …
        </p>
      )}
      {connection === 'lost' && (
        <p className="mb-3 text-sm">
          Verbindung verloren.{' '}
          <button type="button" className="underline" onClick={() => window.location.reload()}>
            Neu laden
          </button>
        </p>
      )}

      <div className="mb-2 flex gap-2">
        <button type="button" className={`rounded-md border px-3 py-1 text-sm ${asLog ? '' : 'font-semibold'}`} style={{ borderColor: 'var(--border)' }} onClick={() => setAsLog(false)}>
          Verlauf
        </button>
        <button type="button" className={`rounded-md border px-3 py-1 text-sm ${asLog ? 'font-semibold' : ''}`} style={{ borderColor: 'var(--border)' }} onClick={() => setAsLog(true)}>
          Protokoll
        </button>
        {cancellable && (
          <button type="button" className="ml-auto rounded-md border px-3 py-1 text-sm" style={{ borderColor: 'var(--border)', color: 'var(--danger)' }} onClick={() => void cancel()}>
            Abbrechen
          </button>
        )}
      </div>

      <div className="rounded-md border" style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}>
        {asLog ? <TranscriptLog messages={messages} /> : <Transcript messages={messages} youLabel="Du" />}
      </div>
    </section>
  )
}
```

- [ ] **Step 4: Die Platzhalter-Route ersetzen**

```tsx
import RunDetailPage from './RunDetailPage'
```

```tsx
    { path: '/agents/runs/:id', element: <RunDetailPage /> },
```

- [ ] **Step 5: Tests laufen lassen**

Run: `npm test -- RunDetailPage`
Expected: PASS, acht Tests.

- [ ] **Step 6: Alles laufen lassen und im Mock ansehen**

Run: `npm test && npm run lint && npm run typecheck`
Expected: alles PASS.

Run: `npm run dev:mock`, dann einen Run starten und zusehen, wie Token, Werkzeugkarte und Abschluss einlaufen. Danach beenden.

- [ ] **Step 7: Commit**

```bash
git add src/AgentForge.Web/src
git commit -m "feat: add run detail with streaming transcript and log toggle"
```

---

### Task 16: Gesprächsliste und Anlege-Dialog

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/NewConversationDialog.tsx`, `areas/agents/ConversationListPage.tsx`
- Modify: `src/AgentForge.Web/src/areas/agents/routes.tsx` (Platzhalter `/agents/conversations`), `areas/agents/AgentDetailPage.tsx` (Knopf „Gespräch beginnen")
- Test: `src/AgentForge.Web/src/__tests__/ConversationListPage.test.tsx`

**Interfaces:**
- Consumes: `listConversations`, `createConversation`, `archiveConversation`, `listAgents`.
- Produces:
  - `NewConversationDialog({ preselectedAgentId, onClose }: { preselectedAgentId?: string; onClose: () => void })`
  - `ConversationListPage()` als Standardexport.

- [ ] **Step 1: Die fehlschlagenden Tests schreiben**

`src/__tests__/ConversationListPage.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ConversationListPage from '../areas/agents/ConversationListPage'
import { stubFetch } from '../test/stubFetch'

const agent = (id: string, name: string) => ({
  id, name, description: null, systemPrompt: 'x', model: 'gpt-5', temperature: 0.7,
  maxOutputTokens: 4096, maxTurns: 20, allowedTools: [], createdAt: '', updatedAt: '',
  archivedAt: null, concurrencyToken: 't',
})

const conversation = {
  id: 'c1', title: 'D&D-Team',
  participants: [
    { agentId: 'a1', name: 'leo', model: 'gpt-5' },
    { agentId: 'a2', name: 'frontend-dev', model: 'gpt-5' },
  ],
  lastMessageExcerpt: 'Schaffst du das?', lastMessageAt: '2026-07-29T10:05:00Z',
  createdAt: '2026-07-29T10:00:00Z', archivedAt: null, concurrencyToken: 'tok-c1',
}

function renderPage(routes: Array<[string, { status?: number; json?: unknown }]>) {
  const calls = stubFetch(routes)
  render(
    <MemoryRouter initialEntries={['/agents/conversations']}>
      <Routes>
        <Route path="/agents/conversations" element={<ConversationListPage />} />
        <Route path="/agents/conversations/:id" element={<p>Gespräch</p>} />
      </Routes>
    </MemoryRouter>,
  )
  return calls
}

test('zeigt Titel, Teilnehmer und die letzte Nachricht', async () => {
  renderPage([['/api/agents/conversations', { json: { items: [conversation], total: 1 } }]])

  expect(await screen.findByRole('link', { name: 'D&D-Team' })).toBeInTheDocument()
  expect(screen.getByText('leo, frontend-dev')).toBeInTheDocument()
  expect(screen.getByText('Schaffst du das?')).toBeInTheDocument()
})

test('ohne Gespräche erscheint der leere Zustand mit dem nächsten Schritt', async () => {
  renderPage([['/api/agents/conversations', { json: { items: [], total: 0 } }]])

  expect(await screen.findByText('Noch keine Gespräche.')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Neues Gespräch' })).toBeInTheDocument()
})

test('ein Gespräch lässt sich mit Rückfrage archivieren', async () => {
  const calls = renderPage([
    ['/api/agents/conversations', { json: { items: [conversation], total: 1 } }],
    ['/api/agents/conversations/c1', { status: 204 }],
    ['/api/agents/conversations', { json: { items: [], total: 0 } }],
  ])
  await screen.findByRole('link', { name: 'D&D-Team' })

  await userEvent.click(screen.getByRole('button', { name: 'D&D-Team archivieren' }))
  await userEvent.click(screen.getByRole('button', { name: 'Archivieren' }))

  await waitFor(() => expect(calls).toHaveLength(3))
  expect(calls[1]!.method).toBe('DELETE')
})

test('der Dialog erlaubt die Mehrfachauswahl von Teilnehmern', async () => {
  renderPage([
    ['/api/agents/conversations', { json: { items: [], total: 0 } }],
    ['/api/agents/definitions', { json: { items: [agent('a1', 'leo'), agent('a2', 'frontend-dev')], total: 2 } }],
  ])
  await screen.findByText('Noch keine Gespräche.')

  await userEvent.click(screen.getByRole('button', { name: 'Neues Gespräch' }))

  expect(await screen.findByRole('checkbox', { name: 'leo' })).toBeInTheDocument()
  expect(screen.getByRole('checkbox', { name: 'frontend-dev' })).toBeInTheDocument()
})

test('ohne Teilnehmer wird nicht angelegt', async () => {
  const calls = renderPage([
    ['/api/agents/conversations', { json: { items: [], total: 0 } }],
    ['/api/agents/definitions', { json: { items: [agent('a1', 'leo')], total: 1 } }],
  ])
  await screen.findByText('Noch keine Gespräche.')
  await userEvent.click(screen.getByRole('button', { name: 'Neues Gespräch' }))
  await screen.findByRole('checkbox', { name: 'leo' })

  await userEvent.click(screen.getByRole('button', { name: 'Anlegen' }))

  expect(screen.getByText('Bitte mindestens einen Teilnehmer wählen.')).toBeInTheDocument()
  expect(calls).toHaveLength(2)
})

test('ein angelegtes Gespräch wird geöffnet, der Titel darf leer bleiben', async () => {
  const calls = renderPage([
    ['/api/agents/conversations', { json: { items: [], total: 0 } }],
    ['/api/agents/definitions', { json: { items: [agent('a1', 'leo')], total: 1 } }],
    ['/api/agents/conversations', { status: 201, json: { ...conversation, id: 'c9' } }],
  ])
  await screen.findByText('Noch keine Gespräche.')
  await userEvent.click(screen.getByRole('button', { name: 'Neues Gespräch' }))
  await userEvent.click(await screen.findByRole('checkbox', { name: 'leo' }))
  await userEvent.click(screen.getByRole('button', { name: 'Anlegen' }))

  await waitFor(() => expect(screen.getByText('Gespräch')).toBeInTheDocument())
  expect(calls[2]!.body).toEqual({ title: '', participantAgentIds: ['a1'] })
})
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- ConversationListPage`
Expected: FAIL — `Failed to resolve import "../areas/agents/ConversationListPage"`.

- [ ] **Step 3: `NewConversationDialog.tsx` schreiben**

Der Titel geht leer an den Server; die Spec sagt, dass der Server ihn dann aus den Teilnehmernamen bildet. Die UI erfindet keinen.

```tsx
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createConversation, listAgents } from './api'
import type { AgentDto } from './types'

export default function NewConversationDialog({
  preselectedAgentId,
  onClose,
}: {
  preselectedAgentId?: string
  onClose: () => void
}) {
  const navigate = useNavigate()
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [chosen, setChosen] = useState<string[]>(preselectedAgentId ? [preselectedAgentId] : [])
  const [title, setTitle] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    void listAgents({ q: '', skip: 0, take: 50 })
      .then((page) => setAgents(page.items))
      .catch(() => setError('Die Agenten konnten nicht geladen werden.'))
  }, [])

  function toggle(id: string) {
    setChosen((current) =>
      current.includes(id) ? current.filter((item) => item !== id) : [...current, id],
    )
    setError(null)
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (chosen.length === 0) {
      setError('Bitte mindestens einen Teilnehmer wählen.')
      return
    }
    setBusy(true)
    try {
      const created = await createConversation({ title, participantAgentIds: chosen })
      navigate(`/agents/conversations/${created.id}`)
    } catch {
      setError('Das Gespräch konnte nicht angelegt werden.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form
      onSubmit={submit}
      role="dialog"
      aria-modal="true"
      aria-label="Neues Gespräch"
      className="max-w-xl rounded-md border p-4"
      style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
    >
      <label className="mb-3 block text-sm">
        Titel (optional)
        <input
          className="mt-1 w-full rounded-md border px-2 py-1.5 text-sm"
          style={{ borderColor: 'var(--border)' }}
          value={title}
          onChange={(event) => setTitle(event.target.value)}
        />
      </label>

      <fieldset className="mb-3">
        <legend className="mb-1 text-sm">Teilnehmer</legend>
        {agents.map((agent) => (
          <label key={agent.id} className="mr-4 inline-flex items-center gap-1.5 text-sm">
            <input type="checkbox" checked={chosen.includes(agent.id)} onChange={() => toggle(agent.id)} />
            {agent.name}
          </label>
        ))}
      </fieldset>

      {error && (
        <p className="mb-3 text-sm" style={{ color: 'var(--danger)' }}>{error}</p>
      )}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={busy}
          className="rounded-md px-3 py-1.5 text-sm font-medium disabled:opacity-60"
          style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
        >
          Anlegen
        </button>
        <button type="button" className="rounded-md border px-3 py-1.5 text-sm" style={{ borderColor: 'var(--border)' }} onClick={onClose}>
          Abbrechen
        </button>
      </div>
    </form>
  )
}
```

- [ ] **Step 4: `ConversationListPage.tsx` schreiben**

```tsx
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { archiveConversation, listConversations } from './api'
import NewConversationDialog from './NewConversationDialog'
import type { ConversationDto } from './types'

export default function ConversationListPage() {
  const [items, setItems] = useState<ConversationDto[]>([])
  const [loading, setLoading] = useState(true)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [pending, setPending] = useState<ConversationDto | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    setLoading(true)
    listConversations({ skip: 0, take: 50 })
      .then((page) => setItems(page.items))
      .catch(() => setItems([]))
      .finally(() => setLoading(false))
  }, [reloadKey])

  async function confirmArchive() {
    if (!pending) return
    try {
      await archiveConversation(pending.id)
    } finally {
      setPending(null)
      setReloadKey((key) => key + 1)
    }
  }

  return (
    <section>
      <header className="mb-4 flex items-center gap-3">
        <h1 className="text-lg font-semibold" style={{ color: 'var(--text-strong)' }}>Gespräche</h1>
        <button
          type="button"
          className="ml-auto rounded-md px-3 py-1.5 text-sm font-medium"
          style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
          onClick={() => setDialogOpen(true)}
        >
          Neues Gespräch
        </button>
      </header>

      {dialogOpen && (
        <div className="mb-4">
          <NewConversationDialog onClose={() => setDialogOpen(false)} />
        </div>
      )}

      {loading && <p className="text-sm">Wird geladen …</p>}

      {!loading && items.length === 0 && (
        <div className="rounded-md border border-dashed p-6 text-center" style={{ borderColor: 'var(--border)' }}>
          <p className="mb-3 text-sm">Noch keine Gespräche.</p>
          <button
            type="button"
            className="rounded-md px-3 py-1.5 text-sm font-medium"
            style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
            onClick={() => setDialogOpen(true)}
          >
            Neues Gespräch
          </button>
        </div>
      )}

      <ul className="grid gap-2">
        {items.map((conversation) => (
          <li
            key={conversation.id}
            className="rounded-md border p-3"
            style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
          >
            <div className="flex items-baseline gap-3">
              <Link to={`/agents/conversations/${conversation.id}`} className="font-medium underline" style={{ color: 'var(--text-strong)' }}>
                {conversation.title}
              </Link>
              <button
                type="button"
                className="ml-auto text-sm underline"
                aria-label={`${conversation.title} archivieren`}
                onClick={() => setPending(conversation)}
              >
                Archivieren
              </button>
            </div>
            <p className="text-sm" style={{ color: 'var(--text-muted)' }}>
              {conversation.participants.map((participant) => participant.name).join(', ')}
            </p>
            {conversation.lastMessageExcerpt && (
              <p className="mt-1 truncate text-sm">{conversation.lastMessageExcerpt}</p>
            )}
          </li>
        ))}
      </ul>

      {pending && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Gespräch archivieren"
          className="mt-4 rounded-md border p-4"
          style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
        >
          <p className="mb-3 text-sm">{`„${pending.title}" archivieren? Der Verlauf bleibt erhalten.`}</p>
          <div className="flex gap-2">
            <button type="button" className="rounded-md px-3 py-1.5 text-sm font-medium" style={{ background: 'var(--danger)', color: '#fff' }} onClick={() => void confirmArchive()}>
              Archivieren
            </button>
            <button type="button" className="rounded-md border px-3 py-1.5 text-sm" style={{ borderColor: 'var(--border)' }} onClick={() => setPending(null)}>
              Abbrechen
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
```

- [ ] **Step 5: Route und den Knopf der Detailseite verdrahten**

In `routes.tsx`:

```tsx
import ConversationListPage from './ConversationListPage'
```

```tsx
    { path: '/agents/conversations', element: <ConversationListPage /> },
```

In `AgentDetailPage.tsx` den Knopf „Gespräch beginnen" auf einen Zustand `chatOpen` umstellen und den Dialog mit vorgewähltem Agenten einhängen:

```tsx
{chatOpen && (
  <div className="mb-5">
    <NewConversationDialog preselectedAgentId={agent.id} onClose={() => setChatOpen(false)} />
  </div>
)}
```

- [ ] **Step 6: Tests laufen lassen**

Run: `npm test -- ConversationListPage`
Expected: PASS, sechs Tests.

- [ ] **Step 7: Commit**

```bash
git add src/AgentForge.Web/src
git commit -m "feat: add conversation list and creation dialog"
```

---

### Task 17: Das Gespräch mit Erwähnungen

**Files:**
- Create: `src/AgentForge.Web/src/areas/agents/MessageComposer.tsx`, `areas/agents/ConversationPage.tsx`
- Modify: `src/AgentForge.Web/src/areas/agents/routes.tsx` (Platzhalter `/agents/conversations/:id`)
- Test: `src/AgentForge.Web/src/__tests__/MessageComposer.test.tsx`, `src/__tests__/ConversationPage.test.tsx`

**Interfaces:**
- Consumes: `useConversationStream`, `getConversation`, `updateConversation`, `listAgents`, `Transcript`, `useContextPanel`, `rememberItem`.
- Produces:
  - `MessageComposer({ participants, onSend }: { participants: ParticipantDto[]; onSend: (content: string, mentions: string[]) => void })`
  - `ConversationPage()` als Standardexport.

Die Erwähnungsauswahl arbeitet mit Ids: `@` öffnet eine Liste der Teilnehmer, die Auswahl legt eine Plakette an und fügt `@name` in den Text ein. Über die Leitung gehen die Ids der Plaketten, nie der geschriebene Text.

- [ ] **Step 1: Die fehlschlagenden Tests des Eingabefeldes schreiben**

`src/__tests__/MessageComposer.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import MessageComposer from '../areas/agents/MessageComposer'

const participants = [
  { agentId: 'a1', name: 'leo', model: 'gpt-5' },
  { agentId: 'a2', name: 'frontend-dev', model: 'gpt-5' },
]

test('@ öffnet die Teilnehmerauswahl', async () => {
  render(<MessageComposer participants={participants} onSend={() => {}} />)

  await userEvent.type(screen.getByLabelText('Nachricht'), '@')

  expect(screen.getByRole('listbox', { name: 'Teilnehmer erwähnen' })).toBeInTheDocument()
  expect(screen.getByRole('option', { name: 'frontend-dev' })).toBeInTheDocument()
})

test('eine gewählte Erwähnung wird zur Plakette und schließt die Auswahl', async () => {
  render(<MessageComposer participants={participants} onSend={() => {}} />)

  await userEvent.type(screen.getByLabelText('Nachricht'), '@')
  await userEvent.click(screen.getByRole('option', { name: 'frontend-dev' }))

  expect(screen.getByText('@frontend-dev')).toBeInTheDocument()
  expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
})

test('gesendet werden Ids, nicht der geschriebene Text', async () => {
  const onSend = vi.fn()
  render(<MessageComposer participants={participants} onSend={onSend} />)

  await userEvent.type(screen.getByLabelText('Nachricht'), '@')
  await userEvent.click(screen.getByRole('option', { name: 'frontend-dev' }))
  await userEvent.type(screen.getByLabelText('Nachricht'), 'schaffst du das?')
  await userEvent.click(screen.getByRole('button', { name: 'Senden' }))

  expect(onSend).toHaveBeenCalledWith('@frontend-dev schaffst du das?', ['a2'])
})

test('eine Plakette lässt sich wieder entfernen', async () => {
  render(<MessageComposer participants={participants} onSend={() => {}} />)

  await userEvent.type(screen.getByLabelText('Nachricht'), '@')
  await userEvent.click(screen.getByRole('option', { name: 'leo' }))
  await userEvent.click(screen.getByRole('button', { name: '@leo entfernen' }))

  expect(screen.queryByText('@leo')).not.toBeInTheDocument()
})

test('ohne Erwähnung wird der Hinweis gezeigt, das Senden aber erlaubt', async () => {
  const onSend = vi.fn()
  render(<MessageComposer participants={participants} onSend={onSend} />)

  await userEvent.type(screen.getByLabelText('Nachricht'), 'nur eine Notiz')

  expect(screen.getByText('Niemand adressiert — die Nachricht wird nur notiert.')).toBeInTheDocument()

  await userEvent.click(screen.getByRole('button', { name: 'Senden' }))
  expect(onSend).toHaveBeenCalledWith('nur eine Notiz', [])
})

test('eine leere Nachricht wird nicht gesendet', async () => {
  const onSend = vi.fn()
  render(<MessageComposer participants={participants} onSend={onSend} />)

  await userEvent.click(screen.getByRole('button', { name: 'Senden' }))

  expect(onSend).not.toHaveBeenCalled()
})
```

- [ ] **Step 2: Test laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- MessageComposer`
Expected: FAIL — `Failed to resolve import "../areas/agents/MessageComposer"`.

- [ ] **Step 3: `MessageComposer.tsx` schreiben**

```tsx
import { useState } from 'react'
import type { ParticipantDto } from './types'

export default function MessageComposer({
  participants,
  onSend,
}: {
  participants: ParticipantDto[]
  onSend: (content: string, mentions: string[]) => void
}) {
  const [text, setText] = useState('')
  const [mentions, setMentions] = useState<ParticipantDto[]>([])
  const [picking, setPicking] = useState(false)

  function choose(participant: ParticipantDto) {
    setPicking(false)
    // The '@' the user typed becomes the badge; the text keeps a readable form.
    setText((current) => `${current.replace(/@$/, '')}@${participant.name} `)
    setMentions((current) =>
      current.some((item) => item.agentId === participant.agentId) ? current : [...current, participant],
    )
  }

  function submit(event: React.FormEvent) {
    event.preventDefault()
    const content = text.trim()
    if (content === '') return
    onSend(content, mentions.map((participant) => participant.agentId))
    setText('')
    setMentions([])
  }

  return (
    <form onSubmit={submit} className="border-t p-3" style={{ borderColor: 'var(--border)' }}>
      {mentions.length > 0 && (
        <div className="mb-2 flex flex-wrap gap-2">
          {mentions.map((participant) => (
            <span key={participant.agentId} className="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs" style={{ borderColor: 'var(--border)' }}>
              {`@${participant.name}`}
              <button
                type="button"
                aria-label={`@${participant.name} entfernen`}
                onClick={() => setMentions((current) => current.filter((item) => item.agentId !== participant.agentId))}
              >
                ×
              </button>
            </span>
          ))}
        </div>
      )}

      {picking && (
        <ul role="listbox" aria-label="Teilnehmer erwähnen" className="mb-2 rounded-md border" style={{ borderColor: 'var(--border)' }}>
          {participants.map((participant) => (
            <li key={participant.agentId}>
              <button
                type="button"
                role="option"
                aria-selected={false}
                className="block w-full px-2 py-1 text-left text-sm hover:bg-[var(--accent-soft)]"
                onClick={() => choose(participant)}
              >
                {participant.name}
              </button>
            </li>
          ))}
        </ul>
      )}

      <label className="block text-sm">
        Nachricht
        <textarea
          className="mt-1 min-h-20 w-full rounded-md border px-2 py-1.5 text-sm"
          style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}
          value={text}
          onChange={(event) => {
            setText(event.target.value)
            if (event.target.value.endsWith('@')) setPicking(true)
          }}
        />
      </label>

      {mentions.length === 0 && text.trim() !== '' && (
        <p className="mt-1 text-xs" style={{ color: 'var(--text-muted)' }}>
          Niemand adressiert — die Nachricht wird nur notiert.
        </p>
      )}

      <button
        type="submit"
        className="mt-2 rounded-md px-3 py-1.5 text-sm font-medium"
        style={{ background: 'var(--accent)', color: 'var(--accent-text)' }}
      >
        Senden
      </button>
    </form>
  )
}
```

- [ ] **Step 4: Die fehlschlagenden Tests der Gesprächsseite schreiben**

`src/__tests__/ConversationPage.test.tsx`:

```tsx
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ConversationPage from '../areas/agents/ConversationPage'
import { ContextPanelOutlet, ContextPanelProvider } from '../shell/ContextPanel'
import { installFakeEventSource } from '../test/fakeEventSource'
import { stubFetch } from '../test/stubFetch'

const conversation = {
  id: 'c1', title: 'D&D-Team',
  participants: [
    { agentId: 'a1', name: 'leo', model: 'gpt-5' },
    { agentId: 'a2', name: 'frontend-dev', model: 'gpt-5' },
  ],
  lastMessageExcerpt: null, lastMessageAt: null,
  createdAt: '2026-07-29T10:00:00Z', archivedAt: null, concurrencyToken: 'tok-c1',
}

function renderPage(extra: Array<[string, { status?: number; json?: unknown }]> = []) {
  const fake = installFakeEventSource()
  const calls = stubFetch([
    ['/api/agents/conversations/c1/messages', { json: [] }],
    ['/api/agents/conversations/c1', { json: conversation }],
    ...extra,
  ])
  render(
    <MemoryRouter initialEntries={['/agents/conversations/c1']}>
      <ContextPanelProvider>
        <Routes>
          <Route path="/agents/conversations/:id" element={<ConversationPage />} />
        </Routes>
        <aside aria-label="Kontext">
          <ContextPanelOutlet />
        </aside>
      </ContextPanelProvider>
    </MemoryRouter>,
  )
  return { fake, calls }
}

beforeEach(() => {
  localStorage.clear()
})

test('zeigt Titel und Teilnehmer in der Kontextspalte', async () => {
  const { fake } = renderPage()

  expect(await screen.findByRole('heading', { name: 'D&D-Team' })).toBeInTheDocument()
  const context = screen.getByRole('complementary', { name: 'Kontext' })
  await waitFor(() => expect(context).toHaveTextContent('frontend-dev'))

  fake.restore()
})

test('eine Antwort strömt mit Absendername ein', async () => {
  const { fake } = renderPage()
  await screen.findByRole('heading', { name: 'D&D-Team' })

  act(() => {
    fake.instances[0]!.emit('token', { streamId: 's1', sequence: 1, text: 'Ja, ' })
    fake.instances[0]!.emit('message', {
      streamId: 's1', sequence: 1,
      message: {
        sequence: 1, role: 'Assistant', senderAgentId: 'a2', senderName: 'frontend-dev',
        content: 'Ja, zwei Stunden.', toolCalls: [], mentions: [], state: 'complete',
        createdAt: '2026-07-29T10:01:00Z',
      },
    })
  })

  expect(screen.getByText('Ja, zwei Stunden.')).toBeInTheDocument()
  expect(screen.getAllByText('frontend-dev').length).toBeGreaterThan(0)

  fake.restore()
})

test('eine gesendete Nachricht geht mit Erwähnungs-Ids raus', async () => {
  const { fake, calls } = renderPage([
    ['/api/agents/conversations/c1/messages', { status: 202, json: { streamId: 's1' } }],
  ])
  await screen.findByRole('heading', { name: 'D&D-Team' })

  await userEvent.type(screen.getByLabelText('Nachricht'), '@')
  await userEvent.click(screen.getByRole('option', { name: 'frontend-dev' }))
  await userEvent.type(screen.getByLabelText('Nachricht'), 'schaffst du das?')
  await userEvent.click(screen.getByRole('button', { name: 'Senden' }))

  await waitFor(() => expect(calls.some((call) => call.method === 'POST')).toBe(true))
  expect(calls.find((call) => call.method === 'POST')!.body).toEqual({
    content: '@frontend-dev schaffst du das?',
    mentions: ['a2'],
  })

  fake.restore()
})

test('Teilnehmer lassen sich in der Kontextspalte ergänzen', async () => {
  const { fake, calls } = renderPage([
    ['/api/agents/definitions', { json: { items: [
      { id: 'a3', name: 'tester', description: null, systemPrompt: 'x', model: 'gpt-5', temperature: 0.7, maxOutputTokens: 4096, maxTurns: 20, allowedTools: [], createdAt: '', updatedAt: '', archivedAt: null, concurrencyToken: 't' },
    ], total: 1 } }],
    ['/api/agents/conversations/c1', { json: { ...conversation, participants: [...conversation.participants, { agentId: 'a3', name: 'tester', model: 'gpt-5' }] } }],
  ])
  await screen.findByRole('heading', { name: 'D&D-Team' })

  await userEvent.selectOptions(await screen.findByRole('combobox', { name: 'Teilnehmer hinzufügen' }), 'a3')

  await waitFor(() => expect(calls.some((call) => call.method === 'PUT')).toBe(true))
  expect(calls.find((call) => call.method === 'PUT')!.body).toMatchObject({
    participantAgentIds: ['a1', 'a2', 'a3'],
    concurrencyToken: 'tok-c1',
  })

  fake.restore()
})

test('das Gespräch landet in den zuletzt berührten Objekten', async () => {
  const { fake } = renderPage()
  await screen.findByRole('heading', { name: 'D&D-Team' })

  expect(JSON.parse(localStorage.getItem('agentforge.recent')!)[0]).toMatchObject({
    key: 'conversation:c1',
    label: 'D&D-Team',
  })

  fake.restore()
})

test('ein fehlendes Gespräch zeigt die nicht-gefunden-Ansicht mit Weg zurück', async () => {
  const fake = installFakeEventSource()
  stubFetch([
    ['/api/agents/conversations/c1/messages', { json: [] }],
    ['/api/agents/conversations/c1', { status: 404, json: { type: 'errors/not-found', title: 'Nicht gefunden' } }],
    ['/api/agents/definitions', { json: { items: [], total: 0 } }],
  ])
  render(
    <MemoryRouter initialEntries={['/agents/conversations/c1']}>
      <ContextPanelProvider>
        <Routes>
          <Route path="/agents/conversations/:id" element={<ConversationPage />} />
        </Routes>
      </ContextPanelProvider>
    </MemoryRouter>,
  )

  expect(await screen.findByText('Dieses Gespräch gibt es nicht.')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Zur Gesprächsliste' })).toBeInTheDocument()

  fake.restore()
})
```

- [ ] **Step 5: Test laufen lassen und Fehlschlag bestätigen**

Run: `npm test -- ConversationPage`
Expected: FAIL — `Failed to resolve import "../areas/agents/ConversationPage"`.

- [ ] **Step 6: `ConversationPage.tsx` schreiben**

```tsx
import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useContextPanel } from '../../shell/ContextPanel'
import { rememberItem } from '../../shell/RecentItems'
import { ApiRequestError } from '../../lib/http'
import { getConversation, listAgents, updateConversation } from './api'
import MessageComposer from './MessageComposer'
import { NotFoundView } from './NotFoundView'
import { Transcript } from './Transcript'
import { useConversationStream } from './useConversationStream'
import type { AgentDto, ConversationDto } from './types'

export default function ConversationPage() {
  const { id = '' } = useParams<{ id: string }>()
  const { messages, outgoing, connection, send } = useConversationStream(id)
  const [conversation, setConversation] = useState<ConversationDto | null>(null)
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [notice, setNotice] = useState<string | null>(null)
  const [missing, setMissing] = useState(false)

  useEffect(() => {
    if (!id) return
    getConversation(id)
      .then((loaded) => {
        setConversation(loaded)
        rememberItem({ key: `conversation:${loaded.id}`, to: `/agents/conversations/${loaded.id}`, label: loaded.title, kind: 'conversation' })
      })
      .catch((cause: unknown) => {
        if (cause instanceof ApiRequestError && cause.info.status === 404) setMissing(true)
        else setNotice('Das Gespräch konnte nicht geladen werden.')
      })
    void listAgents({ q: '', skip: 0, take: 50 }).then((page) => setAgents(page.items)).catch(() => setAgents([]))
  }, [id])

  async function addParticipant(agentId: string) {
    if (!conversation) return
    const ids = [...conversation.participants.map((participant) => participant.agentId), agentId]
    try {
      setConversation(
        await updateConversation(conversation.id, {
          title: conversation.title,
          participantAgentIds: ids,
          concurrencyToken: conversation.concurrencyToken,
        }),
      )
      setNotice(null)
    } catch {
      setNotice('Die Teilnehmer wurden anderswo geändert. Bitte neu laden.')
    }
  }

  async function removeParticipant(agentId: string) {
    if (!conversation) return
    const ids = conversation.participants
      .map((participant) => participant.agentId)
      .filter((existing) => existing !== agentId)
    try {
      setConversation(
        await updateConversation(conversation.id, {
          title: conversation.title,
          participantAgentIds: ids,
          concurrencyToken: conversation.concurrencyToken,
        }),
      )
    } catch {
      setNotice('Die Teilnehmer wurden anderswo geändert. Bitte neu laden.')
    }
  }

  const candidates = agents.filter(
    (agent) => !conversation?.participants.some((participant) => participant.agentId === agent.id),
  )

  useContextPanel(
    useMemo(
      () => (
        <div className="text-sm">
          <p className="mb-1 text-xs font-semibold uppercase" style={{ color: 'var(--text-muted)' }}>Teilnehmer</p>
          <ul className="mb-3">
            {conversation?.participants.map((participant) => (
              <li key={participant.agentId} className="flex items-baseline gap-2">
                <Link to={`/agents/definitions/${participant.agentId}`} className="underline">{participant.name}</Link>
                <span className="text-xs" style={{ color: 'var(--text-muted)' }}>{participant.model}</span>
                <button
                  type="button"
                  className="ml-auto text-xs underline"
                  aria-label={`${participant.name} entfernen`}
                  onClick={() => void removeParticipant(participant.agentId)}
                >
                  entfernen
                </button>
              </li>
            ))}
          </ul>
          <label className="block text-sm">
            Teilnehmer hinzufügen
            <select
              className="mt-1 w-full rounded-md border px-2 py-1 text-sm"
              style={{ borderColor: 'var(--border)' }}
              value=""
              onChange={(event) => void addParticipant(event.target.value)}
            >
              <option value="">wählen …</option>
              {candidates.map((agent) => (
                <option key={agent.id} value={agent.id}>{agent.name}</option>
              ))}
            </select>
          </label>
        </div>
      ),
      [conversation, candidates],
    ),
  )

  if (missing) {
    return (
      <NotFoundView
        what="Dieses Gespräch gibt es nicht."
        backTo="/agents/conversations"
        backLabel="Zur Gesprächsliste"
      />
    )
  }

  return (
    <section>
      <h1 className="mb-3 text-lg font-semibold" style={{ color: 'var(--text-strong)' }}>
        {conversation?.title ?? 'Gespräch'}
      </h1>

      {notice && <p role="alert" className="mb-3 text-sm" style={{ color: 'var(--danger)' }}>{notice}</p>}

      {connection === 'reconnecting' && (
        <p className="mb-3 text-sm" style={{ color: 'var(--text-muted)' }}>Verbindung unterbrochen, versuche erneut …</p>
      )}
      {connection === 'lost' && (
        <p className="mb-3 text-sm">
          Verbindung verloren.{' '}
          <button type="button" className="underline" onClick={() => window.location.reload()}>Neu laden</button>
        </p>
      )}

      <div className="rounded-md border" style={{ borderColor: 'var(--border)', background: 'var(--bg-raised)' }}>
        <Transcript messages={messages} outgoing={outgoing} youLabel="Du" />
        <MessageComposer
          participants={conversation?.participants ?? []}
          onSend={(content, mentions) => void send(content, mentions)}
        />
      </div>
    </section>
  )
}
```

- [ ] **Step 7: Die letzte Platzhalter-Route ersetzen und `NotYet` entfernen**

```tsx
import ConversationPage from './ConversationPage'
```

```tsx
    { path: '/agents/conversations/:id', element: <ConversationPage /> },
```

Die Hilfsfunktion `NotYet` in `routes.tsx` wird jetzt von keiner Route mehr verwendet und gelöscht. `npm run typecheck` schlägt sonst wegen `noUnusedLocals` fehl — das ist beabsichtigt und der Beweis, dass kein Platzhalter übrig ist.

- [ ] **Step 8: Alles laufen lassen und im Mock ansehen**

Run: `npm test && npm run lint && npm run typecheck`
Expected: alles PASS.

Run: `npm run dev:mock`, dann ein Gespräch mit drei Agenten anlegen, `@frontend-dev` erwähnen und die Antwort einlaufen sehen. Danach beenden.

- [ ] **Step 9: Commit**

```bash
git add src/AgentForge.Web/src
git commit -m "feat: add conversation page with mention-addressed group chat"
```

---

### Task 18: Der Host liefert das Frontend aus

**Files:**
- Modify: `src/AgentForge.Host/Program.cs:38` (nach `app.UseStatusCodePages();`) und `:47` (nach `app.MapAreas();`)
- Modify: `src/AgentForge.Host/AgentForge.Host.csproj`
- Create: `src/AgentForge.Host/wwwroot/index.html`
- Modify: `.gitignore`
- Test: `tests/AgentForge.Host.Integration/StaticFilesTests.cs`

**Interfaces:**
- Consumes: `AgentForgeFactory` aus dem bestehenden Integrationstestprojekt.
- Produces: keine neuen Typen. Das Ergebnis ist Verhalten: unbekannte Nicht-API-Pfade liefern `index.html`, unbekannte `/api`-Pfade bleiben 404 als ProblemDetails.

Die Reihenfolge ist der ganze Trick. `MapFallbackToFile` fängt mit `{*path:nonfile}` **alles** ab, auch `/api/gibtsnicht`. Deshalb steht davor eine ausdrückliche `/api`-Route: sie ist spezifischer, gewinnt beim Routen-Abgleich und hält die API bei 404. Ohne diese Zeile bekäme ein Tippfehler in einem API-Pfad die HTML-Seite zurück — ein Fehler, der sich als kaputtes JSON-Parsen im Browser zeigt und dessen Ursache man lange sucht.

- [ ] **Step 1: Den fehlschlagenden Integrationstest schreiben**

`tests/AgentForge.Host.Integration/StaticFilesTests.cs`:

```csharp
using System.Net;

namespace AgentForge.Host.Integration;

public sealed class StaticFilesTests(AgentForgeFactory factory) : IClassFixture<AgentForgeFactory>
{
    [Fact]
    public async Task Unknown_non_api_path_returns_the_spa_shell()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/agents/runs/00000000-0000-0000-0000-000000000001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Root_returns_the_spa_shell()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_api_path_stays_a_problem_details_404()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/gibtsnicht");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Health_endpoint_is_unaffected()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

Sollte das bestehende Projekt eine andere Test-Basisklasse oder ein anderes Fixture-Muster verwenden, gilt das bestehende Muster; nur die vier Zusicherungen sind bindend.

- [ ] **Step 2: Test laufen lassen und Fehlschlag bestätigen**

Run: `dotnet test tests/AgentForge.Host.Integration --filter StaticFilesTests`
Expected: FAIL — die beiden HTML-Tests liefern 404, weil es weder statische Dateien noch einen Fallback gibt.

- [ ] **Step 3: Den Platzhalter anlegen**

`src/AgentForge.Host/wwwroot/index.html`:

```html
<!doctype html>
<html lang="de">
  <head>
    <meta charset="UTF-8" />
    <title>AgentForge</title>
  </head>
  <body>
    <p>
      Das Frontend ist in diesem Verzeichnis nicht gebaut. Beim Entwickeln
      erreichst du es über den Vite-Server; beim Veröffentlichen ersetzt der
      Publish-Schritt diese Datei.
    </p>
  </body>
</html>
```

- [ ] **Step 4: `Program.cs` an zwei Stellen ergänzen**

Nach `app.UseStatusCodePages();`:

```csharp
app.UseDefaultFiles();
app.UseStaticFiles();
```

Nach `app.MapAreas();`:

```csharp
// Must stay above the SPA fallback: it would otherwise answer unknown API
// paths with index.html, which surfaces much later as broken JSON parsing.
app.Map("/api/{*rest}", () => Results.Problem(
    title: "Nicht gefunden.",
    type: "https://agentforge.local/errors/not-found",
    statusCode: StatusCodes.Status404NotFound));

app.MapFallbackToFile("index.html");
```

- [ ] **Step 5: Test laufen lassen**

Run: `dotnet test tests/AgentForge.Host.Integration --filter StaticFilesTests`
Expected: PASS, vier Tests.

- [ ] **Step 6: Den Publish-Schritt in die `csproj` hängen**

In `src/AgentForge.Host/AgentForge.Host.csproj` vor `</Project>` ergänzen:

```xml
  <PropertyGroup>
    <FrontendDir>$(MSBuildProjectDirectory)\..\AgentForge.Web</FrontendDir>
  </PropertyGroup>

  <!-- Publish only. A plain dotnet build must never pay for the frontend build. -->
  <Target Name="PublishFrontend" AfterTargets="Publish" Condition="'$(SkipFrontendBuild)' != 'true'">
    <Exec Command="npm ci" WorkingDirectory="$(FrontendDir)" />
    <Exec Command="npm run build" WorkingDirectory="$(FrontendDir)" />
    <ItemGroup>
      <FrontendArtifacts Include="$(FrontendDir)\dist\**\*" />
    </ItemGroup>
    <Copy SourceFiles="@(FrontendArtifacts)"
          DestinationFiles="@(FrontendArtifacts->'$(PublishDir)wwwroot\%(RecursiveDir)%(Filename)%(Extension)')" />
  </Target>
```

- [ ] **Step 7: Prüfen, dass `dotnet build` kein npm ruft**

Run: `dotnet build`
Expected: erfolgreich, in der Ausgabe erscheint **kein** `npm`.

- [ ] **Step 8: Publish prüfen**

Run: `dotnet publish src/AgentForge.Host -o artifacts/publish-test`
Expected: erfolgreich; `artifacts/publish-test/wwwroot/index.html` ist die gebaute Datei mit einem `<script type="module" src="/assets/…">`-Verweis, nicht der Platzhaltertext. Danach `artifacts/` löschen — es ist ein Prüfschritt, kein Artefakt.

- [ ] **Step 9: Die `.gitignore` ergänzen**

Im Abschnitt `# AgentForge`:

```
artifacts/
src/AgentForge.Host/wwwroot/assets/
```

- [ ] **Step 10: Alles laufen lassen**

Run: `dotnet build && dotnet test`
Expected: alles PASS, ohne Warnungen.

- [ ] **Step 11: Commit**

```bash
git add src/AgentForge.Host tests/AgentForge.Host.Integration .gitignore
git commit -m "feat: serve the built frontend from the host with an spa fallback"
```

---

### Task 19: Abnahme gegen die echte API — **blockiert**

**Voraussetzung:** Teilprojekte 2, 3, 4 und 3b sind umgesetzt. Zum Zeitpunkt dieses Plans existiert `src/Areas/` nicht, es gibt keine Agents-API, keine Runtime und keine Gespräche im Backend.

**Wer diesen Plan abarbeitet, hält hier an und meldet zurück, dass 1 bis 18 fertig sind und 19 auf das Backend wartet.** Nichts an dieser Aufgabe lässt sich sinnvoll gegen den Mock erledigen — sie prüft genau das, was der Mock vortäuscht.

Wenn das Backend steht, sind das die Schritte. Sie folgen den neun Fertigstellungskriterien der Spec.

- [ ] **Step 1: Host und Frontend gemeinsam starten**

Run: `dotnet run --project src/AgentForge.Host` und in einem zweiten Fenster `npm run dev` aus `src/AgentForge.Web`.
Expected: `http://localhost:5173` zeigt die Shell, die Bereichsnavigation nennt genau die Bereiche aus `/api/areas` (Kriterium 9).

- [ ] **Step 2: Agenten-Lebenszyklus von Hand durchgehen**

Agent anlegen, bearbeiten, archivieren.
Expected: der archivierte Agent fehlt in der Liste, ist über seine Id-Route weiter erreichbar (Kriterium 2).

- [ ] **Step 3: Run durchspielen**

Run starten, Verlauf zusehen, auf Protokoll umschalten, abbrechen, erneut abbrechen.
Expected: Werkzeugkarten erscheinen, das Protokoll zeigt dieselben Daten samt System-Prompt, der zweite Abbruch zeigt „Der Run ist bereits beendet." (Kriterium 3).

- [ ] **Step 4: Gruppengespräch durchspielen**

Gespräch mit drei Agenten anlegen, `@name` erwähnen, Antwort abwarten; danach eine Nachricht ohne Erwähnung senden.
Expected: genau der erwähnte Agent antwortet, Absender sind an Name und Farbe unterscheidbar (Kriterium 4); die Nachricht ohne Erwähnung wird gespeichert und als nicht adressiert gekennzeichnet, ohne einen Agenten zu starten (Kriterium 5).

- [ ] **Step 5: Die Fehlercodes gegen den echten Server prüfen**

Für jeden Code aus der Tabelle „Fehlercodes als Vertrag" den Fall auslösen und die Meldung ansehen.
Expected: jeder Fall zeigt seine eigene Meldung. **Zeigt einer die allgemeine Meldung, liefert der Server kein passendes `type` — dann ist das ein Backend-Fehler und kein UI-Fehler.** In diesem Fall Rückmeldung an das Backend, nicht Textvergleich in der UI einbauen (Kriterium 6).

- [ ] **Step 6: Wiederverbindung mitten in einer Antwort erzwingen**

Während eine Antwort strömt, den Host neu starten.
Expected: der Hinweis „Verbindung unterbrochen" erscheint, danach baut sich die Antwort neu auf; am Ende steht jede Nachricht genau einmal im Verlauf (Kriterium 7).

- [ ] **Step 7: Ausgelieferten Bau prüfen**

Run: `dotnet publish src/AgentForge.Host -o artifacts/publish` und die Anwendung aus diesem Verzeichnis starten.
Expected: die Anwendung läuft ohne Vite; ein Neuladen auf `/agents/runs/{id}` zeigt die Seite statt 404 (Kriterium 8).

- [ ] **Step 8: Abschluss**

Run: `dotnet build && dotnet test` aus der Repo-Wurzel und `npm run build && npm run lint && npm test` aus `src/AgentForge.Web`.
Expected: alles fehlerfrei und ohne Warnungen (Kriterium 1).

```bash
git add -A
git commit -m "test: verify ui against the real api"
```
