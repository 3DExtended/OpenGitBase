<!-- forge: #140 -->

# HAProxy unified HTTP routing + Cloudflare tunnel

## Metadata

- ID: git-https-05
- Type: AFK
- Status: ready
- Source: docs/prd/git-https-personal-access-tokens.md

## Parent

[PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md)

## What to build

Add a unified HTTP frontend on the HAProxy load balancer container. Route `/api/*` to API backends, `/{owner}/{repo}.git/*` to dispatcher HTTP backends, and default traffic to the web UI. Redirect `www` hostname git paths to apex with `301` (preserve path and query).

Repoint Cloudflare tunnel to the unified HAProxy HTTP frontend instead of API directly. Wire compose for dispatcher HTTP ports and LB host port exposure. Update bootstrap/rolling-update scripts if they assume API-only tunnel target.

## Acceptance criteria

- [ ] Single HAProxy HTTP frontend routes API, git, and web by path ACL
- [ ] Git path ACL matches `^/[^/]+/[^/]+\.git`
- [ ] `www` + git path → 301 redirect to apex hostname
- [ ] Dispatcher HTTP backends load-balanced across dispatcher fleet
- [ ] Cloudflare tunnel targets unified frontend in compose
- [ ] `docker compose up` allows HTTPS git via HAProxy localhost port without Cloudflare
- [ ] README or compose comments document operator Cloudflare hostname setup

## Blocked by

- [04-dispatcher-smart-http-proxy.md](./04-dispatcher-smart-http-proxy.md)

## User stories covered

- 10, 27, 28, 29, 43

## Notes

- Operator Cloudflare dashboard configuration is documented but not automated.
- Web and API existing ports may be consolidated or remapped — preserve local dev ergonomics.
