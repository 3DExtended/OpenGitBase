---
name: to-prd-local
description: Turn conversation context into a PRD published as a forge Discussion (forge-first), then sync git mirror. Use when user wants a PRD from current context. Follow /publish-docs — do not write docs/prd/ as primary output.
---

# To PRD (forge-first)

Produces a PRD as a **Discussion** on the forge. Git `docs/prd/` is updated via `ogb docs pull`, not authored directly.

Read [/engineering-contract](../engineering-contract/SKILL.md) and [/publish-docs](../publish-docs/SKILL.md) first.

## Process

1. Explore the repo if needed. Use domain glossary and ADRs.

2. Sketch modules to build or modify (deep modules). Confirm with user if the workflow requires it.

3. Draft the PRD using the template below in a **temp file** or string.

4. **Publish** (required):

   ```bash
   ogb auth login   # if needed
   ogb issue create --title "[PRD] Feature name" --body-file /tmp/prd-draft.md
   ```

   Record the discussion number and URL from output.

5. **Sync mirror**:

   ```bash
   ogb docs pull
   git add docs/prd && git commit -m "docs: sync PRD discussion #N"
   ```

6. Report: forge URL, discussion number, mirror path.

Do **not** skip publish and write only local files.

<prd-template>

## Problem Statement

The problem that the user is facing, from the user's perspective.

## Solution

The solution to the problem, from the user's perspective.

## User Stories

A LONG, numbered list of user stories. Each user story should be in the format of:

1. As an <actor>, I want a <feature>, so that <benefit>

This list should be extensive and cover all aspects of the feature.

## Implementation Decisions

Modules, interfaces, architectural decisions, schema changes, API contracts. No stale file paths unless necessary.

## Testing Decisions

External behavior only; which modules; prior art in codebase.

## Out of Scope

## Further Notes

</prd-template>
