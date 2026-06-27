# Linked discussions sidebar

## Metadata

- ID: mr-15
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

**Cross-link UI** between merge requests and discussions (backend CRUD may start in mr-08 for closes-on-merge; this slice completes UX both directions).

**Merge request page:**

- Sidebar or Overview section **Linked discussions** with relationship badge (`closes`, `related`, `implements`)
- Add/remove links (author or Writer+); create form field from mr-06 wired to API
- Optional: parse `#n` tokens from description into links on save

**Discussion detail page:**

- **Linked merge requests** section when links exist
- Links use `!n` notation to MR detail

**API:**

- `GET/POST/DELETE .../merge-requests/{number}/discussion-links`
- Discussion detail includes linked MRs in projection or separate endpoint

## Acceptance criteria

- [ ] Link discussion #12 with type `closes` from MR UI
- [ ] Linked discussions listed on MR with type badge and link to discussion
- [ ] Linked MRs listed on discussion detail
- [ ] Remove link works for author/Writer+
- [ ] Invalid discussion number rejected
- [ ] closes behavior on merge verified in mr-08 still works with UI-created links
- [ ] API tests for CRUD and auth

## Blocked by

- [06-merge-request-core-api-and-ui-shell.md](./06-merge-request-core-api-and-ui-shell.md)
- [08-server-side-merge-and-discussion-closes-links.md](./08-server-side-merge-and-discussion-closes-links.md)

## User stories covered

- 12, 68, 69, 71

## Notes

- `related` and `implements` remain informational only in v1.
