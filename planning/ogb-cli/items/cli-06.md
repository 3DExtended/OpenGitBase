# OS keychain credential storage

## Metadata

- ID: cli-06
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Replace the in-memory credential store as the production default with platform-specific secure storage: macOS Keychain, Windows Credential Manager, and libsecret on Linux. Keep the in-memory implementation as a test double for unit tests and CI. Service name scoped per hostname (e.g. `opengitbase-cli/{hostname}`).

## Acceptance criteria

- [ ] Production CLI uses OS credential store on supported platforms
- [ ] In-memory store used in automated tests (no keychain in CI)
- [ ] Save/get/delete round-trip tested via in-memory double
- [ ] JWT never written to plain-text config file
- [ ] Best-effort implementation on Windows/Linux; macOS is primary dev platform

## Blocked by

- [cli-04](./cli-04.md)

## User stories covered

- 16 — JWT in OS keychain, not plain config
- 17 — config file restrictive permissions (JWT exclusion verified)

## Notes

- Live keychain integration is out of scope for CI; document manual verification on macOS.
