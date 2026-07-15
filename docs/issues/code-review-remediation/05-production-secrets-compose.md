<!-- forge: #58 -->

# Production secrets and compose profile separation

## Metadata

- ID: sec-05
- Type: AFK
- Status: ready
- Source: code review (Jul 2026)

## What to build

Ensure production deployments cannot start with committed dev secrets, debug bypasses, or an internet-exposed dev compose stack.

**Behavior:**

- API startup in `Production` environment fails fast (or logs fatal and refuses to serve) when known dev placeholder secrets are detected (`Jwt:Key`, encryption pepper, platform merge token, default admin seed password).
- Provide a production-oriented compose profile or `docker-compose.prod.yml` without `ASPNETCORE_ENVIRONMENT=Development`, without `Debug__Features__EmailVerification`, without publishing postgres/redis to the host, and with documented secret override requirements.
- E2E endpoints (`/internal/e2e/*`) require both environment gate **and** explicit configuration flag; default production compose does not enable them.
- Caddy (or front proxy) adds baseline security headers: `X-Content-Type-Options`, `X-Frame-Options` or CSP `frame-ancestors`, and a conservative CSP or documented rationale if deferred.
- README or ops doc lists required env overrides for a safe production deploy.

## Acceptance criteria

- [ ] Production startup rejects or aborts on known dev JWT/encryption/admin seed values
- [ ] Production compose file (or profile) uses `Production` environment and disables debug email verification
- [ ] Default production compose does not publish database/redis ports to host
- [ ] E2E database reset unreachable with default production configuration
- [ ] Caddy config includes at least `nosniff` and clickjacking protection headers
- [ ] Deployment checklist documents all secret overrides

## Blocked by

- None — can start immediately

## Findings covered

- Critical: committed default secrets in appsettings
- Critical: E2E database reset on misconfiguration
- High: default docker-compose not production-safe
- Medium: debug email-verification bypass in Development compose
- Low: no security headers in Caddy config

## Notes

Do not remove dev defaults from `appsettings.Development.json` — only guard Production. Consider a `SecretsValidator` hosted service run at startup.
