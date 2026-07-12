# OpenGitBase Compute Agent

This agent enrolls a compute node, sends heartbeats, long-polls for jobs, and executes scripts.

## Local development enrollment

Platform compute enrollment is automated — no manual curl required:

```bash
# Start API + postgres first, then:
./scripts/bootstrap-fleet.sh
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --build compute-agent-1
```

The bootstrap script mints a platform enrollment token via `POST /api/admin/compute-enrollments` and writes `ComputeAgent__EnrollmentToken` into `docker-compose.override.yml` for `compute-agent-1`.

Alternatively, use the admin console at `/admin/compute` to create enrollments and copy the compose override snippet.

Current sandbox implementation uses `ProcessSandboxExecutor` to keep local compose development working when `/dev/kvm` and Firecracker are unavailable.

The execution abstraction is `ISandboxExecutor`, so a future `FirecrackerSandboxExecutor` can be introduced without changing the claim/heartbeat control loop.
