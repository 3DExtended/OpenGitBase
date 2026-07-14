---
name: cli-goldens
description: Testing pattern for ogb and OpenGitBase CLI tools — stub HTTP behavior tests plus JSON golden files for stable agent-facing output. Use when adding or changing CLI commands.
---

# CLI golden tests

Hybrid testing for `applications/OpenGitBase.Cli/`.

## Layer 1 — Behavior tests (always)

Use `StubHttpMessageHandler` + `CliApp.RunAsync` with `CliTestSupport.CreateOverrides`.

Assert:

- HTTP method and path (including query params)
- Exit code
- Key substrings in human output
- Request count for multi-step commands

Prior art: `IssueCommandExtendedTests`, `MergeRequestCommandTests`, `OgbApiClientTests`.

## Layer 2 — JSON goldens (stable contracts)

For commands agents parse with `jq` (`list`, `create`, `status`, `view --json`):

- Capture normalized JSON to `tests/OpenGitBase.Cli.Tests/Goldens/<command>.json`
- Normalize volatile fields (timestamps, GUIDs) in test helper before compare
- Fail when field names or structure change without updating golden

Add goldens when:

- `--json` schema is part of the public contract
- Human output is not the automation surface

Skip goldens for:

- One-off error messages
- Commands with fully dynamic content

## Layer 3 — Integration

`OpenGitBase.Cli.Integration.Tests` against in-process `WebApplicationFactory` for lifecycle flows.

## Layer 4 — Compose E2E

`OpenGitBase.E2E.Tests` with `[RequiresComposeFact]` for smoke against real stack.

## Meta-test

`CommandHandlerCoverageTests` requires each `*CommandHandlers` type to have matching `*CommandTests` or `*CommandExtendedTests` in the test assembly.

## Example test shape

```csharp
var handler = new StubHttpMessageHandler();
handler.EnqueueResponse(HttpStatusCode.OK, "[]");
var exit = await CliApp.RunAsync(
    ["--hostname", "https://forge.example.com", "mr", "-R", "acme/demo", "list"],
    output, error,
    CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));
Assert.Equal(0, exit);
Assert.Contains("status=open", handler.Requests.Single().RequestUri!.Query);
```
