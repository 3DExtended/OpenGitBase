# Build static Nuxt SPA, then serve via Caddy with /api reverse proxy.
FROM node:22-alpine AS build

WORKDIR /app

RUN corepack enable

COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .

ARG NUXT_PUBLIC_INSTANCE_NAME=OpenGitBase
ARG NUXT_PUBLIC_INSTANCE_LOGO_URL=
ARG NUXT_PUBLIC_API_BASE=/api
ARG NUXT_PUBLIC_SITE_GATE_ENABLED=true

ENV NUXT_PUBLIC_INSTANCE_NAME=$NUXT_PUBLIC_INSTANCE_NAME
ENV NUXT_PUBLIC_INSTANCE_LOGO_URL=$NUXT_PUBLIC_INSTANCE_LOGO_URL
ENV NUXT_PUBLIC_API_BASE=$NUXT_PUBLIC_API_BASE
ENV NUXT_PUBLIC_SITE_GATE_ENABLED=$NUXT_PUBLIC_SITE_GATE_ENABLED
ENV NUXT_PUBLIC_MSW=false

RUN pnpm generate

FROM caddy:2-alpine AS runtime

COPY docker/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/.output/public /srv

EXPOSE 8080
