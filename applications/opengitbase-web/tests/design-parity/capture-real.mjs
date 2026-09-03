// Screenshots the live app's real screens (dark Terminal/Ice theme) via MSW mocks.
// Requires a running dev server with NUXT_PUBLIC_MSW=true (see run.mjs).
import { chromium } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { SCREENS, WIDTH, installExtraRoutes } from './screens.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const BASE = process.env.OGB_BASE || 'http://localhost:3200'
const OUT = join(HERE, 'report', 'real')
mkdirSync(OUT, { recursive: true })

const ADMIN_ME = { userId: '22222222-2222-2222-2222-222222222222', username: 'demo-user', emailVerified: true, isAdmin: true }
const meFor = auth => auth === 'admin' ? ADMIN_ME : { ...ADMIN_ME, isAdmin: true }

const browser = await chromium.launch()
for (const s of SCREENS) {
  const ctx = await browser.newContext({ viewport: { width: WIDTH, height: 1000 }, deviceScaleFactor: 1 })
  await ctx.addInitScript(() => {
    document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    // block the MSW service worker so page.route fixtures win (needed for detail screens)
    void navigator.serviceWorker?.getRegistrations?.().then(rs => Promise.all(rs.map(r => r.unregister())))
  })
  const page = await ctx.newPage()
  if (s.auth === 'out') await page.route('**/api/account/me', r => r.fulfill({ status: 401, body: '' }))
  else await page.route('**/api/account/me', r => r.fulfill({ json: meFor(s.auth) }))
  await installExtraRoutes(page, s.id)

  const useMsw = s.mock !== 'mergeRequest' // MR is fully page.route-mocked; others use MSW worker
  const url = BASE + s.route + (s.route.includes('?') ? '&' : '?') + (useMsw ? 'msw=1' : 'msw=0')
  try {
    await page.goto(url, { waitUntil: 'networkidle', timeout: 30000 })
  } catch { console.log(s.id.padEnd(14), '(nav timeout, continuing)') }
  await page.evaluate(async () => { try { await document.fonts.ready } catch {} })
  await page.waitForTimeout(700)
  await page.addStyleTag({ content: '*,*::before,*::after{animation-duration:0s!important;transition-duration:0s!important}' })
  await page.screenshot({ path: join(OUT, s.id + '.png'), fullPage: true })
  console.log(s.id.padEnd(14), 'captured  ' + page.url().replace(BASE, ''))
  await ctx.close()
}
await browser.close()
console.log('\nreal screens →', OUT)
