<!-- forge: #141 -->

# SSH disable gate

## Metadata

- ID: git-https-06
- Type: AFK
- Status: ready
- Source: docs/prd/git-https-personal-access-tokens.md

## Parent

[PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md)

## What to build

Disable SSH git by default without removing code. `GIT_SSH_ENABLED=false` skips dispatcher `sshd` startup. HAProxy SSH TCP frontend and host port `:2211` move behind a Docker Compose `ssh` profile. API `git/config` returns `sshEnabled: false` by default.

Web UI hides SSH keys settings section with an explanatory note pointing users to Personal Access Tokens. All SSH code (`PublicGitSshKey`, fleet SSH keys, auth hook, SSH proxy) remains intact for re-enablement via flag + profile.

## Acceptance criteria

- [ ] `GIT_SSH_ENABLED` defaults to `false` in compose
- [ ] Dispatcher does not start `sshd` when SSH disabled
- [ ] HAProxy SSH frontend and `:2211` host port require `--profile ssh`
- [ ] `git/config` reflects `sshEnabled` from environment
- [ ] Web UI hides SSH keys link/section when `sshEnabled` is false
- [ ] `GIT_SSH_ENABLED=true` + `--profile ssh` restores full SSH git path
- [ ] No SSH-related code or tables removed

## Blocked by

- [01-git-access-tokens-and-settings-ui.md](./01-git-access-tokens-and-settings-ui.md)
- [05-haproxy-unified-http-routing.md](./05-haproxy-unified-http-routing.md)

## User stories covered

- 30, 31, 32, 35

## Notes

- SSH disable is configuration-only; do not delete tests for SSH paths — gate them or run under profile.
