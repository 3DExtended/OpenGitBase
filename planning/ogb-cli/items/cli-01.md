# CLI project bootstrap

## Metadata

- ID: cli-01
- Type: AFK
- Status: ready
- Source: docs/prd/ogb-cli.md

## Parent

[PRD: `ogb` CLI (Forge Command-Line Tool)](../../../docs/prd/ogb-cli.md)

## What to build

Create the `ogb` .NET console application project under `applications/OpenGitBase.Cli/`, add it to the solution, and wire CI to build it. Implement the root command using `System.CommandLine` with `--help` and `--version` so the tool is invokable and discoverable before any feature commands exist.

## Acceptance criteria

- [ ] New console project builds as part of the solution
- [ ] Executable/tool name is `ogb`
- [ ] `ogb --help` lists the root command and global options placeholder
- [ ] `ogb --version` prints the assembly/package version
- [ ] CI pipeline includes the CLI project in build (and test once tests exist)

## Blocked by

- None — can start immediately

## User stories covered

- 1 — installable/distributable CLI project exists
- 2 — `--help` for discovery
- 3 — `--version` for upgrades and bug reports

## Notes

- Packaging as `dotnet tool` can land in this slice or a follow-up; minimum bar is a runnable project from the repo.
- No API or web changes in this slice.
