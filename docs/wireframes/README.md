# OpenGitBase — Web App Wireframes

Mid-fidelity wireframes of every implemented screen in `applications/opengitbase-web`
(logged-out and logged-in), grouped into 7 pages on a single pan/zoom canvas.

**Live canvas (Claude Design):** https://claude.ai/code/artifact/aae68f66-028c-4cae-a2b1-7027b60ac43f

The canvas is editable in place (click-to-select, properties panel, inline text, Save).
These source files are what the canvas is generated from — edit them to drive the next
iteration, then re-seed and re-publish to the **same** URL.

## What's here

- `gen.mjs` — generator. Emits every `*.dc.html` artboard + `canvas.json`. Holds the
  shared chrome (header, sidebars, cards, badges, buttons, inline SVG icons) and the
  design tokens lifted from `applications/opengitbase-web/app/assets/main.css`
  (teal accent `#0d9488`, zinc neutrals, Inter / JetBrains Mono, 56px header, 256px sidebar).
- `measure.mjs` — measures each artboard's real rendered height (Playwright @1280px)
  and rewrites `canvas.json` frame heights + row layout so nothing clips.
- `*.dc.html` — the 34 artboards (one per screen; `Main.dc.html` is the entry/legend).
- `canvas.json` — layout manifest: artboard positions, the 7 pages, launch view.

## Coverage (34 artboards)

| Page | Screens |
|------|---------|
| Foundation | Index + design-system legend |
| Public & discovery | Home (guest), Dashboard, Explore, Owner profile, System status, Pitch |
| Auth | Auth sheet (sign-in/up, forgot/reset, verify, CLI), Invite accept |
| Repository | Overview, Files, Commit diff, Discussions (+detail), Merge requests (+new, +detail), Pipelines (+run), Members, Settings |
| Org & account | Org overview, Members, Storage/Compute, Account settings, Create forms |
| Admin console | Home, Storage fleet, Compute fleet, CI supply chain, Domain allowance, Status & incidents, Replication |
| Docs | Index, Article |

Screens whose data isn't in the MSW mock (file tree/blob, populated MR & discussion
lists, branch-protection rules, replication rows) show representative sample rows
reconstructed from the page source — illustrative of intended layout, not live data.

## Regenerate & re-publish (next iteration)

Requires Node and Playwright (available via `applications/opengitbase-web/node_modules`).

```bash
# 1. edit gen.mjs (or the .dc.html files directly), then:
cd docs/wireframes
node gen.mjs            # rewrite artboards + canvas.json
node measure.mjs        # resize frames to content (run from a dir where @playwright/test resolves)

# 2. seed a fresh canvas payload with the Claude Design helper (path from /design skill):
#    node <skill-base>/seed-canvas.mjs --template <skill-base>/payload.template.html \
#      --out opengitbase-wireframes.html --title "OpenGitBase Wireframes" \
#      --artboard Main.dc.html --artboard <...each other>.dc.html --canvas canvas.json

# 3. re-publish to the SAME artifact URL (keeps the link):
#    Artifact tool → url: https://claude.ai/code/artifact/aae68f66-028c-4cae-a2b1-7027b60ac43f
#                    contract: "0.1.31", capabilities omitted (keeps stored declaration)
```

In a fresh Claude Code session, run `/design` first to extract the current helper/payload
base directory, then follow the update flow above. The canvas can also be `--extract`ed
back from the live artifact if these files are ever lost.
