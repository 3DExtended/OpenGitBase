<!-- forge: #55 -->

# Internal network trust behind reverse proxy

## Metadata

- ID: sec-02
- Type: HITL
- Status: ready
- Source: code review (Jul 2026)

## What to build

Fix the internal-network security model so fleet, push-validation, and storage-registration endpoints are not reachable from the public internet when traffic passes through HAProxy, Cloudflare tunnel, or Docker reverse proxies.

**Decision required (HITL):** Choose and document one primary approach:

- Trusted forwarded headers (`X-Forwarded-For` / `Forwarded`) with explicit proxy allowlist, or
- mTLS / service tokens on fleet endpoints (replacing or supplementing IP allowlists), or
- Network segmentation only (document that API must not be tunnel-published without additional guards)

**Behavior (after decision):**

- `InternalNetworkMiddleware` correctly identifies external vs internal callers behind reverse proxies.
- Storage node re-registration cannot authenticate via a spoofable `X-Storage-Node-Certificate-Thumbprint` header alone.
- `/internal/e2e/*` and other sensitive internal prefixes are in `RestrictedPathPrefixes` or equivalent guards.
- `E2ETest` environment behavior is documented; middleware disable is intentional for tests only.

## Acceptance criteria

- [ ] Architecture decision recorded (ADR or issue Notes) for internal trust model
- [ ] Proxied external request to a restricted internal path returns 403 (integration test with simulated proxy headers)
- [ ] Storage node re-register requires cryptographic proof of identity, not header thumbprint alone
- [ ] E2E reset/emails endpoints unreachable from external callers in default production configuration
- [ ] HAProxy / compose documentation updated with required proxy header or mTLS settings

## Blocked by

- None — can start immediately (HITL gate is the trust-model decision)

## Findings covered

- Critical: internal-network IP bypass behind reverse proxy
- High: push-validation trusts caller-supplied role when internal guard fails
- Medium: storage node certificate thumbprint from client header
- Critical: E2E database reset on misconfiguration (path restriction overlap)

## Notes

`InternalNetworkMiddleware` currently uses `context.Connection.RemoteIpAddress` only. Docker/HAProxy traffic appears as `172.x.x.x` and is treated as internal. This slice unblocks **sec-04** push-validation hardening that depends on a trustworthy internal boundary.

### Trust model decision (Jul 2026)

**Primary approach:** trusted forwarded headers (`X-Forwarded-For`) from an explicit proxy allowlist configured via `InternalNetwork:TrustedProxyNetworks` and `InternalNetwork:TrustedProxyAddresses`. HAProxy already sets `option forwardfor`; the API enables `UseForwardedHeaders()` so `RemoteIpAddress` reflects the original client when the immediate peer is a trusted proxy. Storage node identity uses mTLS client certificates only — the `X-Storage-Node-Certificate-Thumbprint` header is ignored without a validated TLS client cert.

**E2ETest:** `InternalNetworkMiddleware` is disabled intentionally in the `E2ETest` environment only; production and development keep it enabled with forwarded-header trust.
