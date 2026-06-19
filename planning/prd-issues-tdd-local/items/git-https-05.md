# git-https-05 — implementation record

## Status

- Branch: `feat/git-https-pat`
- Completion: **done**

## Summary

Unified HAProxy HTTP frontend on `:8080` routes `/api/*` to API (strips prefix), `/{owner}/{repo}.git/*` to dispatcher HTTP backends, default to web. `www` + git path → 301 apex redirect. Cloudflare tunnel targets `ssh-lb:8080`. Caddy proxies `/api` directly to API replicas.

## Tests

- `haproxy -c` config validation
- Manual: `./scripts/e2e-https-git-test.sh`
