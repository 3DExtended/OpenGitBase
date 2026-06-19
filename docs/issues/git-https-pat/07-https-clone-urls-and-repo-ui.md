# Repository HTTPS clone URLs + settings navigation

## Metadata

- ID: git-https-07
- Type: AFK
- Status: ready
- Source: docs/prd/git-https-personal-access-tokens.md

## Parent

[PRD: Git HTTPS via Personal Access Tokens](../../prd/git-https-personal-access-tokens.md)

## What to build

Update repository overview to show HTTPS clone URL: `{gitBaseUrl}/{owner}/{repo}.git` using API `gitBaseUrl`, with fallback that strips `www.` from browser origin. Add link to Personal Access Tokens settings from clone instructions. Add i18n strings for HTTPS clone hints.

When SSH is disabled, demote or remove SSH clone URL in favor of HTTPS. When SSH enabled, show both or SSH secondary per product judgment.

## Acceptance criteria

- [ ] Repo overview displays `https://opengitbase.com/{owner}/{repo}.git` when API config set
- [ ] Fallback strips `www.` from `window.location.origin` when API unavailable
- [ ] Clone section links to PAT settings page
- [ ] i18n keys for HTTPS clone title and hint text
- [ ] SSH clone URL hidden when `sshEnabled` is false
- [ ] Settings nav includes PAT link (from issue 01)

## Blocked by

- [01-git-access-tokens-and-settings-ui.md](./01-git-access-tokens-and-settings-ui.md)
- [06-ssh-disable-gate.md](./06-ssh-disable-gate.md)

## User stories covered

- 9, 33, 34

## Notes

- Website hosted at `www.opengitbase.com`; displayed clone URLs use apex without `www`.
