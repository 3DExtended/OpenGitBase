# Job Identity security boundary

| Credential | Scope | Repo read | Cross-job reuse | Expires |
|------------|-------|-----------|-----------------|---------|
| Compute node enrollment | Node registration/heartbeat only | No | N/A | Enrollment expiry |
| Job identity token | One job, one repository, one `afterSha` | Yes (scoped) | No | Job teardown / timeout |

Validated by `ValidateJobIdentityQueryHandler` at the workspace materialization boundary.
