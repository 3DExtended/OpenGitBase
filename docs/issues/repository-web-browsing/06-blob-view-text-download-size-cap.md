<!-- forge: #207 -->

# Blob view — text, download, size cap

## Metadata

- ID: repo-browse-06
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Add Nuxt route `/{owner}/{repo}/blob/{ref}/{path}` and wire blob + raw API endpoints through content authorization.

**Blob view behavior:**

- Text files under **1 MB**: display with **client-side syntax highlighting** (Shiki or equivalent); language from file extension with plain-text fallback.
- Files over **1 MB**: no inline body; show message and download action.
- Binary files: “Binary file not shown” + download link.
- **Raw/download** endpoint returns bytes; UI exposes Download button for all blob types.

Link file rows from tree views to blob routes.

## Acceptance criteria

- [ ] `/blob/{ref}/{path}` renders syntax-highlighted text for a small `.ts` or `.py` file
- [ ] Blob over 1 MB shows too-large message without inline content
- [ ] Binary file (e.g. `.zip`) shows not-shown message with download link
- [ ] Download button fetches raw endpoint and saves/displays file
- [ ] Blob and raw API endpoints enforce content authorization from issue 03
- [ ] API controller tests: text blob, oversized blob metadata, binary flag
- [ ] Unit test for 1 MB cap boundary (at cap vs over cap)
- [ ] Automated test: open known file from tree → blob page shows expected content snippet

## Blocked by

- [04-branch-tag-ref-picker-tree-navigation.md](./04-branch-tag-ref-picker-tree-navigation.md)

## User stories covered

- 11, 19, 20, 21, 24, 25

## Notes

- Image inline preview and SVG rules are issue 07.
- OpenAPI sync after new blob/raw routes are stable.
