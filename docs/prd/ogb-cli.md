<!-- forge: #20 -->

# PRD: `ogb` CLI (Forge Command-Line Tool)

## Problem Statement

OpenGitBase provides a web UI and REST API for repository hosting, authentication, and collaboration — including **Discussions** (threaded, numbered conversations with lifecycle states). Developers and automation authors who work from the terminal have no first-class way to interact with the platform without opening a browser or hand-crafting HTTP calls.

GitHub users expect a **`gh`-like CLI**: authenticate once, run commands from inside a git clone, and script common workflows. OpenGitBase needs the same ergonomics for its forge primitives, starting with authentication and **issue-style workflows** mapped to the existing Discussion domain.

Today, sign-in is optimized for the web SPA (httpOnly cookies and Bearer JWT from `POST /signin/login`). There is no loopback OAuth page, no local credential store, and no command-line entry point. Personal Access Tokens exist for git HTTPS only and do not authorize Discussion REST endpoints.

## Solution

Ship **`ogb`**, a .NET console application distributed as a global tool or standalone binary. The CLI talks to the OpenGitBase REST API over HTTPS, defaulting to **`https://www.opengitbase.com/`**, with an optional **`--hostname`** override for other deployments.

**Authentication** uses a **loopback browser flow** (similar to `gh auth login --web`):

1. The CLI starts a short-lived HTTP listener on `127.0.0.1`.
2. It opens the user's browser to `{host}/cli/auth?port=…&state=…`.
3. A new **Nuxt page** presents a username/password form (no Google/Apple buttons in v1).
4. On success, the page redirects the session JWT to the localhost callback URL.
5. The CLI stores host metadata in a config file and the JWT in the **OS credential store** (macOS Keychain, Windows Credential Manager, libsecret on Linux).

**Issue commands** are a user-facing alias for **Discussions**. The CLI exposes `ogb issue create`, `comment`, `close`, `list`, `view`, and `status`, backed by the existing Discussion API. Closing maps to **resolve** (default) or **dismiss** (`--reason dismissed`). Repository context is inferred from the git `origin` remote when inside a clone; `-R owner/repo` overrides.

Output is human-readable by default; **`--json`** emits structured JSON for scripting. When the stored JWT expires, commands fail with a clear message directing the user to run `ogb auth login` again.

## User Stories

### Discovery and installation

1. As a developer, I want to install `ogb` via `dotnet tool install` (or a published binary), so that I can use it without cloning the OpenGitBase repository.
2. As a developer, I want `ogb --help` to list available commands and global flags, so that I can discover capabilities from the terminal.
3. As a developer, I want `ogb --version` to print the CLI version, so that I can verify upgrades and report bugs accurately.

### Host configuration

4. As a developer, I want the CLI to default to `https://www.opengitbase.com/`, so that I can use it against production without extra configuration.
5. As a developer targeting a self-hosted instance, I want `--hostname` on any command to override the API base URL, so that I can work against local or staging deployments.
6. As a developer, I want the active hostname recorded in local config after login, so that subsequent commands know which instance to call without repeating the flag.

### Authentication — loopback login

7. As a developer, I want `ogb auth login` to open my browser to a login page on the target host, so that I authenticate through a familiar web form rather than typing secrets into the terminal.
8. As a developer, I want the login page to accept **username and password** only in v1, so that the flow is simple and matches accounts created via email registration.
9. As a developer, I want the browser to return control to the CLI automatically after login, so that I do not have to copy-paste tokens manually.
10. As a security-conscious user, I want the loopback flow to use a random **state** parameter validated on callback, so that cross-site redirect attacks cannot inject credentials into my CLI session.
11. As a security-conscious user, I want the auth page to redirect JWTs only to **`127.0.0.1` / `localhost`** on the port the CLI advertised, so that tokens are not sent to arbitrary hosts.
12. As a developer, I want `ogb auth status` to show whether I am logged in, which hostname is active, and which username the token represents (when decodable), so that I can confirm my session before scripting.
13. As a developer, I want `ogb auth logout` to remove stored credentials for the active host, so that shared machines can be cleared safely.

### Credential persistence

