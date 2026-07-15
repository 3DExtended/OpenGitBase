<!-- forge: #56 -->

# Repository access checks and DTO field redaction

## Metadata

- ID: sec-03
- Type: AFK
- Status: ready
- Source: code review (Jul 2026)

## What to build

Enforce repository privacy and membership on read endpoints, and stop leaking infrastructure fields (`PhysicalPath`, internal storage hosts/ports) to callers who should not see them.

**Behavior:**

- `GET /repository/by-slug/{owner}/{slug}` and `GET /repository/{id}` require the same access semantics as repository content browse: anonymous users may read public repos only; private repos return 404 to anonymous outsiders and 403 (or 404, per existing convention) to authenticated non-members.
- `GET /repository/{id}/usage` applies the same access check before returning usage.
- `GET /repository-member/{repositoryId}` returns members only when the caller can access the repository; enforce a minimum role to list members if needed.
- Public discovery (`GET /public/repositories`) and anonymous access-check success responses use a redacted DTO without `PhysicalPath`, `StorageNodeId`, or internal routing fields.
- Repository member create/update validates that the caller may grant the requested role (no Admin → Owner escalation by a lesser role).

## Acceptance criteria

- [ ] Anonymous user cannot read private repository metadata by slug or id (404 or consistent not-found)
- [ ] Authenticated non-member cannot read private repository metadata (403 or 404 per project convention)
- [ ] Member/owner can read private repository metadata they belong to
- [ ] Member list endpoint rejects callers without repository access
- [ ] Public repository list omits `PhysicalPath` and internal storage identifiers
- [ ] Access-check success response for anonymous/PAT callers omits internal host/port topology (or is restricted to dispatcher-only contract)
- [ ] Member role mutation rejects privilege escalation beyond caller's role
- [ ] API controller/integration tests cover anonymous, outsider, member, and owner matrix for affected endpoints

## Blocked by

- None — can start immediately

## Findings covered

- Critical: private repository metadata IDOR
- High: repository member list IDOR
- High: infrastructure disclosure via access-checks and public discovery
- Medium: repository member role mass assignment

## Notes

Align error semantics (403 vs 404 for private repos) with existing `RepositoryContentAuthorizationService` behavior so the web app does not need one-off handling per endpoint.
