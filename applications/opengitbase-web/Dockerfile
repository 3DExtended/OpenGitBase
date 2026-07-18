# syntax=docker/dockerfile:1
# Build static Nuxt SPA, then serve via Caddy with /api reverse proxy.
# Build context: repository root (see docker-compose.yml).
FROM node:22-alpine AS build

WORKDIR /app

ENV PNPM_HOME="/pnpm"
ENV PATH="${PNPM_HOME}:${PATH}"
RUN corepack enable

COPY applications/opengitbase-web/package.json applications/opengitbase-web/pnpm-lock.yaml ./
RUN --mount=type=cache,id=opengitbase-pnpm,target=/pnpm/store \
    pnpm install --frozen-lockfile --store-dir=/pnpm/store

COPY applications/opengitbase-web/ .

COPY VERSION /repo/VERSION
COPY scripts/docker/resolve-deploy-version.sh /repo/scripts/docker/resolve-deploy-version.sh

ARG DEPLOY_BUMP=0
ARG GIT_SHA=unknown
ARG NUXT_PUBLIC_INSTANCE_NAME=OpenGitBase
ARG NUXT_PUBLIC_INSTANCE_LOGO_URL=
ARG NUXT_PUBLIC_API_BASE=/api
ARG NUXT_PUBLIC_SITE_GATE_ENABLED=true

ENV DEPLOY_BUMP="${DEPLOY_BUMP}"
ENV GIT_SHA="${GIT_SHA}"
ENV NUXT_PUBLIC_INSTANCE_NAME="${NUXT_PUBLIC_INSTANCE_NAME}"
ENV NUXT_PUBLIC_INSTANCE_LOGO_URL="${NUXT_PUBLIC_INSTANCE_LOGO_URL}"
ENV NUXT_PUBLIC_API_BASE="${NUXT_PUBLIC_API_BASE}"
ENV NUXT_PUBLIC_SITE_GATE_ENABLED="${NUXT_PUBLIC_SITE_GATE_ENABLED}"
ENV NUXT_PUBLIC_MSW=false

RUN --mount=type=bind,source=.deploy-patch,target=/repo/.deploy-patch,rw \
    eval "$(sh /repo/scripts/docker/resolve-deploy-version.sh)" && \
    pnpm generate

FROM caddy:2-alpine AS runtime

RUN apk add --no-cache curl

COPY applications/opengitbase-web/docker/Caddyfile /etc/caddy/Caddyfile
COPY applications/opengitbase-web/docker/entrypoint.sh /docker/entrypoint.sh
RUN chmod +x /docker/entrypoint.sh
COPY --from=build /app/.output/public /srv

EXPOSE 8080
ENTRYPOINT ["/docker/entrypoint.sh"]
