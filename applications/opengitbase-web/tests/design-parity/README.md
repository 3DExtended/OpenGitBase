# Design parity harness

A small website that puts the **Terminal / Ice** design mockups next to the **live app**,
screen by screen, so we can see how close the real UI is to the target design and where it
still drifts.

- **Mockups (target design):** `docs/wireframes/directions/terminal/ice/*.dc.html`
  (the artboards the design direction was signed off on).
- **Real (live app):** captured from the running app in its dark Terminal/Ice theme,
  via the same MSW mocks the visual suite uses.

The report offers four views per screen: **side by side**, an **overlay slider**
(drag to wipe between real and mockup), **mockup only**, and **real only**.

## Run it

```bash
# from applications/opengitbase-web
node tests/design-parity/run.mjs           # boots the app (MSW), captures, builds report/
open tests/design-parity/report/index.html

# self-contained single file (PNGs inlined as data URIs — shareable/publishable):
OGB_PARITY_INLINE=1 node tests/design-parity/run.mjs
```

`run.mjs` starts a dev server on port 3200 with mocks on and tears it down when done.
To reuse a server you already have running, set `OGB_BASE`:

```bash
OGB_BASE=http://localhost:3200 node tests/design-parity/capture-real.mjs
node tests/design-parity/render-mockups.mjs
node tests/design-parity/build-report.mjs
```

## Files

| File | Role |
|------|------|
| `screens.mjs` | The screen catalogue — each id maps a live route to its `<id>.dc.html` mockup, with a note and (for the merge-request detail) the extra `page.route` fixtures it needs. |
| `capture-real.mjs` | Screenshots each live route (full page, 1360px) with the right auth/mock setup. |
| `render-mockups.mjs` | Renders each mockup artboard to PNG at the same width. |
| `build-report.mjs` | Emits `report/index.html`. `OGB_PARITY_INLINE=1` embeds the PNGs. |
| `run.mjs` | Orchestrates all three around a throwaway dev server. |

## How the mocks are wired

The app only activates MSW when the URL has `?msw=1` **or** `NUXT_PUBLIC_MSW=true`
(dev only — see `app/utils/mswEnabled.ts`). The harness therefore runs the server with
`NUXT_PUBLIC_MSW=false` and opts each MSW-backed screen in per navigation with `?msw=1`.
The merge-request detail screen is instead mocked entirely with Playwright `page.route`
fixtures (mirroring `tests/visual/merge-requests.spec.ts`), so it is captured with the
worker kept out of the way.

## What "parity" means here

This compares **design language** (dark charcoal, dotted grid, Ice cyan accent,
Space Grotesk + JetBrains Mono, cards / badges / buttons), not feature completeness.
Where a mockup shows content the app hasn't built yet (extra repo-overview rails, a
populated storage-node table), the note on that screen says so — that gap is scope,
not a styling regression.

`report/` is generated output and is git-ignored; regenerate it with the commands above.
