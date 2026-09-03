// Renders the Terminal/Ice mockups (.dc.html artboards) to PNG at the same width
// as the live captures, so the report can place them side by side.
import { chromium } from '@playwright/test'
import { readFileSync, mkdirSync, existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join, resolve } from 'node:path'
import { SCREENS, WIDTH } from './screens.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const MOCK_SRC = resolve(HERE, '../../../../docs/wireframes/directions/terminal/ice')
const OUT = join(HERE, 'report', 'mock')
mkdirSync(OUT, { recursive: true })

const browser = await chromium.launch()
for (const s of SCREENS) {
  const file = join(MOCK_SRC, s.id + '.dc.html')
  if (!existsSync(file)) { console.log(s.id.padEnd(14), 'no mockup at', file); continue }
  const src = readFileSync(file, 'utf8')
  const head = src.slice(src.indexOf('<helmet>') + 8, src.indexOf('</helmet>'))
  const inner = src.slice(src.indexOf('</helmet>') + 9, src.indexOf('</x-dc>'))
  const ctx = await browser.newContext({ viewport: { width: WIDTH, height: 1000 }, deviceScaleFactor: 1 })
  const page = await ctx.newPage()
  await page.setContent(`<!doctype html><html><head><meta charset="utf-8">${head}</head><body style="margin:0">${inner}</body></html>`, { waitUntil: 'networkidle' })
  await page.evaluate(async () => { try { await document.fonts.ready } catch {} })
  await page.screenshot({ path: join(OUT, s.id + '.png'), fullPage: true })
  console.log(s.id.padEnd(14), 'rendered')
  await ctx.close()
}
await browser.close()
console.log('\nmockups →', OUT)
