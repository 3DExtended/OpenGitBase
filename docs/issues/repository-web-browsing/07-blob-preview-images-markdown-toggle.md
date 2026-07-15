<!-- forge: #208 -->

# Blob preview — images, SVG, markdown toggle

## Metadata

- ID: repo-browse-07
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Extend blob view with rich preview modes:

- **PNG, JPEG, GIF, WebP** under 1 MB: inline image preview.
- **SVG**: download only — never render inline (XSS avoidance).
- **Markdown** (`.md`, `.markdown`): **Rendered** view by default using the safe markdown component from issue 05; **Raw** toggle shows syntax-highlighted source.

API blob response includes `previewKind` hint (`text`, `image`, `svg`, `binary`) to drive UI mode selection.

## Acceptance criteria

- [ ] PNG/JPEG/GIF/WebP files under 1 MB display inline image on blob page
- [ ] SVG blob page shows download only; no `<img>` or inline SVG render
- [ ] `.md` file defaults to rendered markdown; toggle switches to raw highlighted source
- [ ] Rendered and raw modes preserve ref and path in URL or local state (no navigation loss)
- [ ] API blob DTO includes correct `previewKind` for image, svg, text, and binary fixtures
- [ ] API controller tests for previewKind classification
- [ ] Automated tests: image fixture renders img element; SVG fixture has no inline preview; markdown toggle switches visible content

## Blocked by

- [06-blob-view-text-download-size-cap.md](./06-blob-view-text-download-size-cap.md)

## User stories covered

- 22, 23, 26, 28, 30, 31

## Notes

- Image preview may use raw endpoint URL with auth cookie for private repos (same-origin).
- Markdown toggle i18n: “Rendered” / “Raw” labels.
