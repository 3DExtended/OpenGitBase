# Progress Log — Encrypted Replica Storage (Phase 3)

| When | Item | Action | Result |
|------|------|--------|--------|
| 2026-07-10 | ers-15 | Cross-org encrypted replica placement engine | a4eb387 |
| 2026-07-10 | ers-16 | Per-repo MaxBytesOverride API, enforcement, settings UI | 20fc361 |

## Verification (ers-16)

- `dotnet test` — RepositoryByteOverrideServiceTests, access-check override, usage handler
- `pnpm test:visual` — `repo-byte-override` gallery snapshots
- Migration: `20260710212728_AddRepositoryMaxBytesOverride`
