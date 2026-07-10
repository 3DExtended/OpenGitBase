# Agent guide — OpenGitBase

## Read order

1. **[docs/PROJECT-STATE.md](../docs/PROJECT-STATE.md)** — what is implemented, how components interact, encryption posture
2. **[state.md](state.md)** — enabled stacks and features (current snapshot)
3. **[overview.md](overview.md)** — high-level repo layout
4. **[cli.md](cli.md)** — agentGenCli commands available in this repo
4. Stack guides (only if present — check `state.md`):
   - **[backend.md](backend.md)** — .NET / CQRS / EF / API (when backend=dotnet)
   - **[frontend.md](frontend.md)** — Flutter app (when frontend=flutter)

## Safe-change rules

- **Add features via CLI** — `agentGenCli new backend-feature` / `new frontend-feature`. Do not recreate the folder or project layout by hand.
- **Respect CLI markers** — the tool patches marked blocks in `FeatureRegistration.cs` and `app_router.dart`. Prefer extending via new features over editing markers unless you know the pattern.
- **Regenerate, don't hand-edit** — OpenAPI Dart client (`lib/generated/swaggen/`), golden baselines (use project scripts / `flutter test --update-goldens`).
- **Check state first** — commands and directory trees differ when backend or frontend was skipped at init.

## Manifest

[`.agentGenCli.json`](../.agentGenCli.json) records project name, init options, feature lists, and every agentGenCli command run.
