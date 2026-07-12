# Base image build script + vsock guest agent in rootfs

## Metadata

- ID: ci-rt-05
- Type: AFK
- Status: ready
- Source: docs/prd/ci-cd-runtime-completion.md

## Parent

[PRD: CI/CD Runtime Completion](../../../docs/prd/ci-cd-runtime-completion.md)

## What to build

Deliver an operator script that builds a curated **Base Image** rootfs from a pinned OCI source: includes unprivileged `ogb` user, vsock guest agent binary and init hook, and minimal tooling needed for the first Firecracker tracer. Export as content-addressed tarball, upload to **Layer Store**, and create or update a **Base Image Catalog** entry with `ociProvenance` and `contentHash`.

Document platform-pinned guest kernel expectations per agent host (single kernel version in v1, not per catalog entry).

## Acceptance criteria

- [ ] Operator script builds rootfs from pinned OCI and uploads to **Layer Store**
- [ ] Catalog entry created with traceable `ociProvenance` and `contentHash`
- [ ] Rootfs contains `ogb` user and vsock guest agent capable of receiving execute requests
- [ ] Operator documentation covers kernel pinning and agent host requirements
- [ ] Script is idempotent and suitable for compose dev seeding

## Blocked by

- None — can start immediately

## User stories covered

- 33 — Base images built from pinned OCI sources
- 34 — Rootfs includes vsock guest agent and `ogb` user
- 35 — One platform-pinned guest kernel per compute agent

## Notes

- Replaces stub `alpine-seed` tarball approach from prior seed scripts.
- Guest agent protocol details finalized in ci-rt-08; this slice delivers the binary in rootfs.
