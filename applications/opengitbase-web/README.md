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
| `pnpm sync:api` | Export OpenAPI spec and regenerate TS client |

Regenerate the API client from the repo root:

```bash
node scripts/sync-openapi.mjs
```

## Production deploy

Production hosting uses the Caddy reverse-proxy config in [`docker/Caddyfile`](docker/Caddyfile). It serves the static Nuxt SPA at `/` and proxies `/api` to the .NET API (with `/api` stripped before forwarding).

### Docker Compose

From the repo root, the `web` service builds this app and serves it on port **3000** with `/api` proxied to the `api` service:

```bash
docker compose up -d --build web api postgres
```

Open [http://localhost:3000](http://localhost:3000). The API remains available directly at [http://localhost:8080](http://localhost:8080).

Override branding at **image build time** via compose `build.args` (`NUXT_PUBLIC_INSTANCE_NAME`, `NUXT_PUBLIC_INSTANCE_LOGO_URL`, `NUXT_PUBLIC_API_BASE`).

Set `NUXT_PUBLIC_*` variables at **build time** (they are baked into the static bundle).

## Project layout

```
app/                  Nuxt 4 application code
  assets/main.css     Design tokens and shell styles
  components/         AppHeader, AppSidebar
  composables/        useInstanceBranding
  layouts/            default shell layout
  pages/              Route pages
i18n/locales/         Externalized UI strings
generated/api/        OpenAPI-generated client (regenerate via sync:api)
openapi/              Exported swagger.json
```
