# Progress log — org storage self-service UI

Branch: **main**

| Item | Status | Commit |
|------|--------|--------|
| oss-01 | complete | `280b60d` |
| oss-02 | complete | `6c02f2f` |
| oss-03 | complete | `a17cf90` |
| oss-04 | complete | `021558d` |
| oss-05 | complete | `62249a3` |

## Verification

- `dotnet test tests/OpenGitBase.Api.Tests --filter OrganizationStorageControllerTests`
- `dotnet test tests/OpenGitBase.Features.StorageNode.Tests --filter UpdateStorageNodeCapacityQueryHandlerTests`
- `pnpm test` (web unit, including orgStorageBootstrap + storageDocsPages)
- `pnpm test:visual -- tests/visual/org-storage.spec.ts`
- `pnpm test:visual:update -- tests/visual/shell.spec.ts -g "visual gallery"` (gallery baseline refresh)
- Bootstrap script dry-run: `scripts/bootstrap-org-storage-node.sh --dry-run ...`

## Notes

- Compose E2E not run (stack not required for UI/docs-only tail items).
- Full bootstrap against live instance: manual operator verification documented in oss-02 item.
