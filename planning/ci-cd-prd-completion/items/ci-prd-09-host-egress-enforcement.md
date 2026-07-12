# Host egress enforcement in compute agent

## Metadata

- ID: ci-prd-09
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md (gap after ci-16)

## Parent

[PRD: CI/CD Pipelines](../../../docs/prd/ci-cd-pipelines.md)

## What to build

Enforce **Egress Policy on the compute host** during job execution (outside the guest). Resolve effective allowlists per hosting profile (`ogb-hosted`, `organization-self-hosted`, `community-hosted`) and block outbound connections to non-allowed domains with actionable log messages pointing authors to **Domain Allowance Requests**.

## Acceptance criteria

- [ ] Agent resolves effective allowlist via API or embedded policy composer before/during job
- [ ] `ogb-hosted` and `community-hosted` use platform allowlist only
- [ ] `organization-self-hosted` uses platform defaults ∪ org allowlist
- [ ] Denied connection produces log line naming domain and how to request allowance
- [ ] Integration test: denied domain fails with egress log; approved domain succeeds after workflow
- [ ] Egress enforcement applies during `installscript` and `script` network use

## Blocked by

- [ci-prd-07-firecracker-ogb-hosted-tracer.md](./ci-prd-07-firecracker-ogb-hosted-tracer.md)

## User stories covered

- 55 — Egress enforced on host
- 56 — Restricted default egress with allowlists
- 61 — Request new egress domain with justification
- 62 — Clear log messages when egress denied

## Notes

- Allowlist tables and domain-request API exist; this slice adds **runtime enforcement** in the agent/host network path.
- Implementation may use iptables/nftables, proxy deny rules, or equivalent — document chosen mechanism in slice notes when implementing.
