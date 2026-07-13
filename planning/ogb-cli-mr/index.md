# `ogb mr` — work items

**Source PRD:** [docs/prd/ogb-cli-mr.md](../../docs/prd/ogb-cli-mr.md)

Vertical slices for merge request commands on the OpenGitBase CLI (`ogb mr`), backed by the existing Merge Request REST API. Builds on completed `ogb` v1 (auth + issue commands).

**Prerequisite:** `ogb auth login` and `ogb issue` infrastructure shipped on `main`.

## Work items

| Order | ID | Title | Type | Status | Blocked by |
|-------|-----|-------|------|--------|------------|
| 1 | [mr-01](./items/mr-01.md) | MR API client and `mr list` | AFK | ready | — |
| 2 | [mr-02](./items/mr-02.md) | `ogb mr view` | AFK | ready | mr-01 |
| 3 | [mr-03](./items/mr-03.md) | `ogb mr status` | AFK | ready | mr-01 |
| 4 | [mr-04](./items/mr-04.md) | `ogb mr diff` | AFK | ready | mr-01 |
| 5 | [mr-05](./items/mr-05.md) | Git branch resolver and `mr create` | AFK | ready | mr-01 |
| 6 | [mr-06](./items/mr-06.md) | `ogb mr close` | AFK | ready | mr-01 |
| 7 | [mr-07](./items/mr-07.md) | `ogb mr ready` | AFK | ready | mr-01 |
| 8 | [mr-08](./items/mr-08.md) | `ogb mr edit` | AFK | ready | mr-01 |
| 9 | [mr-09](./items/mr-09.md) | `ogb mr approve` | AFK | ready | mr-01 |
| 10 | [mr-10](./items/mr-10.md) | `ogb mr merge` | AFK | ready | mr-01 |
| 11 | [mr-11](./items/mr-11.md) | MR integration tests | AFK | ready | mr-05, mr-10 |
| 12 | [mr-12](./items/mr-12.md) | Compose E2E smoke | AFK | ready | mr-11 |

## Dependency graph

```
mr-01 ──┬──► mr-02
        ├──► mr-03
        ├──► mr-04
        ├──► mr-05 ──┐
        ├──► mr-06   │
        ├──► mr-07   ├──► mr-11 ──► mr-12
        ├──► mr-08   │
        ├──► mr-09   │
        └──► mr-10 ──┘
```

## Parallelism

After **mr-01**, items **mr-02** through **mr-10** may proceed in parallel (except **mr-11** waits for **mr-05** and **mr-10**).

## Verification (each item)

- **mr-01–mr-10:** `dotnet test tests/OpenGitBase.Cli.Tests`
- **mr-11:** `dotnet test tests/OpenGitBase.Cli.Integration.Tests`
- **mr-12:** compose stack + `dotnet test` filter `CliMrE2eTests` or `scripts/test-ogb-cli-mr-e2e.sh`

## Out of scope (deferred)

- `OGB_TOKEN` / headless auth
- MR review comments, discussion links
- Pipeline checks (`ogb run`)
- `ogb pr` alias
