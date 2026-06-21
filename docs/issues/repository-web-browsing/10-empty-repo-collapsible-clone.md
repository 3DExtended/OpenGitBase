# Empty repository state and collapsible clone

## Metadata

- ID: repo-browse-10
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

When a repository has **no commits** (no branches / empty git state):

- Show an **empty state** message on the repository home page.
- **Hide** file tree and README sections entirely.
- Show **clone/push instructions** in a **collapsible** section (collapsed by default on repos with content; expanded by default on empty repos).

For repositories **with content**, move existing clone/push cards into a collapsible **“Clone repository”** section on home so code browsing dominates the page.

## Acceptance criteria

- [ ] Newly created repo with no pushes shows empty state without tree or readme sections
- [ ] Empty repo shows clone/push guidance in expanded collapsible section
- [ ] Repo with commits shows tree (+ readme when present); clone section is collapsible and **collapsed by default**
- [ ] Expanding clone section shows HTTPS (and SSH when enabled) instructions from existing copy/i18n
- [ ] API returns explicit empty-repo signal (empty branches list or dedicated flag) that UI handles without error toast
- [ ] API test: content endpoints for empty repo return appropriate empty response (not 500)
- [ ] Automated test: empty repo page has no directory table; repo with commits has directory table
- [ ] i18n keys for empty state title/body and collapsible clone section label

## Blocked by

- [02-public-root-tree-web-ui.md](./02-public-root-tree-web-ui.md)

## User stories covered

- 13, 14

## Notes

- Can ship in parallel with issues 04–07; only depends on issue 02 home shell.
- Reuse existing clone URL helpers from current repo overview page.
