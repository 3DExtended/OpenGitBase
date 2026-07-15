<!-- forge: #205 -->

# Branch/tag ref picker and tree navigation

## Metadata

- ID: repo-browse-04
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Wire API **branches** and **tags** list endpoints through content authorization. Add Nuxt routes:

- `/{owner}/{repo}/tree/{ref}/{path?}` — directory listing for any nested path.

Add a **ref picker** component with **Branches | Tags** tabs. Ref is encoded in the URL. When switching refs, preserve the current path where the path exists on the new ref; otherwise fall back to that ref’s root.

Update repository home to use the ref picker and link directory rows to `/tree/{ref}/{path}` or `/blob/{ref}/{path}` (blob route fully wired in issue 06).

## Acceptance criteria

- [ ] Branches and tags API endpoints return lists for authorized callers
- [ ] `/tree/{ref}` and `/tree/{ref}/nested/path` render directory tables
- [ ] Ref picker shows Branches tab and Tags tab with correct lists
- [ ] Changing ref updates URL and reloads tree; path preserved when valid
- [ ] Breadcrumb or path display shows current directory path
- [ ] API controller tests for branches/tags list endpoints
- [ ] Automated test: navigate from home → subfolder → switch branch → verify URL and content update
- [ ] i18n strings for ref picker labels and empty path root

## Blocked by

- [02-public-root-tree-web-ui.md](./02-public-root-tree-web-ui.md)

## User stories covered

- 10, 15, 17, 18

## Notes

- Tag names with special characters must be URL-encoded in routes.
- Default ref on home still follows issue 02 rules when no ref in URL.
