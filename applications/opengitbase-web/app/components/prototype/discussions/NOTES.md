# Discussion UI prototype (grill v2)

**Verdict:** List **variant A** (cards) + shared detail hybrid layout.

| Area | Choice |
|------|--------|
| List | Cards with status bar, chip filters, fly-in create |
| Detail | Right meta sidebar + content-width bottom composer |

**Route:** `/{owner}/{repo}/discussions` and `/{owner}/{repo}/discussions/{number}` — dev only (`import.meta.dev`).

**Run locally:**

```bash
cd applications/opengitbase-web
NUXT_DEV_API_PROXY=http://localhost:8089 pnpm dev --port 3001
```

Delete `app/components/prototype/` and promote shared components to production when ready.