14. As a developer, I want to log in once and have subsequent `ogb` invocations reuse my session, so that daily terminal work does not require repeated authentication.
15. As a developer running shell scripts locally, I want stored credentials to be available to non-interactive `ogb` commands on the same machine, so that I can automate issue creation and comments without a browser.
16. As a security-conscious user, I want the JWT stored in the **OS keychain/credential store**, not in plain text in the config file, so that filesystem leaks are less likely to expose my session.
17. As a security-conscious user, I want host metadata config files written with restrictive permissions (`0600`), so that other users on the machine cannot read my OpenGitBase configuration.

### Session expiry

18. As a developer whose JWT has expired, I want commands to fail with an explicit **"session expired — run `ogb auth login`"** message, so that I understand the failure mode immediately.
19. As a developer, I want API `401 Unauthorized` responses to trigger the same clear re-login guidance, so that revoked or invalid tokens are handled consistently.

### Repository context

20. As a developer inside a git clone, I want `ogb issue` commands to infer `owner/repo` from the `origin` remote URL, so that I can run commands without repeating repository coordinates.
21. As a developer working outside a clone (or across repos), I want `-R owner/repo` (and `--repo`) to specify the target repository explicitly, so that I can manage discussions on any repo I can access.
22. As a developer with non-standard remotes, I want a clear error when repository context cannot be inferred and no `-R` flag was provided, so that I know to pass `--repo` explicitly.

### Issue create

23. As a signed-in user with read access, I want `ogb issue create --title "…"` to open a new **Discussion** in the current repository, so that I can file bugs and questions from the terminal.
24. As a signed-in user, I want `ogb issue create --body "…"` (or `--body-file path`) to include an opening comment body, so that I can provide context in one step.
25. As a signed-in user, I want the command to print the new discussion number and URL on success, so that I can share or follow up on the thread.
26. As a signed-in user without participate permission (blocked, or sign-in required), I want create to fail with the same semantics as the API (`401` / `403`), so that authorization stays consistent with the web UI.

### Issue comment

27. As a signed-in participant, I want `ogb issue comment 42 --body "…"` to add a comment to discussion `#42`, so that I can respond from CI scripts or terminal workflows.
28. As a signed-in participant, I want `ogb issue comment 42 --body-file path` to read comment text from a file, so that I can paste long markdown without shell escaping issues.
29. As a participant commenting on a **Resolved** or **Dismissed** discussion, I want the API's reopen-via-comment behavior to apply unchanged, so that the CLI does not need a separate reopen command in v1.

### Issue close

30. As a Writer+ repository member, I want `ogb issue close 42` to **resolve** the discussion by default, so that the common "mark fixed" action is one command.
31. As a Writer+ repository member, I want `ogb issue close 42 --reason dismissed` to **dismiss** the discussion, so that I can close non-actionable threads without a separate subcommand.
32. As a Reader without Writer role, I want close to fail with `403 Forbidden`, so that permission boundaries match the Discussion API.
33. As a developer, I want close to confirm the resulting status in human output (and JSON), so that scripts can verify the outcome.

### Issue list

34. As a signed-in user with read access, I want `ogb issue list` to show discussions for the current repository, so that I can triage from the terminal.
35. As a repository visitor, I want optional filters `--status open|engaged|resolved|dismissed` (or equivalent) passed through to the API, so that I can narrow results.
36. As a developer, I want list output to include number, title, status, and updated time at minimum, so that I can scan results quickly.

### Issue view

37. As a signed-in user with read access, I want `ogb issue view 42` to show discussion metadata and the comment thread, so that I have full context without opening a browser.
38. As a developer, I want `ogb issue view 42 --comments` (or comments included by default in view) to load the thread, so that one command replaces list + separate comment fetch when debugging.

### Issue status

39. As a developer, I want `ogb issue status 42` to print only the discussion lifecycle state (`Open`, `Engaged`, `Resolved`, `Dismissed`), so that scripts can gate logic on whether a thread is still active.
40. As a developer, I want `ogb issue status 42 --json` to emit structured status for parsing, so that automation can branch without fragile text parsing.

### Output and scripting

