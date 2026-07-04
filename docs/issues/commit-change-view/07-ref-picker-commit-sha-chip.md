# Ref picker commit SHA chip

## Metadata

- ID: cv-07
- Type: AFK
- Status: ready
- Source: docs/prd/commit-change-view.md

## Parent

[PRD: Commit Change View (Clickable Commits & Per-Commit Diff)](../../prd/commit-change-view.md)

## What to build

Show the tip commit SHA beside the ref picker on repository browse surfaces, linked to the commit change view without changing default ref → tree browse behavior.

**Behavior:**

- Branch/tag `<select>` continues to control tree browse navigation (unchanged).
- Visible monospace SHA chip beside the picker shows `commitSha` for the currently selected ref.
- Chip uses `RepoCommitLink` (short display); no `from` context required (repo breadcrumb fallback on commit page).
- Update all pages using `RepoRefPicker` (repo home, tree browse, blob browse as applicable).

**Non-goals:**

- Do not make ref names open the commit page.
- Dropdown option labels need not show SHA unless it fits without clutter (chip is the primary entry point).

## Acceptance criteria

- [ ] Selected branch ref shows tip SHA chip linking to commit page
- [ ] Selected tag ref shows tip SHA chip linking to commit page
- [ ] Changing ref in picker updates chip SHA without full page reload beyond existing browse refresh
- [ ] Ref name selection still navigates to / opens tree browse as today
- [ ] Chip absent or disabled gracefully when `commitSha` unavailable (empty repo edge case)

## Blocked by

- [05-repocommitlink-mr-commits-tab.md](./05-repocommitlink-mr-commits-tab.md)

## User stories covered

- 29, 30, 31

## Notes

- Can be implemented in parallel with **cv-06** after **cv-05** lands.
- Completes scope **E** (every commit SHA surfaced in repo UI is actionable).
