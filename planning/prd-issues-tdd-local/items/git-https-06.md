# git-https-06 — implementation record

## Status

- Branch: `feat/git-https-pat`
- Completion: **done**

## Summary

`GIT_SSH_ENABLED=false` by default in compose. Dispatcher entrypoint skips `sshd` when disabled. SSH TCP LB moved to `docker-compose.ssh.yml` with `--profile ssh`. API `git/config` reflects env via existing `Startup.cs` wiring.

## Tests

- Dispatcher healthcheck HTTP-only by default
- SSH profile documented in README
