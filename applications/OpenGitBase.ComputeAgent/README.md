# OpenGitBase Compute Agent

This agent enrolls a compute node, sends heartbeats, long-polls for jobs, and executes scripts.

Current sandbox implementation uses `ProcessSandboxExecutor` to keep local compose development working when `/dev/kvm` and Firecracker are unavailable.

The execution abstraction is `ISandboxExecutor`, so a future `FirecrackerSandboxExecutor` can be introduced without changing the claim/heartbeat control loop.
