<!-- forge: #167 -->

# Approvals and merge gates

## Metadata

- ID: mr-07
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Human **approval** workflow and pluggable **merge gates** framework. v1 registers only the **RequiredApprovals** gate; CI/CD pipeline gates plug in later without renaming states.

**Approval records:** `(mergeRequestId, userId, commitSha, createdAt)`.

**Rules:**

- Writer+ may approve; MR author cannot self-approve
- Draft MRs cannot be approved
- Required count from matching protected-branch rule on target (default 0 when unprotected)
- When `approvalCount >= requiredCount` at current `sourceHeadSha`, transition **Open → Approved**
- When source HEAD changes and rule `dismissApprovalsOnPush` applies, dismiss all approvals and transition **Approved → Open**
- Force-push dismisses approvals same as new commits

**API:**

- `POST .../approve`
- Approval list on MR detail

**Web UI:**

- Approve button (eligible users only)
- Approval count widget (e.g. “1 of 2 required”)
- Status badge **Approved**

**Extensibility:**

- `IMergeGate` (or equivalent) registry; v1 provider: `RequiredApprovalsGate`

## Acceptance criteria

- [ ] Writer+ not author can approve Open MR
- [ ] Author and Reader approve attempts rejected
- [ ] Open → Approved when required count met at current HEAD
- [ ] Approved → Open when new commit pushed and dismiss rule enabled
- [ ] Draft cannot be approved
- [ ] Unprotected target: required count 0 allows immediate Approved when published (or document Open→Approved on zero required)
- [ ] Gate registry interface exists with RequiredApprovals provider only
- [ ] Web UI shows approval state and button
- [ ] Handler tests for eligibility, dismiss, and Open↔Approved transitions

## Blocked by

- [06-merge-request-core-api-and-ui-shell.md](./06-merge-request-core-api-and-ui-shell.md)

## User stories covered

- 20, 21, 24, 25, 26, 27, 28, 29, 30, 31

## Notes

- Hook approval dismiss to push events via SHA refresh on MR read or explicit webhook/poll — acceptable v1: refresh on MR detail load after push.