41. As a developer, I want human-friendly tables and labels by default, so that interactive use is pleasant.
42. As a developer writing scripts, I want `--json` on all issue and auth commands that produce data, so that I can pipe output to `jq` or other tools.
43. As a developer, I want `--json` errors to include HTTP status and API error bodies when available, so that scripts can log actionable failure details.
44. As a developer, I want non-zero exit codes on failure (auth, network, 4xx, 5xx), so that shell scripts and CI steps fail visibly.

### CLI auth page (web)

45. As a developer logging in via CLI, I want the `/cli/auth` page to be visually consistent with the OpenGitBase web app, so that I trust the login prompt.
46. As a developer, I want the page to show which hostname/instance I am authenticating against, so that I do not accidentally log in to the wrong deployment.
47. As a developer entering invalid credentials, I want an inline error on the auth page without breaking the loopback flow, so that I can retry without restarting the CLI listener.
48. As a developer registering a new account, I want a clear message that CLI login requires an existing account (with link to web registration if applicable), so that I understand why login failed for unknown users.

## Implementation Decisions

### Product mapping

- **`ogb issue` is a CLI alias for Discussions**, not a new domain entity. No database schema or API changes are required for issue CRUD in v1.
- User-facing language uses **"issue"** in the CLI; API and domain language remain **Discussion**.
- Discussion lifecycle states map as follows for CLI output:
  - `Open`, `Engaged` → treated as "open" in casual messaging; `status` command uses exact enum names.
  - `Resolved` ← default `ogb issue close`
  - `Dismissed` ← `ogb issue close --reason dismissed`
- Reopen is **comment-only** per existing Discussion rules; no `ogb issue reopen` in v1.

### Modules (deep interfaces)

The implementation should favor **deep modules** — narrow public surfaces that encapsulate complexity and are testable in isolation.

#### 1. `IHostResolver`

- **Responsibility:** Resolve API base URL from default (`https://www.opengitbase.com/`), `--hostname` flag, and persisted config.
- **Interface:** `GetActiveHost()`, `NormalizeHost(string input)` (scheme, trailing slash, `/api` prefix rules).
- **Notes:** All HTTP traffic targets `{host}/api/…` or the same path convention the web client uses (verify against existing HAProxy/Caddy routing).

#### 2. `IConfigStore`

- **Responsibility:** Read/write per-user config (XDG: `~/.config/ogb/hosts.yml` or platform equivalent).
- **Stores:** Active hostname, logged-in username (optional cache), last login timestamp — **not** the JWT.
- **Interface:** `GetActiveHost()`, `SetActiveHost()`, `Clear()`.
- **File mode:** `0600` on write.

#### 3. `ICredentialStore`

- **Responsibility:** Store and retrieve JWT per normalized hostname using OS secure storage.
- **Interface:** `SaveToken(host, token)`, `GetToken(host)`, `DeleteToken(host)`, `HasToken(host)`.
- **Platform backends:** macOS Keychain, Windows Credential Manager, libsecret (Linux).
- **Test double:** In-memory implementation for unit tests (no keychain in CI).

#### 4. `ILoopbackAuthServer`

- **Responsibility:** Bind ephemeral `127.0.0.1` port, serve `/callback`, validate `state`, capture token, signal completion to CLI host process.
- **Interface:** `StartAsync()` → `{ port, state, callbackUrl }`, `WaitForTokenAsync(timeout)`, `Stop()`.
- **Security:** Reject callbacks missing or mismatching `state`; accept only one successful callback per session.

#### 5. `IOgbApiClient`

- **Responsibility:** HTTP calls to OpenGitBase REST endpoints with Bearer auth from `ICredentialStore`.
- **Interface (v1):**
  - Auth: `LoginAsync(username, password)` (used by web page, not CLI directly — listed for test harnesses)
  - Discussions: `ListDiscussions`, `GetDiscussion`, `CreateDiscussion`, `CreateComment`, `ResolveDiscussion`, `DismissDiscussion`
- **Behavior:** Attach `Authorization: Bearer {jwt}`; map 401 to typed `SessionExpiredException` with user-facing message.
- **No OpenAPI codegen required for v1** — hand-maintained DTOs mirroring existing API contracts keep the CLI project lightweight; codegen can be a follow-up.

