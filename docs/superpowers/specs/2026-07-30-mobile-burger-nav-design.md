# AgentForge — Mobile burger nav (AppShell)

**Date:** 2026-07-30  
**Status:** Design approved for planning  
**Scope:** Frontend shell only — collapse the full left sidebar into a burger drawer on narrow viewports. No backend.

## Goal

On mobile / narrow screens, stop permanently consuming horizontal space with the left nav. Provide a burger that opens a collapsible drawer containing **everything** currently in the left sidebar (brand, area title, primary nav, Recent).

## Decisions (locked)

| Topic | Choice |
|---|---|
| Approach | Overlay drawer below `md`; permanent left aside at `md+` |
| Drawer contents | Full left sidebar: AgentForge brand, area title, nav links, Recent |
| Mobile chrome | Slim top bar with burger + brand only (no duplicate area title in the bar) |
| Close drawer | Backdrop click, Escape, or activating a nav / Recent link |
| Open state | Local React state in `AppShell`; closed by default |
| Breakpoint | Tailwind `md` (768px). Right context panel remains `lg+` (unchanged) |
| Desktop | Unchanged: always-visible `w-64` left aside; no burger |
| Focus / a11y | Burger is a `<button>` with accessible name (e.g. “Open menu” / “Close menu”); drawer uses appropriate landmark / `aria-modal` or dialog semantics for the overlay |

## Out of scope

- Redesigning nav labels, Recent behavior, or context panel content
- Changing the `lg` breakpoint for the right context rail
- Swipe gestures
- Persisting open/closed state across reloads
- Shared design-system `Drawer` package beyond what `AppShell` needs
- Bottom-sheet or push-layout alternatives

## Current behavior (baseline)

`AppShell` (`frontend/src/shell/AppShell.tsx`) is a three-column flex layout:

- Left: always `w-64` aside with brand, area nav, Recent
- Main: `<Outlet />`
- Right: context panel, `hidden` below `lg`

There is no header bar and no burger.

## Target behavior

### `≥ md`

Same as today: permanent left aside; no top bar; no drawer.

### `< md`

```
┌─────────────────────────────┐
│ ☰  AgentForge               │  ← top bar
├─────────────────────────────┤
│                             │
│  main (Outlet)              │  ← full width
│                             │
└─────────────────────────────┘

When open:
┌──────────┬──────────────────┐
│ drawer   │ dimmed backdrop  │
│ (full    │ (closes on click)│
│  sidebar │                  │
│  content)│                  │
└──────────┴──────────────────┘
```

- Left aside is **not** in the document flow on small screens (hidden / off-canvas).
- Drawer overlays content; does not permanently shrink the main column.
- Choosing any in-drawer `NavLink` closes the drawer after navigation starts (or on click).

## Implementation sketch (non-binding detail for plan)

- Refactor sidebar body into a shared fragment/component used by both the permanent aside and the drawer (avoid duplicated nav markup).
- Mobile top bar: `md:hidden`; permanent aside: `hidden md:flex` (or equivalent).
- Drawer + backdrop: fixed/absolute overlay, only mounted or visible when `open &&` narrow — or always in DOM with CSS, closed when `!open`.
- On resize to `md+`, force `open = false` so state does not linger.

## Testing

- Component / unit: burger toggles open; Escape and backdrop close; link click closes.
- Manual: phone-width viewport — main is full width; open drawer shows brand + nav + Recent; desktop unchanged.

## Success criteria

- Below `md`, no permanent left column eating width.
- Burger opens/closes the full menu (everything in today’s sidebar).
- At `md+`, layout matches current desktop shell.
- Right context panel behavior unchanged (`lg+`).
