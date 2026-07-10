# Agent onboarding

This project was scaffolded with **agentGenCli** and has since grown into a distributed Git forge. Start here before making changes.

1. Read [`docs/PROJECT-STATE.md`](docs/PROJECT-STATE.md) — current implementation baseline, component interactions, encryption posture
2. Read [`.agents/state.md`](.agents/state.md) — which stacks and features are enabled in *this* project
3. Read [`.agents/README.md`](.agents/README.md) — navigation, conventions, safe-change rules
4. Open stack-specific guides only if they exist (see `state.md`):
   - [`.agents/backend.md`](.agents/backend.md) — when backend is dotnet
   - [`.agents/frontend.md`](.agents/frontend.md) — when frontend is flutter

Project manifest (command history): [`.agentGenCli.json`](.agentGenCli.json)

Prefer `agentGenCli new …` and `agentGenCli project …` over hand-rolling feature structure.
