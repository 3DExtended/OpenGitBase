# Agent guide — OpenGitBase

## Read order

1. [`.agents/skills/engineering-contract/SKILL.md`](skills/engineering-contract/SKILL.md) — TDD + forge-first docs
2. **[docs/PROJECT-STATE.md](../docs/PROJECT-STATE.md)** — what is implemented, how components interact, encryption posture
3. **[state.md](state.md)** — enabled stacks and features (current snapshot)
4. **[code-structure.md](code-structure.md)** — repo layout, features, CLI, web
5. **[testing.md](testing.md)** — test layers, compose, meta-tests
6. **[docs.md](docs.md)** — PRD/ADR/slices via `ogb` (forge-first)
7. **[overview.md](overview.md)** — high-level repo layout
8. **[cli.md](cli.md)** — agentGenCli commands
9. **[skills/README.md](skills/README.md)** — project skill index (`/tdd`, `/publish-docs`, …)
10. Stack guides (check `state.md`):
   - **[backend.md](backend.md)** — .NET / CQRS / EF / API
   - **[frontend.md](frontend.md)** — Flutter (not used; web is Nuxt)

## Safe-change rules

- **Add features via CLI** — `agentGenCli new backend-feature` / `new frontend-feature`. Do not recreate the folder or project layout by hand.
- **Respect CLI markers** — the tool patches marked blocks in `FeatureRegistration.cs` and `app_router.dart`. Prefer extending via new features over editing markers unless you know the pattern.
- **Regenerate, don't hand-edit** — OpenAPI Dart client (`lib/generated/swaggen/`), golden baselines (use project scripts / `flutter test --update-goldens`).
- **Check state first** — commands and directory trees differ when backend or frontend was skipped at init.

## Manifest

[`.agentGenCli.json`](../.agentGenCli.json) records project name, init options, feature lists, and every agentGenCli command run.
