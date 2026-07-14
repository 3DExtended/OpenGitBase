---
name: visual-snapshots
description: Playwright visual regression for Nuxt UI changes. Required for any component, page, layout, or styling change. Use when touching opengitbase-web appearance.
---

# Visual snapshots

Required for **every UI appearance change** in `applications/opengitbase-web/`.

## When required

- New or changed Vue components, pages, layouts
- Design tokens / CSS that affect rendered appearance
- Not required for pure logic/composable changes with no visual effect

## Gallery fixture

Add or extend a section in `applications/opengitbase-web/app/pages/__visual__/index.vue`:

- `data-testid="visual-<name>"`
- Representative props/fixtures (see `visual-discussion-sub-threads` pattern)

## Playwright spec

Add or extend `applications/opengitbase-web/tests/visual/<feature>.spec.ts`:

```typescript
await waitForApp(page);
await page.goto('/__visual__/?msw=1');
await expect(page.getByTestId('visual-<name>')).toHaveScreenshot('<name>.png');
```

Page-level flows: snapshot `body` or stable `data-testid` region (see `shell.spec.ts`, `discussion-detail.spec.ts`).

## Commands

```bash
cd applications/opengitbase-web
pnpm test:visual              # verify
pnpm test:visual:update       # regenerate baselines after intentional change
```

## Done criteria

- Baselines committed under `-snapshots/` in the same commit as the UI change
- `pnpm test:visual` passes before work item is complete

## Compose vs dev server

Visual tests use Playwright `webServer` (`pnpm dev` + MSW) — compose not required for gallery snapshots. Run compose E2E separately when API integration matters.
