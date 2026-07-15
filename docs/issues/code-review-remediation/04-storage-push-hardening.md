<!-- forge: #57 -->

# Storage destructive ops and push enforcement hardening

## Metadata

- ID: sec-04
- Type: AFK
- Status: ready
- Source: code review (Jul 2026)

## What to build

Harden the storage internal HTTP API and git hooks so destructive operations, push rule enforcement, and blob serving cannot be abused even with a valid storage bearer token or container-network access.

**Behavior:**

- `DELETE /internal/repos` rejects `physicalPath` equal to the repos root (`/srv/git`); only individual repository directories may be deleted.
- `pre-receive` push validation **fails closed** when the API URL is missing — pushes are rejected with a clear error, not silently accepted.
- `sync-from` (or equivalent) restricts `sourceHost` to an allowlist of known storage/dispatcher hosts, preventing arbitrary internal SSRF.
- Raw blob download enforces a maximum size (consistent with inline blob cap or a documented higher limit) to prevent memory exhaustion.
- Git Smart HTTP on port `:8082` is not anonymously reachable from the Docker network without authentication (bind to localhost, require mTLS, or proxy-only access — align with **sec-02** trust model).

## Acceptance criteria

- [ ] DELETE with repos root path returns 400/403 and does not remove data
- [ ] DELETE with valid per-repo path still works in integration test
- [ ] Push with missing API URL configuration is rejected by `pre-receive`
- [ ] `sync-from` with disallowed host is rejected
- [ ] Raw blob endpoint rejects or streams-with-cap for objects above size limit
- [ ] Port `:8082` git HTTP is not usable for anonymous push/fetch from arbitrary containers (document expected network layout)
- [ ] Storage unit tests cover repos-root rejection, fail-closed push validation, and blob cap

## Blocked by

- [Internal network trust behind reverse proxy](./02-internal-network-trust.md) — recommended; `:8082` and push-validation exposure overlap with internal trust boundaries

## Findings covered

- High: storage DELETE can wipe entire repos root
- High: push validation fails open when API URL missing
- Medium: storage Git HTTP on `:8082` without authentication
- Medium: `sync-from` SSRF on storage internal API
- Medium: unbounded blob download (DoS)

## Notes

Repos-root validation is independent and can land early if **sec-02** is delayed. Fail-closed push validation is high priority for any internet-exposed storage node.
