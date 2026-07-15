<!-- forge: #35 -->

# Pipeline YAML parser + v1 validation

## Metadata

- ID: ci-02
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-pipelines.md

## Parent

[PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)](../../prd/ci-cd-pipelines.md)

## What to build

Implement the **Pipeline Definition Parser** deep module: parse and validate `.opengitbase-ci.yml` v1, resolve **Pipeline Defaults** onto jobs, extract stages (with first-seen fallback ordering), enforce required `runs-on`, validate `only` glob patterns, and produce a structured **Pipeline Definition** suitable for scheduling. Include a pure glob matcher and contract tests aligned with the published JSON Schema in the web application.

This slice is a library with tests only — no API or scheduler wiring yet.

## Acceptance criteria

- [ ] `ParsePipelineDefinition(yamlText)` returns a typed definition or structured validation errors
- [ ] Pipeline defaults for `image`, `dependencies`, and `variables` merge correctly into per-job **Resolved Job** output
- [ ] Missing `stages:` uses first-seen job order; explicit `stages:` controls stage sequence
- [ ] Every job without `runs-on` is rejected with a clear validation error
- [ ] `only` glob patterns are validated and matchable against ref names (`*`, no `**` in v1)
- [ ] Contract tests cover representative valid and invalid YAML fixtures
- [ ] Parser rules stay consistent with the web app JSON Schema

## Blocked by

None — can start immediately.

## User stories covered

- 1 — Define CI in `.opengitbase-ci.yml` at the repository root.
- 2 — Published JSON Schema for editor validation.
- 3 — Optional `stages:` with first-seen fallback ordering.
- 4 — Pipeline-wide defaults for `image`, `dependencies`, and `variables`.
- 5 — Override defaults per job.
- 6 — Every job requires `runs-on`.
- 7 — `only` glob filters on branch/tag names.
- 8 — Parallel jobs within a stage (definition extraction).
- 9 — Sequential stages (definition extraction).
- 10 — Different `runs-on` values within the same stage.
- 11 — Predefined immutable `CI_*` variables (shape in resolved job).
- 12 — Custom `variables:` for non-reserved names.
- 13 — `GIT_DEPTH` in variables.
- 14 — Ordered `dependencies:` with `installscript`.
- 15 — Dependency `version` labels for humans only.
- 19 — `script` runs as `ogb` by default.
- 20 — Optional `user: root` for scripts.

## Notes

- `installscript` runs as root; `script` default user is `ogb` — encode in resolved job metadata for the executor.
- Docker/container tooling is only valid when declared as a dependency (story 18 validated at parse or resolve time).
