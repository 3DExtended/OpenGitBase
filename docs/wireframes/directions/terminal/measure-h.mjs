import { chromium } from '@playwright/test'
import { readFileSync, writeFileSync } from 'node:fs'
const DIR = new URL('./', import.meta.url).pathname
const cj = JSON.parse(readFileSync(DIR + 'canvas.json', 'utf8'))
const browser = await chromium.launch()
for (const a of cj.artboards) {
  const src = readFileSync(DIR + a.file, 'utf8')
  const head = src.slice(src.indexOf('<helmet>') + 8, src.indexOf('</helmet>'))
  const inner = src.slice(src.indexOf('</helmet>') + 9, src.indexOf('</x-dc>'))
  const ctx = await browser.newContext({ viewport: { width: a.w, height: 60 }, deviceScaleFactor: 1 })
  const page = await ctx.newPage()
  await page.setContent(`<!doctype html><html><head><meta charset="utf-8">${head}</head><body>${inner}</body></html>`, { waitUntil: 'networkidle' })
  await page.evaluate(async () => { try { await document.fonts.ready } catch {} })
  const h = await page.evaluate(() => Math.ceil((document.querySelector('.wrap')||document.body).scrollHeight))
  a.h = h + 24
  await ctx.close()
  console.log(a.file.padEnd(22), h)
}
await browser.close()
writeFileSync(DIR + 'canvas.json', JSON.stringify(cj, null, 2))
