// One-shot: boot the app with MSW mocks, capture the live screens, render the
// mockups, build the report, then tear the server down.
//
//   node tests/design-parity/run.mjs          -> report/ with linked PNGs
//   OGB_PARITY_INLINE=1 node .../run.mjs       -> self-contained report/index.html
//
// Reuses an already-running server if OGB_BASE is set (skips spawn/teardown).
import { spawn, execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const HERE = dirname(fileURLToPath(import.meta.url))
const WEB = join(HERE, '..', '..')
const PORT = process.env.OGB_PARITY_PORT || '3200'
const external = !!process.env.OGB_BASE
const BASE = process.env.OGB_BASE || `http://localhost:${PORT}`

let server
async function waitUp(url, tries = 90) {
  for (let i = 0; i < tries; i++) {
    try { const r = await fetch(url); if (r.ok) return true } catch {}
    await new Promise(r => setTimeout(r, 1000))
  }
  throw new Error('dev server did not come up: ' + url)
}
function run(script) {
  execFileSync(process.execPath, [join(HERE, script)], {
    stdio: 'inherit', cwd: WEB, env: { ...process.env, OGB_BASE: BASE },
  })
}

try {
  if (!external) {
    console.log(`\n▶ starting dev server (MSW) on :${PORT} …`)
    server = spawn('pnpm', ['dev', '--port', PORT], {
      cwd: WEB, stdio: 'ignore',
      // MSW off at the server: MSW-backed screens opt in per-navigation with ?msw=1,
      // while page.route-only screens (merge request) keep the worker out of the way.
      env: { ...process.env, NUXT_IGNORE_LOCK: '1', NUXT_PUBLIC_MSW: 'false', NUXT_PUBLIC_SITE_GATE_ENABLED: 'false', NUXT_PUBLIC_DEPLOY_SHA: 'visual-test' },
    })
    await waitUp(BASE + '/')
    console.log('  up.')
  } else {
    console.log('▶ using existing server at', BASE)
  }
  console.log('\n▶ capturing live screens …'); run('capture-real.mjs')
  console.log('\n▶ rendering mockups …');       run('render-mockups.mjs')
  console.log('\n▶ building report …');          run('build-report.mjs')
  console.log('\n✔ done → tests/design-parity/report/index.html')
} finally {
  if (server) server.kill()
}
