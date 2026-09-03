import { chromium } from '@playwright/test'
import { readFileSync, writeFileSync, readdirSync } from 'node:fs'
const DIR = new URL('./', import.meta.url).pathname
const files = readdirSync(DIR).filter(f => f.endsWith('.dc.html'))

const browser = await chromium.launch()
const ctx = await browser.newContext({ viewport: { width: 1280, height: 60 }, deviceScaleFactor: 1 })
const heights = {}
for (const f of files) {
  const src = readFileSync(DIR + f, 'utf8')
  const head = src.slice(src.indexOf('<helmet>') + 8, src.indexOf('</helmet>'))
  const inner = src.slice(src.indexOf('</helmet>') + 9, src.indexOf('</x-dc>'))
  const page = await ctx.newPage()
  await page.setContent(`<!doctype html><html><head><meta charset="utf-8">${head}</head><body>${inner}</body></html>`, { waitUntil: 'networkidle' })
  await page.evaluate(async () => { try { await document.fonts.ready } catch {} })
  const h = await page.evaluate(() => {
    const w = document.querySelector('.wrap')
    return Math.ceil(w ? w.scrollHeight : document.body.scrollHeight)
  })
  heights[f] = h
  await page.close()
}
await browser.close()

const cj = JSON.parse(readFileSync(DIR + 'canvas.json', 'utf8'))
for (const a of cj.artboards) {
  const m = heights[a.file]
  if (m) a.h = m + 36  // small slack; bg fills any surplus
}
// re-flow rows with new heights (3 cols per page, keep x, recompute y per page)
const byPage = {}
for (const a of cj.artboards) (byPage[a.page] ??= []).push(a)
for (const pid of Object.keys(byPage)) {
  const list = byPage[pid]
  let rowY = 0, i = 0
  while (i < list.length) {
    const row = list.slice(i, i + 3)
    let maxH = 0
    row.forEach(a => { a.y = rowY; maxH = Math.max(maxH, a.h) })
    rowY += maxH + 180; i += 3
  }
}
writeFileSync(DIR + 'canvas.json', JSON.stringify(cj, null, 2))
console.log(Object.entries(heights).map(([k, v]) => `${k.replace('.dc.html','').padEnd(22)} ${v}`).join('\n'))
