# PRD: `ogb docs pull` and discussion links

## Problem Statement

Agents and humans adopted a **forge-first** spec workflow: PRDs, ADRs, and work slices are Discussions in the product repo, linked to each other. Today:

- `ogb issue` covers create/list/view/comment/close on Discussions
- MRs support **discussion links** via API; **discussion-to-discussion links do not**
- There is no `ogb docs pull` to export forge content into `docs/prd/`, `docs/adr/`, `docs/issues/`
- Skills fall back to dual-writing mirror files during bootstrap

Without links and pull, agents cannot maintain a single canonical spec graph or a git mirror automatically.

## Solution

1. **Discussion link API** — CRUD links between discussions in a repository (relationship types: `parent`, `child`, `related`, `blocks`).
2. **`ogb issue link`** — Create/list/remove links from the CLI.
3. **`ogb docs pull`** — Export discussions matching `[PRD]`, `[ADR]`, `[slice]` title prefixes (and linked graph) to mirror paths.
4. **Optional `ogb docs push`** — Detect local mirror drift and update discussions (lower priority than pull).

## User Stories

1. As an agent authoring a PRD, I want to publish a Discussion and receive a number, so that slices can link to it.
2. As an agent, I want `ogb issue link 43 --parent 42`, so that slice #43 is machine-linked to PRD #42.
3. As an agent, I want `ogb docs pull` to refresh `docs/prd/` from forge, so that git search matches canonical specs.
4. As a developer, I want ADRs exported to `docs/adr/NNNN-slug.md`, so that ADR numbering stays consistent.
5. As CI (future), I want to fail when mirror is stale vs forge, so that exports stay in sync.

## Implementation Decisions

### API

- New endpoints under `repository/by-slug/{owner}/{slug}/discussions/{number}/links` (mirror MR link pattern)
- Entity: `DiscussionLinkEntity` with `(SourceDiscussionId, TargetDiscussionId, RelationshipType)`
- Authorization: same as discussion participate/read rules by link direction

### CLI

- Extend `IOgbApiClient` with link + export methods
- `ogb issue link`, `ogb issue links` subcommands
- `ogb docs pull [--prefix PRD|ADR|slice]` writes mirror tree
- Normalize bodies to markdown files; embed `<!-- forge: #N -->` front matter

### Export layout

| Title prefix | Mirror path |
|--------------|-------------|
| `[PRD]` | `docs/prd/{slug}.md` |
| `[ADR]` | `docs/adr/{nnnn}-{slug}.md` |
| `[slice]` | `docs/issues/{id}.md` |

### Modules

- Feature: extend `Discussion` feature or small `DiscussionLink` submodule
- CLI: `DiscussionLinkCommandHandlers`, extend `DocsCommandHandlers`

## Testing Decisions

- API: controller tests + authorization matrix
- CLI: stub HTTP + integration test link + pull against in-process API
- E2E: compose smoke publish → link → pull → assert file exists
- Follow `cli-goldens` for `ogb docs pull --json` inventory output

## Out of Scope

- Cross-repository links
- Wiki-style non-discussion docs
- Bidirectional sync conflict resolution (pull-only v1)

## Further Notes

- Unblocks removal of bootstrap dual-write in `.agents/docs.md`
- Enables CI meta-test phase D (mirror freshness)
- Parent skill: `publish-docs`, `engineering-contract`

## Suggested slices

1. Discussion link API + domain
2. `ogb issue link` / `ogb issue links`
3. `ogb docs pull` export
4. CI freshness check (optional)
