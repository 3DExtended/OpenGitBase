# Org egress approval on compute settings page

## Metadata

- ID: ci-rt-14
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Add org **Domain Allowance Request** review to the org compute settings page: list pending org-scoped requests and approve or deny using existing organization pipeline egress APIs. Org owners only.

Include Playwright visual snapshot coverage for the new section.

## Acceptance criteria

- [ ] Org compute settings page lists pending org egress domain requests
- [ ] Org owner can approve and deny requests; non-owner receives forbidden
- [ ] Approved domains appear in org egress allowlist used by effective allowlist API
- [ ] Visual regression snapshots committed for org compute page changes
- [ ] MSW handlers extended if needed for visual gallery fixtures

## Blocked by

- None — can start immediately

## User stories covered

- 25 — Org owners approve org-scoped **Domain Allowance Requests** from compute settings

## Notes

- Platform admin egress UI already exists at admin egress route.
- Runtime enforcement is ci-rt-10.
