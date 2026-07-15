<!-- forge: #206 -->

# README on repository home

## Metadata

- ID: repo-browse-05
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Wire API **readme** endpoint through content authorization (returns markdown source + resolved filename for a ref). On repository home, render README **below** the root file tree (tree first, README second — per product decision).

Build a shared **safe markdown renderer** component:

- Disallow raw HTML in markdown source.
- Sanitize rendered HTML output (strip scripts, event handlers, dangerous URLs).

Omit README section entirely when no qualifying root file exists. Re-render when ref picker changes ref on home.

## Acceptance criteria

- [ ] Readme API returns source for default precedence (`README.md` first, case-insensitive)
- [ ] Home page shows file tree above rendered README for repos with a README
- [ ] Home page omits README section when no root readme file exists
- [ ] Markdown renderer blocks `<script>` and inline event handlers in rendered output
- [ ] Headings, lists, links, and fenced code blocks render correctly
- [ ] Changing ref updates README content when present
- [ ] API controller test for readme success and 404 when absent
- [ ] Unit tests for readme precedence resolution (via API or storage proxy layer)
- [ ] Component or e2e test: fixture repo with `README.md` shows rendered heading on home

## Blocked by

- [02-public-root-tree-web-ui.md](./02-public-root-tree-web-ui.md)
- [04-branch-tag-ref-picker-tree-navigation.md](./04-branch-tag-ref-picker-tree-navigation.md)

## User stories covered

- 2, 9, 27, 28, 29, 30, 31

## Notes

- Reuse the same renderer component in issue 07 for markdown blobs.
- README section uses the currently selected ref from the ref picker.
