# OpenGitBase Web

Static Nuxt 4 SPA for the OpenGitBase Git forge. Ships as pre-built assets behind a reverse proxy with the .NET API on `/api`.

## Stack

- Nuxt 4 (`ssr: false`)
- Nuxt UI 4, Pinia, VueUse
- `@nuxtjs/i18n` (English only at launch)
- OpenAPI-generated TypeScript client (`@hey-api/openapi-ts`)

## Prerequisites

- Node.js 20+
- [pnpm](https://pnpm.io/) 10+
- .NET 10 SDK (for API client generation)

## Setup

```bash
cd applications/opengitbase-web
pnpm install
```

Copy environment defaults:

```bash
cp .env.example .env
```

## Development

Start the .NET API (default `http://localhost:5000`), then run the Nuxt dev server:

```bash
pnpm dev
```

The dev server listens on `http://localhost:3000` and proxies `/api` to the backend via Vite (`NUXT_DEV_API_PROXY`, default `http://localhost:5000`). This mirrors production same-origin routing so httpOnly cookie auth works consistently.

## Public status page

Anonymous visitors can open `/status` for live fleet health (website, API, Git, storage, and data stores) plus 90-day history charts. Operators manage incident banners at `/admin/status`. See [docs/prd/public-status-dashboard.md](../../docs/prd/public-status-dashboard.md) for the full design.

### Instance branding

| Variable | Default | Description |
|----------|---------|-------------|
| `NUXT_PUBLIC_INSTANCE_NAME` | `OpenGitBase` | Shown in header and page titles |
| `NUXT_PUBLIC_INSTANCE_LOGO_URL` | *(empty)* | Optional logo URL; falls back to icon |
| `NUXT_PUBLIC_API_BASE` | `/api` | API base path for the generated client |
| `NUXT_DEV_API_PROXY` | `http://localhost:5000` | Dev-only Vite proxy target |

## Scripts

| Command | Description |
|---------|-------------|
| `pnpm dev` | Start dev server with `/api` proxy |
| `pnpm build` | Production build (static SPA output) |
| `pnpm lint` | ESLint |
| `pnpm typecheck` | Vue/TS type checking |
| `pnpm test` | Vitest unit tests |
| `pnpm test:visual` | Playwright visual and UI regression tests |
| `pnpm sync:api` | Export OpenAPI spec and regenerate TS client |

## Community pitch (`/pitch`)

Reveal.js contributor deck (fullscreen, no app shell). Edit slide copy in `app/data/communityPitchSlides.ts` and URLs in `app/utils/communityPitchLinks.ts`.

See [docs/community-pitch.md](../../docs/community-pitch.md) for messaging baseline, editing guide, and test commands.

Regenerate the API client from the repo root:

```bash
node scripts/sync-openapi.mjs
```

## Production deploy

Production hosting uses the Caddy reverse-proxy config in [`docker/Caddyfile`](docker/Caddyfile). It serves the static Nuxt SPA at `/` and proxies `/api` to the .NET API (with `/api` stripped before forwarding).

### Docker Compose

From the repo root, copy `docker-compose.override.example.yml` to `docker-compose.override.yml`, fill in secrets (Cloudflare tunnel token), then:

```bash
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --build
```

Fleet enrollment tokens are written by `./scripts/bootstrap-fleet.sh` into `docker-compose.override.yml`.

Override branding at **image build time** via the `web.build.args` section in `docker-compose.override.yml` (`NUXT_PUBLIC_INSTANCE_NAME`, `NUXT_PUBLIC_INSTANCE_LOGO_URL`, `NUXT_PUBLIC_API_BASE`, `NUXT_PUBLIC_SITE_GATE_ENABLED`).

**Site gate policy:** production Docker images default to `NUXT_PUBLIC_SITE_GATE_ENABLED=false`. When enabled for local dev (`pnpm dev`), the gate is a cosmetic preview lock only — the password is not shipped in production bundles and the middleware does not run outside `import.meta.dev`.

Set `NUXT_PUBLIC_*` variables at **build time** (they are baked into the static bundle).

## Project layout

```
app/                  Nuxt 4 application code
  assets/main.css     Design tokens and shell styles
  components/         AppHeader, AppSidebar
  composables/        useInstanceBranding
  layouts/            default shell layout
  pages/              Route pages (includes /pitch community deck)
i18n/locales/         Externalized UI strings
generated/api/        OpenAPI-generated client (regenerate via sync:api)
openapi/              Exported swagger.json
```
