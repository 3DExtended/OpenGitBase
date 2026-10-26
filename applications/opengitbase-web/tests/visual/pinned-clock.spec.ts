import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, PINNED_NOW, test } from './helpers'

test.describe('Pinned clock', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/sign-in')
  })

  test('is in place before a beforeEach hook navigates', async ({ page }) => {
    expect(await page.evaluate(() => new Date().toISOString())).toBe(PINNED_NOW)
  })
})

// A spec that takes `test` straight from @playwright/test skips the pinned
// clock, and its relative times only start failing months later.
test('every visual spec takes test and expect from ./helpers', () => {
  const { testDir } = test.info().project
  // [^'"] keeps a match inside one import statement: the specs use no semicolons.
  const playwrightValueImport = /^import\s+(?!type\b)[^'"]*?\bfrom\s+'@playwright\/test'/m
  const offenders = readdirSync(testDir)
    .filter(file => file.endsWith('.spec.ts'))
    .filter(file => playwrightValueImport.test(readFileSync(join(testDir, file), 'utf8')))
  expect(offenders, 'import { expect, test } from \'./helpers\' instead of @playwright/test').toEqual([])
})
