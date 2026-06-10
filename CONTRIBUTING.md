# Contributing to OpenGitBase

Thank you for your interest in contributing. This project welcomes pull
requests, bug reports, and documentation improvements.

## Before you contribute

1. Read the [LICENSE](LICENSE) — contributions are licensed under the same
   terms as the project.
2. Read [docs/licensing.md](docs/licensing.md) if you are unsure how licensing
   applies to deployers vs contributors.

## Developer Certificate of Origin (DCO)

We use the [Developer Certificate of Origin (DCO)](https://developercertificate.org/)
version 1.1. Every commit in a pull request must include a `Signed-off-by`
line certifying the DCO:

```
Developer's Certificate of Origin 1.1

By making a contribution to this project, I certify that:

(a) The contribution was created in whole or in part by me and I have the right
    to submit it under the open source license indicated in the file; or

(b) The contribution is based upon previous work that, to the best of my
    knowledge, is covered under an appropriate open source license and I have
    the right under that license to submit that work with modifications,
    whether created in whole or in part by me, under the same open source
    license (unless I am permitted to submit under a different license), as
    indicated in the file; or

(c) The contribution was provided directly to me by some other person who
    certified (a), (b) or (c) and I have not modified it.

(d) I understand and agree that this project and the contribution are public
    and that a record of the contribution (including all personal information
    I submit with it, including my sign-off) is maintained indefinitely and may
    be redistributed consistent with this project or the open source license(s)
    involved.
```

### How to sign off

Add a sign-off line to each commit message:

```
Signed-off-by: Your Name <your.email@example.com>
```

Use `git commit -s` to add the line automatically:

```bash
git commit -s -m "fix: describe your change"
```

Or install the project hook (adds sign-off on every commit in this repo):

```bash
./scripts/install-git-hooks.sh
```

Pull requests without DCO sign-off on all commits will not be merged.

## Pull request guidelines

- Keep changes focused — one logical change per PR when possible.
- Update documentation when behavior changes.
- Follow existing code style in the files you edit.
- Describe what changed and why in the PR description.

## What you grant by contributing

By submitting a signed-off contribution, you agree that:

- Your contribution is licensed under the [LICENSE](LICENSE).
- The project may use, modify, and distribute your contribution as part of
  opengitbase, including in builds offered to commercial licensees.

We do not use a separate Contributor License Agreement (CLA). The DCO is
sufficient for this project.

## Questions

Open an issue or reach out to the maintainers if anything is unclear before
you invest significant effort.
