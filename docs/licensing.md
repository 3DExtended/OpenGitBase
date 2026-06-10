# Licensing FAQ

This FAQ explains common scenarios. It is **informational only** — the
[LICENSE](../LICENSE) is the binding document.

## Is opengitbase open source?

No. OpenGitBase is **source-available**: code is public, but production use has
conditions. It is not an OSI-approved open source license.

## Quick decision guide

```text
Are you in dev/test only?
  → Yes: free, no restrictions

Are you hosting for people outside your organization?
  → Yes: commercial license required (any size)

Is use solely for an OSI-licensed open-source project?
  → Yes: free in production (any sponsor size)

Is use internal only AND ARR < $1M AND employees ≤ 300?
  → Yes: free in production

Otherwise:
  → commercial license required
```

## Worked examples

| Scenario | Free production use? |
|----------|----------------------|
| Solo developer on a $5 VPS for personal repos | Yes |
| 50-person startup, $500K ARR, internal git server | Yes |
| 400-employee company, internal git server | No — commercial license |
| $2M ARR company, 80 employees, internal use | No — commercial license |
| 10-person startup offering "Git hosting" to paying customers | No — commercial license (third-party access) |
| Linux Foundation hosts an instance for kernel.org (OSI project) | Yes — open-source project exemption |
| Large company hosts opengitbase only for contributors to their OSI-licensed project | Yes — open-source project exemption |
| Company with 200 employees evaluating opengitbase in staging | Yes — development/testing |
| Agency deploys opengitbase for a client's internal use | Depends — the **client** organization must meet free-tier rules or hold a commercial license |

## Development and testing

Anyone may use opengitbase in development, CI, QA, and staging environments
without a commercial license, as long as those environments are not serving
production workloads or external users.

## Modifications and forks

- You **may** modify the code and keep changes private (no copyleft).
- You **may** publish forks, but must comply with [TRADEMARK.md](../TRADEMARK.md)
  (rename public forks; do not imply official status).
- Forks remain subject to the LICENSE — you cannot remove usage restrictions
  for downstream users.

## Third-party hosting

**Any** production deployment that gives access to parties outside your
organization requires a commercial license, including:

- Managed git hosting products
- Multi-tenant SaaS
- White-label or resale offerings
- Free-tier hosting for external users

Internal use by your employees and contractors does not count as third-party
access.

## Open-source project exemption

An open-source project means software publicly distributed under an
[OSI-approved license](https://opensource.org/licenses). The exemption applies
when opengitbase is used **solely** to develop, build, test, or distribute that
project — not as general IT infrastructure for the sponsoring organization.

## Enforcement

Version 1 uses an honor system. There are no license keys or telemetry-based
checks. You are responsible for determining whether your use requires a
commercial license.

## Getting a commercial license

See [COMMERCIAL-LICENSE.md](../COMMERCIAL-LICENSE.md). Contact the maintainers
when commercial licensing is available.

## Legal review

Custom licenses involve legal risk. Organizations should have counsel review
the [LICENSE](../LICENSE) before production deployment, especially near the
$1M ARR or 300-employee thresholds.
