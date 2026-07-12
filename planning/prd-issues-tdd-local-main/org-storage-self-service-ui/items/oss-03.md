## Summary

Refactored `/{org}/storage` with extracted components (quota, nodes, enrollments, placement), enrollment form + bootstrap snippet/download, inline node edit (capacity + hosting scope), 403 owner-only UX, i18n.

## Linked Context

- PRD: `docs/prd/org-storage-self-service-ui.md`
- Work item: `oss-03`

## Status

- Branch: `main`
- Tests: `pnpm test -- app/utils/orgStorageBootstrap.test.ts`
- Commit(s): `a17cf90`
