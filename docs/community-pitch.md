# Community pitch deck

The OpenGitBase **community pitch** is a Reveal.js presentation embedded in the web UI at **`/pitch`**. It targets contributors and evaluators: source-open forge, privacy-first self-hosting, and transparent project direction.

## Viewing the deck

| Environment | URL |
|-------------|-----|
| Local dev | `http://localhost:3000/pitch` |
| Docker (HAProxy) | `http://localhost:8089/pitch` |
| Production | `https://www.opengitbase.com/pitch` |

Navigation:

- **Arrow keys** — previous / next slide
- **Esc** — slide overview
- **Exit** — toolbar button returns to home

Discovery links:

- Header nav **Community** (all users)
- Guest home hero **Community pitch**
- Guest mobile sidebar **Community**

## Editing content

Slide copy and structure live in the web app repo:

| File | Purpose |
|------|---------|
| [communityPitchSlides.ts](../applications/opengitbase-web/app/data/communityPitchSlides.ts) | Slide titles, bullets, layout |
| [communityPitchLinks.ts](../applications/opengitbase-web/app/utils/communityPitchLinks.ts) | External URLs (GitHub, README, license) |
| [pitch.vue](../applications/opengitbase-web/app/pages/pitch.vue) | Reveal.js shell and rendering |
| [pitch.css](../applications/opengitbase-web/app/assets/pitch.css) | Theme (uses `--ogb-*` design tokens) |
| [en.json](../applications/opengitbase-web/i18n/locales/en.json) | Page title, toolbar strings (`pitch.*`, `nav.pitch`) |

To add a slide, append an entry to `communityPitchSlides` with a unique `id` and one of the layouts: `title`, `default`, `columns`, or `cta`.

To change CTA targets, edit `communityPitchLinks` (primary: GitHub Issues + project state doc; secondary: hosted README).

## Messaging baseline

The deck reflects decisions from a structured planning session (July 2026):

- **Audience:** open-source / contributor community
- **Primary CTA:** contribute (Issues, PROJECT-STATE, CONTRIBUTING)
- **Secondary CTA:** run locally (hosted README on opengitbase.com)
- **Positioning:** *Git that's yours to design* — transparent, privacy-first, source-open
- **License framing:** contributing is open; production use has conditions (source-available, not OSI)
- **Honesty:** hobby project pace; transparency roadmap (public roadmap, finance page) without hard deadlines
- **Maintainer:** [@3dextended](https://github.com/3dextended)

Technical depth for architecture and encryption posture: [PROJECT-STATE.md](./PROJECT-STATE.md).

## Tests

**Unit (Vitest):**

```bash
cd applications/opengitbase-web
pnpm test
```

Covers slide ordering, link targets, and `pitch` slug reservation.

**Playwright (behavioral):**

```bash
cd applications/opengitbase-web
pnpm test:visual tests/visual/community-pitch.spec.ts
```

Verifies `/pitch` renders, CTA hrefs, header nav link, and exit navigation.

## Route reservation

`/pitch` is reserved at the routing layer so user/org slugs cannot collide:

- [useSidebarContext.ts](../applications/opengitbase-web/app/composables/useSidebarContext.ts) — `RESERVED_TOP_LEVEL`
- [slug-validation.ts](../applications/opengitbase-web/app/utils/slug-validation.ts) — `RESERVED_SLUGS`
