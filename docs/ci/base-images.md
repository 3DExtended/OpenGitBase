# Base images (operator guide)

Platform **Base Images** are content-addressed rootfs tarballs stored in the **Layer Store** and registered in the **Base Image Catalog**. Each catalog entry maps a YAML `image:` slug to a `contentHash` and `ociProvenance`.

## Build workflow

Use the operator script after the API and MinIO layer store are running:

```bash
./scripts/build-base-image-rootfs.sh
```

Environment overrides:

| Variable | Default | Purpose |
|----------|---------|---------|
| `BASE_IMAGE_OCI` | `docker.io/library/alpine:3.20` | Pinned OCI source |
| `BASE_IMAGE_SLUG` | `alpine` | Catalog slug |
| `BASE_IMAGE_VERSION` | `3.20` | Catalog version label |
| `API_URL` | `http://localhost:8089` | Control plane |
| `MINIO_ENDPOINT` | `http://localhost:9000` | Layer Store |

The script is idempotent: re-running with the same OCI input produces the same `contentHash` and safely re-uploads the tarball.

## Rootfs contents

Each curated rootfs includes:

- unprivileged `ogb` user (uid 1000)
- `/usr/local/bin/ogb-guest-agent` — vsock listener for in-guest execution (protocol finalized in ci-rt-08)
- `/etc/ogb/base-image-marker` — build provenance marker

## Guest kernel pinning (v1)

Firecracker guest kernels are **pinned per compute agent host**, not per catalog entry. Operators must install one supported kernel build on each bare-metal agent (for example `vmlinux` 5.10 LTS) and configure the agent `KernelImagePath`.

Compose development keeps `PreferProcessSandbox: true` and does not require KVM. Use the optional Firecracker compose profile (ci-rt-15) to exercise real MicroVM boot on capable hosts.

## Compose seeding

For local development, `scripts/seed-base-image-catalog.sh` delegates to `build-base-image-rootfs.sh` when Docker is available.
