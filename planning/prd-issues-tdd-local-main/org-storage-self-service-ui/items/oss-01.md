## Summary

Added org-scoped `PATCH /organization/{id}/storage/nodes/{nodeId}/capacity` with `EnforceUsedBytesFloor` on the storage capacity handler, owner authorization, and web API client method.

## Linked Context

- PRD: `docs/prd/org-storage-self-service-ui.md`
- Work item: `oss-01`

## Status

- Branch: `main`
- Tests: passing
  - `dotnet test tests/OpenGitBase.Api.Tests --filter OrganizationStorageControllerTests`
  - `dotnet test tests/OpenGitBase.Features.StorageNode.Tests --filter UpdateStorageNodeCapacityQueryHandlerTests`
- Commit(s): `280b60d`