#### 6. `IGitRemoteResolver`

- **Responsibility:** Parse `owner` and `slug` from git `origin` URL for HTTPS and SSH remotes.
- **Interface:** `TryResolveFromWorkingDirectory()` → `Option<RepoSlug>`, with `-R` override taking precedence.
- **Supported patterns:** `https://{host}/{owner}/{repo}.git`, `git@{host}:{owner}/{repo}.git`, and variants with/without `.git`.

#### 7. Command router (`System.CommandLine`)

- **Responsibility:** Parse global options (`--hostname`, `--json`), subcommands (`auth`, `issue`), dispatch to handlers.
- **Handlers are thin:** validate input → call deep modules → format output.

#### 8. Output formatters

- **Responsibility:** Human tables vs JSON serialization behind `IOutputWriter`.
- **Interface:** `WriteIssue`, `WriteIssueList`, `WriteAuthStatus`, `WriteError`.
- **JSON shape:** Stable property names (`number`, `title`, `status`, `url`, `comments`, etc.).

#### 9. Nuxt `/cli/auth` page

- **Responsibility:** Read `port` and `state` query params; render username/password form; POST to existing `POST /signin/login`; on success redirect to `http://127.0.0.1:{port}/callback?token={jwt}&state={state}`.
- **Validation:**
  - Require `port` (integer, ephemeral range) and `state` (non-empty).
  - Do not redirect if login returns non-success.
  - URL-encode token in redirect query string.
- **No new API endpoints** for login — reuse existing sign-in controller.
- **Registration flow:** If login fails for unknown user, show error; do not attempt OAuth registration redirect (`redirect{apiKey}`) in v1.

### Project placement

- New .NET console project: **`applications/OpenGitBase.Cli/`**
- Packaged as **`ogb`** executable (assembly name / tool command).
- Added to solution; CI builds and tests the CLI project.

### API endpoints consumed (existing)

| CLI action | HTTP |
|------------|------|
| Login (via web page) | `POST /signin/login` |
| List issues | `GET /repository/by-slug/{owner}/{slug}/discussions` |
| View issue | `GET /repository/by-slug/{owner}/{slug}/discussions/{number}?include=comments` |
| Create issue | `POST /repository/by-slug/{owner}/{slug}/discussions` |
| Comment | `POST /repository/by-slug/{owner}/{slug}/discussions/{number}/comments` |
| Close (resolve) | `POST /repository/by-slug/{owner}/{slug}/discussions/{number}/resolve` |
| Close (dismiss) | `POST /repository/by-slug/{owner}/{slug}/discussions/{number}/dismiss` |

### JWT lifetime

- Production JWT expiry is configured at **86400 seconds (24 hours)** in appsettings; CLI must not assume longer-lived sessions.
- v1 does **not** implement refresh tokens, silent re-login, or password storage in keychain.

### Assumptions

- Users logging in via CLI already have accounts (email/password registration completed on web).
- Discussion API accepts Bearer JWT the same way as the web SPA (already true for authenticated requests).
- Default production host serves both web UI and API under the same hostname with path-based routing.
- Anchored comments, tags, assignee, and sub-thread operations are **out of scope** for v1 issue commands.

## Testing Decisions

### Principles

- Test **observable behavior** through module interfaces and command exit codes, not internal command-line parsing details.
- Prefer **in-memory test doubles** for `ICredentialStore`, `ILoopbackAuthServer`, and HTTP (mock `HttpMessageHandler`) over live network calls in unit tests.
- Integration tests may spin up the API test host (existing `AuthTestServerConfiguration` patterns) for end-to-end HTTP verification.

### Modules to test

| Module | Focus | Prior art |
|--------|-------|-----------|
| `IGitRemoteResolver` | HTTPS/SSH URL parsing; missing remote; `-R` override | Git URL parsing in dispatcher tests |
| `IHostResolver` | Default host; `--hostname`; normalization | — |
| `ICredentialStore` | In-memory double; save/get/delete round-trip | — |
| `ILoopbackAuthServer` | State validation; single callback; timeout | — |
| `IOgbApiClient` | Bearer header; 401 → session expired; discussion CRUD request shapes | `E2eApiClient`, `RepositoryDiscussionsControllerTests` |
| Output formatters | Human vs `--json` snapshots | Controller test JSON assertions |
| Command handlers | Exit codes; flag validation; repo context errors | `System.CommandLine` testing patterns |

