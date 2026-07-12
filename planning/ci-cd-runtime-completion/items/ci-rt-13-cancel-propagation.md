# Cancel propagation (Kafka + poll + VM teardown)

## Metadata

- ID: ci-rt-13
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

When an authorized user cancels a running **Job**, publish `ci.job.cancelled` to Kafka for platform agent wake. During in-guest execution, the agent polls job status and destroys the MicroVM promptly on `cancelled`. Org agents without Kafka rely on poll during execution.

Ensure cancel does not leave running host processes or Firecracker instances after terminal cancel state.

## Acceptance criteria

- [ ] Cancel with write access publishes `ci.job.cancelled` and marks job `cancelled`
- [ ] Agent poll during execution detects `cancelled` and tears down MicroVM
- [ ] Platform agent Kafka consumer wakes on cancel topic
- [ ] Contract test: cancel without write access returns 403 (from ci-rt-03)
- [ ] Cancelled job does not continue to `script` after install phase

## Blocked by

- [ci-rt-03-ci-variables-cancel-acl.md](./ci-rt-03-ci-variables-cancel-acl.md) (ci-rt-03)
- [ci-rt-06-firecracker-launcher.md](./ci-rt-06-firecracker-launcher.md) (ci-rt-06)

## User stories covered

- 38 — Write user can cancel and tear down MicroVM
- 40 — Kafka cancel wake plus in-flight poll for all agents

## Notes

- Kafka topic bootstrap alongside existing `ci.job.available`.
- Process sandbox cancel should also stop in-flight work where applicable.
