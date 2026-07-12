# CI variables, org context, cancel write ACL

## Metadata

- ID: ci-rt-03
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Extend the pipeline scheduler **CI Variable Composer** to emit the full v1 predefined set including `CI_PROJECT_PATH`, `CI_PROJECT_PATH_SLUG`, and `CI_JOB_ID`. When the repository owner is an organization, set `CI_ORGANIZATION_ID` for egress and policy resolution.

Enforce repository **write access** on **Job Cancellation**: users without write permission receive 403; anonymous and read-only users cannot cancel.

Wire **Job Execution User** from parsed YAML (`user: ogb|root`) into the agent execution protocol environment (for vsock guest agent in ci-rt-08).

## Acceptance criteria

- [ ] Scheduled jobs include `CI_PROJECT_PATH`, `CI_PROJECT_PATH_SLUG`, `CI_JOB_ID` in environment JSON
- [ ] Org-owned repositories set `CI_ORGANIZATION_ID`; user-owned repositories omit it
- [ ] Cancel endpoint requires repository write access; forbidden for read-only and anonymous callers
- [ ] YAML `user: root` is available to the agent for script execution user selection
- [ ] Unit or integration tests cover cancel ACL and variable composition

## Blocked by

- None — can start immediately

## User stories covered

- 39 — Users without write access cannot cancel jobs
- 41 — Full predefined `CI_*` variables in scripts
- 42 — `CI_ORGANIZATION_ID` for org-owned repositories

## Notes

- VM teardown on cancel is ci-rt-13; this slice is authorization and scheduler variables only.