### Integration scenarios

- Login flow (mocked browser redirect): CLI receives token → stores in credential store → `auth status` shows logged in.
- `issue create` with mocked API returns number and URL.
- `issue close` default calls resolve endpoint; `--reason dismissed` calls dismiss endpoint.
- Expired token: API returns 401 → CLI exit code non-zero → stderr mentions `ogb auth login`.
- Inside temp git repo with `origin` set → commands resolve owner/slug without `-R`.

### Web page tests

- Nuxt component or route test: valid query params render form; successful login triggers redirect URL with token and state.
- Invalid credentials show error without redirect.

### Out of scope for automated tests in v1

- Live OS keychain integration in CI (use memory double only in pipelines).
- Cross-browser manual QA of loopback flow (document smoke test checklist).
- Visual regression of `/cli/auth` page.

## Out of Scope

- **CI/CD pipeline authentication** (dedicated tokens, `OGB_TOKEN` env var, headless service accounts) — deferred to a follow-up PRD.
- **OAuth provider buttons** (Google, Apple) on the CLI auth page — username/password only in v1.
- **Device authorization grant** (user code + poll) — loopback only in v1.
- **New Issue entity** — Discussions remain the backend; CLI naming only.
- **Merge request commands** (`ogb pr …`).
- **Git operations** (`ogb clone`, repo create) — PAT/git HTTPS is separate.
- **Discussion tags, assignee, anchors, sub-threads** in CLI flags.
- **Silent JWT refresh** or storing password in keychain for re-auth.
- **Multi-account concurrent sessions** beyond one active host config (switching hosts re-login is sufficient for v1).
- **Shell completion** (`ogb completion`) — nice follow-up.
- **Windows/Linux keychain parity testing** beyond best-effort implementation (macOS primary dev platform).

## Further Notes

### Relationship to Discussions PRD

The [Repository Discussions PRD](repository-discussions.md) defines domain behavior (lifecycle, permissions, reopen-via-comment). This PRD adds a **terminal client** only. Any Discussion API or authorization change must remain backward-compatible with the CLI or be versioned explicitly.

### Relationship to Git HTTPS PAT PRD

Personal Access Tokens authenticate git Smart HTTP, not Discussion REST endpoints in the current architecture. The CLI uses **session JWT** from password login. A future PRD may add PAT scopes for API access suitable for CI; this PRD explicitly defers that work.

### Suggested implementation order (tracer bullets)

1. **CLI-01 — Project scaffold:** `applications/OpenGitBase.Cli/`, `System.CommandLine`, host resolver, config store, in-memory credential store, API client shell.
2. **CLI-02 — Loopback auth:** localhost server, `ogb auth login/logout/status`, wire credential store (memory first).
3. **CLI-03 — Nuxt `/cli/auth` page:** form, login POST, redirect with state validation.
4. **CLI-04 — OS keychain backends:** replace memory store as default on supported platforms.
5. **CLI-05 — Issue commands:** git remote resolver, create/comment/close/list/view/status, human output.
6. **CLI-06 — JSON output and exit codes:** `--json`, structured errors.
7. **CLI-07 — Tests:** unit tests for deep modules; API integration tests for discussion commands; web route test for auth page.

### Naming reference

| CLI (user-facing) | Domain / API |
|-----------------|--------------|
| issue | Discussion |
| issue close (default) | resolve |
| issue close --reason dismissed | dismiss |
| issue status | DiscussionStatus enum |
| auth login | POST /signin/login + loopback |

### Security checklist (implementation)

- Validate `state` on loopback callback before accepting token.
- Restrict redirect target to `127.0.0.1` / `localhost` only.
- Never log JWT contents.
- Config file mode `0600`; keychain entry scoped by service name (e.g. `opengitbase-cli/{hostname}`).
- Auth page must not echo password in URL or client-side storage.
