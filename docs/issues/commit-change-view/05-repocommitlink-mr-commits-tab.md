<!-- forge: #67 -->

# RepoCommitLink and MR Commits tab

## Metadata

- ID: cv-05
- Type: AFK
- Status: ready
- Source: docs/prd/commit-change-view.md

## Parent

[PRD: Commit Change View (Clickable Commits & Per-Commit Diff)](../../prd/commit-change-view.md)

## What to build

Shared commit link component and merge request **Commits** tab integration — the first user-facing “click a commit” flow.

**`RepoCommitLink` component:**

- Props: `owner`, `repo`, `sha`, optional display mode (`short` default), optional `from` context string.
- Builds path `/{owner}/{repo}/commit/{sha}` with optional `?from=...` query.
- Monospace link styling consistent with existing forge accent links.
- Internal path helper reusable outside the component.

**MR Commits tab:**

- Entire commit card is one clickable target (message + metadata row).
- Links include `from=mr/{number}` for context-aware back navigation on the commit page.
- Card hover/focus states indicate clickability.

Migrate commit page parent SHA links to use `RepoCommitLink`.

## Acceptance criteria

- [ ] `RepoCommitLink` renders correct href with short SHA display by default
- [ ] `from` query appended when context prop provided
- [ ] MR **Commits** tab: clicking any commit card navigates to commit page for that SHA
- [ ] Commit page back link returns to originating merge request when opened from MR Commits tab
- [ ] Commit page parent SHAs use `RepoCommitLink`
- [ ] Playwright visual regression: MR **Commits** tab with clickable cards
- [ ] E2E click-through (compose stack): seed repo with multiple commits → open MR → **Commits** tab → click commit → assert commit page message/diff → back link returns to MR detail

## Blocked by

- [04-commit-page-shell.md](./04-commit-page-shell.md)

## User stories covered

- 21–26, 32

## Notes

- **First demo milestone** for the feature set.
- E2E prior art: `MergeRequestE2eTests`, `BrowseE2eTests` navigation patterns.
- Per-commit diff may differ from MR **Changes** cumulative diff; no UI copy should imply they are the same.
