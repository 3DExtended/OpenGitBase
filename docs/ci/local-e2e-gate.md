# CI/CD local verification

Run the compose stack, bootstrap fleet tokens, then:

```bash
scripts/bootstrap-fleet.sh
scripts/seed-base-image-catalog.sh   # optional but recommended
scripts/test-pipelines-e2e.sh
```

`test-pipelines-e2e.sh` is the canonical post-change smoke test for pipeline scheduling, compute claim, structured logs (layer/workspace/script sections), and a green `ogb-hosted` run.

**Failure modes**

- **KVM missing** — Firecracker falls back to process sandbox; E2E still passes but MicroVM path is not exercised.
- **Compute agent unhealthy** — check `/admin/compute` and enrollment token in `docker-compose.override.yml`.
- **Kafka down** — platform wake signals degrade to poll; jobs still complete but slower.
