import { writeFileSync } from 'node:fs'
const OUT = new URL('./', import.meta.url).pathname

/* ---------- palette (lifted from applications/opengitbase-web) ---------- */
const c = {
  bg:'#fafafa', sf:'#ffffff', bd:'#e4e4e7', bd2:'#d4d4d8',
  tx:'#18181b', mu:'#71717a', fa:'#a1a1aa',
  ac:'#0d9488', acH:'#0f766e', softBg:'#ccfbf1', softTx:'#0f766e',
  okBg:'#dcfce7', okTx:'#15803d', nBg:'#f4f4f5', nTx:'#52525b',
  wBg:'#fef9c3', wTx:'#a16207', opBg:'#e0e7ff', opTx:'#4338ca',
  eBg:'#fee2e2', eTx:'#dc2626',
  bwBg:'#fffbeb', bwBd:'#fde68a', bwTx:'#92400e',
  beBg:'#fef2f2', beBd:'#fecaca', beTx:'#dc2626',
  biBg:'#eff6ff', biBd:'#bfdbfe', biTx:'#1d4ed8',
}

/* ---------- icons ---------- */
const S = (p, s=18, sw=2) => `<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" style="flex:none">${p}</svg>`
const IC = {
  logo:(s=22)=>`<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="none" stroke="${c.ac}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="flex:none"><circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="9" r="2.4"/><path d="M6 8.4v7.2"/><path d="M18 11.4c0 3-2.5 3.6-5.5 4.2C10 16 8.4 16.6 8.4 18"/></svg>`,
  panel:(s=18)=>S(`<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 4v16"/>`,s),
  sun:(s=18)=>S(`<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4 12H2M22 12h-2M5 5l1.4 1.4M17.6 17.6L19 19M19 5l-1.4 1.4M6.4 17.6L5 19"/>`,s),
  bell:(s=18)=>S(`<path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>`,s),
  chev:(s=16)=>S(`<path d="M6 9l6 6 6-6"/>`,s),
  code:(s=18)=>S(`<path d="M16 18l4-6-4-6M8 6l-4 6 4 6"/>`,s),
  chat:(s=18)=>S(`<path d="M21 15a2 2 0 0 1-2 2H8l-4 3V6a2 2 0 0 1 2-2h13a2 2 0 0 1 2 2z"/>`,s),
  merge:(s=18)=>S(`<circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="15" r="2.4"/><path d="M6 8.4v7.2M6 9a6 6 0 0 0 6 6h3.6"/>`,s),
  pipe:(s=18)=>S(`<rect x="3" y="4" width="7" height="7" rx="1.5"/><rect x="14" y="13" width="7" height="7" rx="1.5"/><path d="M6.5 11v3a3 3 0 0 0 3 3H14"/>`,s),
  gear:(s=18)=>S(`<circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.3 1a7 7 0 0 0-1.7-1l-.3-2.5H10.4l-.3 2.5a7 7 0 0 0-1.7 1l-2.3-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.3-1a7 7 0 0 0 1.7 1l.3 2.5h3.2l.3-2.5a7 7 0 0 0 1.7-1l2.3 1 2-3.4-2-1.5c.1-.3.1-.7.1-1z"/>`,s),
  users:(s=18)=>S(`<path d="M16 20v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="8" r="3.2"/><path d="M22 20v-2a4 4 0 0 0-3-3.8"/><path d="M16 4.2A4 4 0 0 1 16 12"/>`,s),
  shield:(s=18)=>S(`<path d="M12 2l8 3v6c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V5z"/>`,s),
  server:(s=18)=>S(`<rect x="3" y="4" width="18" height="7" rx="1.5"/><rect x="3" y="13" width="18" height="7" rx="1.5"/><path d="M7 7.5h.01M7 16.5h.01"/>`,s),
  cpu:(s=18)=>S(`<rect x="6" y="6" width="12" height="12" rx="2"/><path d="M9 1v3M15 1v3M9 20v3M15 20v3M1 9h3M1 15h3M20 9h3M20 15h3"/><rect x="10" y="10" width="4" height="4"/>`,s),
  layers:(s=18)=>S(`<path d="M12 2l9 5-9 5-9-5z"/><path d="M3 12l9 5 9-5M3 17l9 5 9-5"/>`,s),
  activity:(s=18)=>S(`<path d="M3 12h4l3 8 4-16 3 8h4"/>`,s),
  dbrepl:(s=18)=>S(`<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v6c0 1.7 3.6 3 8 3s8-1.3 8-3V5"/><path d="M4 11v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6"/>`,s),
  search:(s=18)=>S(`<circle cx="11" cy="11" r="7"/><path d="M21 21l-4-4"/>`,s),
  plus:(s=18)=>S(`<path d="M12 5v14M5 12h14"/>`,s),
  aleft:(s=18)=>S(`<path d="M19 12H5M12 19l-7-7 7-7"/>`,s),
  aright:(s=16)=>S(`<path d="M5 12h14M12 5l7 7-7 7"/>`,s),
  ext:(s=15)=>S(`<path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><path d="M15 3h6v6M10 14L21 3"/>`,s),
  monitor:(s=18)=>S(`<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/>`,s),
  folder:(s=16)=>S(`<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>`,s),
  file:(s=16)=>S(`<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/>`,s),
  key:(s=18)=>S(`<circle cx="7.5" cy="15.5" r="4"/><path d="M10.5 12.5L20 3M16 7l3 3M14 9l2 2"/>`,s),
  user:(s=18)=>S(`<circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 3.6-6 8-6s8 2 8 6"/>`,s),
  grid:(s=18)=>S(`<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>`,s),
  copy:(s=16)=>S(`<rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>`,s),
  refresh:(s=16)=>S(`<path d="M21 12a9 9 0 1 1-3-6.7L21 8"/><path d="M21 3v5h-5"/>`,s),
}

/* ---------- small helpers ---------- */
const bd = (t, kind='n') => {
  const m = { ok:[c.okBg,c.okTx], n:[c.nBg,c.nTx], w:[c.wBg,c.wTx], open:[c.opBg,c.opTx], e:[c.eBg,c.eTx] }[kind]
  return `<span class="bd" style="background:${m[0]};color:${m[1]}">${t}</span>`
}
const btn = (t, v='pri', ic='') => `<span class="btn ${v}">${ic}${t}</span>`
const banner = (t, kind='w') => {
  const m = { w:[c.bwBg,c.bwBd,c.bwTx], e:[c.beBg,c.beBd,c.beTx], i:[c.biBg,c.biBd,c.biTx] }[kind]
  return `<div class="banner" style="background:${m[0]};border:1px solid ${m[1]};color:${m[2]}">${t}</div>`
}
const card = (title, body, right='') =>
  `<div class="card">${title!==null?`<div class="ch"><span>${title}</span>${right}</div>`:''}<div class="cb">${body}</div></div>`
const field = (label, ph='', req=false, w='340px') =>
  `<div class="field"><label class="lb">${label}${req?' <span class="req">*</span>':''}</label><div class="inp" style="max-width:${w}">${ph}</div></div>`
const sel = (v, w='140px') => `<span class="sel" style="min-width:${w}">${v} ${IC.chev(15)}</span>`

/* ---------- chrome ---------- */
function topbar(loggedIn, activeNav='', withToggle=false){
  const nav = ['Home','Explore','Docs','Community','System status']
    .map(n=>`<span class="${n===activeNav?'on':''}">${n}</span>`).join('')
  const right = loggedIn
    ? `<span>${IC.sun()}</span><span>${IC.bell()}</span><span class="usermenu">demo-user ${IC.chev(15)}</span>`
    : `<span>${IC.sun()}</span><span>Sign in</span>${btn('Sign up','pri sm')}`
  return `<div class="topbar">${(loggedIn||withToggle)?`<span style="color:${c.mu}">${IC.panel()}</span>`:''}<div class="brand">${IC.logo(22)} OpenGitBase</div><nav class="nav">${nav}</nav><div class="spacer"></div><div class="tr">${right}</div></div>`
}
const foot = () => `<div class="foot"><span>OpenGitBase — self-hosted Git forge</span><span><a>Documentation</a><a>System status</a></span></div>`
const ni = (icon, label, active=false, cls='') => `<div class="ni ${active?'on':''} ${cls}">${icon} <span>${label}</span></div>`
const ver = () => `<div class="grow"></div><div class="ver">v1.0.0<br>wireframe-mock</div>`

function repoSide(active, opts={}){
  const items = [
    ni(IC.code(), 'Code', active==='code'),
    ni(IC.chat(), 'Discussions', active==='disc'),
    ni(IC.merge(), 'Merge requests', active==='mr'),
    ni(IC.pipe(), 'Pipelines', active==='pipe'),
  ]
  if (opts.owner){
    items.push(ni(IC.gear(), 'Repository settings', active==='settings'))
    items.push(ni(IC.users(), 'Members', active==='members'))
  }
  return `<aside class="side"><div class="sub mono">demo-user/hello-world</div>${items.join('')}${ver()}</aside>`
}
function dashSide(){
  return `<aside class="side"><div class="ni out" style="border:1px solid ${c.bd2};justify-content:space-between">${IC.plus(16)} <span style="flex:1;margin-left:4px">Create</span> ${IC.chev(15)}</div><div class="cap">Organizations</div>${ni(IC.grid(16),'Acme Corp')}<div class="cap">Repositories</div>${ni(IC.logo(16),'Private Notes')}${ni(IC.logo(16),'Hello World')}${ver()}</aside>`
}
function orgSide(active){
  return `<aside class="side"><div style="padding:2px 8px 4px"><div style="font-weight:700">Acme Corp</div><div class="sub" style="padding:0">Organization</div></div>${ni(IC.grid(16),'Overview',active==='ov')}<div class="cap">Organization settings</div>${ni(IC.users(16),'Members',active==='members')}${ni(IC.server(16),'Storage',active==='storage')}${ni(IC.cpu(16),'Compute',active==='compute')}<div class="cap">Repositories</div>${ni(IC.logo(16),'Hello World')}${ver()}</aside>`
}
function adminSide(active){
  return `<aside class="side"><div class="cap" style="margin-top:2px">Administration</div>${ni(IC.shield(16),'Administration',active==='home')}${ni(IC.server(16),'Storage fleet',active==='storage')}${ni(IC.cpu(16),'Compute fleet',active==='compute')}${ni(IC.layers(16),'CI supply chain',active==='ci')}${ni(IC.shield(16),'Domain allowance',active==='egress')}${ni(IC.activity(16),'Status & incidents',active==='status')}${ni(IC.dbrepl(16),'Repository replication',active==='repl')}${ver()}</aside>`
}
function acctSide(active){
  return `<aside class="side"><div class="cap" style="margin-top:2px">Account settings</div>${ni(IC.user(16),'Profile',active==='profile')}${ni(IC.key(16),'Access tokens',active==='tokens')}${ver()}</aside>`
}
function docsSide(active){
  const it = [['Overview','ov'],['Quick start'],['How it works'],['Pipeline YAML'],['Hosting profiles'],['Compute nodes'],['Base images'],['Dependencies & layers'],['Variables'],['Network egress'],['Editor setup']]
  return `<aside class="side" style="width:300px"><div class="sub mono">docs/ci</div>${ni(IC.code(),'Code')}${ni(IC.chat(),'Discussions')}${ni(IC.merge(),'Merge requests')}${ni(IC.pipe(),'Pipelines')}<div style="margin-top:16px"><a style="display:flex;gap:6px;align-items:center;font-size:13px">${IC.aleft(15)} All documentation</a><div class="cap" style="margin-left:0">CI/CD</div>${it.map(([t,k])=>`<div class="ni ${active===k?'on':''}" style="padding:6px 10px;font-size:14px;${active===k?`color:${c.ac}`:`color:${c.mu}`}">${t}</div>`).join('')}</div>${ver()}</aside>`
}

/* layouts */
const appShell = (side, main, loggedIn=false, activeNav='') =>
  `<div class="wrap">${topbar(loggedIn, activeNav, true)}<div class="body">${side}<div class="main">${main}</div></div>${foot()}</div>`
const plainShell = (main, loggedIn=false, activeNav='') =>
  `<div class="wrap">${topbar(loggedIn, activeNav)}<div class="main" style="padding:32px 128px;min-height:520px">${main}</div>${foot()}</div>`
const headerBlock = (crumb, title, sub='', right='') =>
  `<div class="rowb" style="align-items:flex-start;margin-bottom:24px"><div>${crumb?`<div class="crumb mono" style="margin-bottom:4px">${crumb}</div>`:''}<h1 class="h1">${title}</h1>${sub?`<p class="sub">${sub}</p>`:''}</div>${right?`<div style="display:flex;gap:12px">${right}</div>`:''}</div>`
const backLink = (t) => `<a style="display:inline-flex;gap:6px;align-items:center;font-weight:500;margin-bottom:14px">${IC.aleft(16)} ${t}</a>`

/* ---------- .dc.html wrapper ---------- */
const CSS = `
*{box-sizing:border-box}
html{height:100%}
body{margin:0;min-height:100%;font-family:'Inter',ui-sans-serif,system-ui,sans-serif;background:${c.bg};color:${c.tx};font-size:14px;line-height:1.45;-webkit-font-smoothing:antialiased}
.mono{font-family:'JetBrains Mono',ui-monospace,monospace}
a{color:${c.ac};text-decoration:none}a:hover{color:${c.acH}}
.wrap{width:1280px;min-height:100%;background:${c.bg};overflow:hidden}
.topbar{height:56px;background:#fff;border-bottom:1px solid ${c.bd};display:flex;align-items:center;gap:20px;padding:0 24px}
.brand{display:flex;align-items:center;gap:8px;font-weight:700;font-size:16px}
.nav{display:flex;gap:22px;color:${c.mu};font-size:14px}.nav .on{color:${c.tx};font-weight:500}
.spacer{flex:1}.tr{display:flex;align-items:center;gap:16px;color:${c.mu}}
.usermenu{display:flex;align-items:center;gap:6px;color:${c.tx}}
.body{display:flex;align-items:stretch}
.side{width:256px;background:#fff;border-right:1px solid ${c.bd};padding:14px 12px;display:flex;flex-direction:column;gap:1px;flex:none}
.side .cap{font-size:11px;letter-spacing:.05em;text-transform:uppercase;color:${c.fa};font-weight:600;margin:16px 8px 6px}
.side .sub{font-size:12px;color:${c.mu};padding:2px 8px;margin-bottom:6px}
.ni{display:flex;align-items:center;gap:10px;padding:8px 10px;border-radius:8px;color:${c.tx};font-size:14px}
.ni.on{background:${c.nBg};font-weight:500}
.grow{flex:1}.ver{font-size:11px;color:${c.fa};padding:8px;line-height:1.35}
.main{flex:1;padding:32px 40px;min-width:0}
.foot{height:57px;border-top:1px solid ${c.bd};display:flex;align-items:center;justify-content:space-between;padding:0 24px;color:${c.mu};font-size:13px;background:${c.bg}}
.foot a{margin-left:20px}
.h1{font-size:26px;font-weight:700;margin:0}.h2{font-size:18px;font-weight:600;margin:0 0 2px}
.sub{color:${c.mu};font-size:14px;margin:6px 0 0}.crumb{color:${c.mu};font-size:13px}.muted{color:${c.mu}}
.card{background:#fff;border:1px solid ${c.bd};border-radius:12px;margin-bottom:20px}
.card .ch{padding:15px 20px;border-bottom:1px solid ${c.bd};font-weight:600;font-size:15px;display:flex;align-items:center;justify-content:space-between}
.card .cb{padding:20px}
.bd{display:inline-flex;align-items:center;padding:2px 9px;border-radius:6px;font-size:12px;font-weight:600;line-height:1.5}
.btn{display:inline-flex;align-items:center;gap:6px;height:38px;padding:0 15px;border-radius:8px;font-size:14px;font-weight:500;border:1px solid transparent;white-space:nowrap}
.btn.pri{background:${c.ac};color:#fff}.btn.soft{background:${c.softBg};color:${c.softTx}}
.btn.out{background:#fff;border-color:${c.bd2};color:${c.tx}}.btn.dng{background:${c.eBg};color:${c.eTx}}
.btn.sm{height:30px;padding:0 11px;font-size:13px}.btn.wide{width:100%;justify-content:center}
.field{margin-bottom:16px}.lb{display:block;font-size:14px;margin-bottom:6px}.req{color:#dc2626}
.inp{width:100%;height:38px;border:1px solid ${c.bd2};border-radius:8px;background:#fff;padding:0 12px;color:${c.fa};font-size:14px;display:flex;align-items:center}
.inp.ta{height:auto;min-height:88px;padding:10px 12px;align-items:flex-start}
.sel{display:inline-flex;align-items:center;justify-content:space-between;gap:10px;height:38px;border:1px solid ${c.bd2};border-radius:8px;background:#fff;padding:0 12px;font-size:14px}
.banner{border-radius:12px;padding:13px 18px;font-size:14px;margin-bottom:20px}
.hero{border:1px solid ${c.bd};border-radius:16px;padding:40px 44px;background:linear-gradient(135deg,#effefb 0%,#fafafa 60%);margin-bottom:30px}
.repocard{border:1px solid ${c.bd};border-radius:12px;padding:18px 20px;background:#fff}
.grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}
.grid3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px}
.rowb{display:flex;align-items:center;justify-content:space-between}
.tabs{display:flex;background:${c.nBg};border-radius:10px;padding:4px;gap:4px;max-width:660px}
.tab{flex:1;text-align:center;padding:9px;border-radius:8px;font-size:14px;color:${c.mu}}
.tab.on{background:${c.ac};color:#fff;font-weight:500}
.chk{width:16px;height:16px;border:1.5px solid ${c.bd2};border-radius:4px;display:inline-block;vertical-align:-3px;margin-right:8px}
.bar{height:8px;border-radius:5px;background:${c.bd};overflow:hidden}.bar>i{display:block;height:100%;background:${c.ac}}
.auth{max-width:420px;margin:0 auto}
.kv{display:flex;justify-content:space-between;padding:11px 0;border-bottom:1px solid ${c.bd}}
.divider{height:1px;background:${c.bd};margin:8px 0}
.slabel{font-size:12px;font-weight:600;color:${c.mu};text-transform:uppercase;letter-spacing:.04em;margin:2px 0 10px}
.diff{border:1px solid ${c.bd};border-radius:10px;overflow:hidden;font-family:'JetBrains Mono',monospace;font-size:13px}
.diff .fh{background:#fafafa;padding:8px 14px;color:${c.mu}}
.diff .ln{display:flex}.diff .g{width:44px;text-align:right;padding:2px 10px;color:${c.fa};background:#fbfbfb;border-right:1px solid ${c.bd}}
.diff .t{padding:2px 12px;flex:1}
`
function dcFile(inner){
  return `<!doctype html>
<html>
<head>
<meta charset="utf-8">
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap">
<style>${CSS}</style>
</helmet>
${inner}
</x-dc>
<script data-dc-script data-props='{"$preview":{"width":1280,"height":900}}'>
class Component extends DCLogic { renderVals(){ return {} } }
</script>
</body>
</html>`
}

/* repo header block reused */
const repoTitle = (title, badge='Public') => `<div class="rowb" style="align-items:flex-start;margin-bottom:24px"><div><div class="crumb mono" style="margin-bottom:6px">demo-user/hello-world</div><h1 class="h1">${title}</h1></div>${bd(badge, badge==='Private'?'n':'ok')}</div>`

/* =====================================================================
   SCREENS
   ===================================================================== */
const screens = []
const add = (name, page, h, html) => screens.push({ name, page, h, html })

/* ---------- FOUNDATION: index + legend ---------- */
add('Main','p_found',1180, dcFile(`<div class="wrap" style="padding:36px 48px">
<div style="display:flex;align-items:center;gap:12px;margin-bottom:4px">${IC.logo(30)}<h1 class="h1" style="font-size:30px">OpenGitBase — Web App Wireframes</h1></div>
<p class="sub" style="max-width:760px">Mid-fidelity wireframes of every screen in the OpenGitBase web app (Nuxt 4 · Nuxt UI · teal/zinc). Grouped into pages: Public &amp; discovery, Auth, Repository, Org &amp; account, Admin console, Docs. Logged-out and logged-in states shown. Use the pages menu to switch groups.</p>
<div class="grid2" style="margin-top:26px;align-items:start">
<div>
<div class="slabel">Global header — logged out</div>${topbar(false,'Home')}
<div class="slabel" style="margin-top:22px">Global header — logged in</div>${topbar(true,'Home',true)}
<div class="slabel" style="margin-top:22px">Footer</div>${foot()}
<div class="slabel" style="margin-top:22px">Buttons</div>
<div style="display:flex;gap:10px;flex-wrap:wrap">${btn('Primary','pri')}${btn('Soft','soft')}${btn('Outline','out')}${btn('Danger','dng')}${btn('Small','pri sm')}</div>
<div class="slabel" style="margin-top:22px">Status badges</div>
<div style="display:flex;gap:8px;flex-wrap:wrap">${bd('Public','ok')}${bd('Passed','ok')}${bd('Healthy','ok')}${bd('Verified','ok')}${bd('Private','n')}${bd('Draft','w')}${bd('Degraded','w')}${bd('Open','open')}${bd('Forbidden','e')}</div>
<div class="slabel" style="margin-top:22px">Alert banners</div>
${banner('Warning / attention state','w')}${banner('Forbidden / error state','e')}${banner('Informational note','i')}
</div>
<div>
<div class="slabel">Sitemap — implemented screens</div>
<div class="card"><div class="cb" style="font-size:13.5px;line-height:1.9">
<b>Public &amp; discovery</b> — Home (guest + dashboard) · Explore · Owner profile · System status · Community pitch (slides)<br>
<b>Auth</b> — Sign in · Sign up · Forgot / Reset password · Verify email · CLI sign-in · Invite accept<br>
<b>Repository</b> — Overview · File tree · File view · Commit diff · Discussions + detail · Merge requests + new + detail · Pipelines + run · Members · Settings (+ branch rules)<br>
<b>Org &amp; account</b> — Org overview · Members · Storage · Compute · Account settings · Access tokens · SSH keys · New repo · New org<br>
<b>Admin console</b> — Home · Storage fleet · Compute fleet · CI supply chain · Domain allowance · Status &amp; incidents · Repository replication<br>
<b>Docs</b> — Index · Article
</div></div>
<div class="slabel" style="margin-top:14px">Color tokens</div>
<div style="display:flex;gap:10px;flex-wrap:wrap">
${['#0d9488 accent','#14b8a6 accent-500','#fafafa bg','#e4e4e7 border','#18181b text','#71717a muted'].map(x=>{const[hex,n]=x.split(' ');return `<div style="text-align:center"><div style="width:60px;height:44px;border-radius:8px;border:1px solid ${c.bd};background:${hex}"></div><div style="font-size:11px;color:${c.mu};margin-top:4px">${n}</div></div>`}).join('')}
</div>
</div>
</div>
</div>`))

/* ---------- PUBLIC ---------- */
add('HomeLoggedOut','p_pub',720, dcFile(plainShell(`
${`<div class="hero"><h1 style="font-size:44px;font-weight:700;margin:0 0 12px">Welcome to OpenGitBase</h1><p style="font-size:18px;color:${c.mu};margin:0 0 22px">A community-first Git forge for self-hosting and collaboration.</p><div style="display:flex;gap:12px">${btn('Get started','pri')}${btn('Explore','soft')}${btn('Community pitch','out',IC.monitor(17)+' ')}</div></div>`}
<div class="rowb" style="margin-bottom:14px"><h2 class="h2" style="font-size:20px">Recently updated public repositories</h2><a>View all</a></div>
<p class="muted">No public repositories yet.</p>`, false, 'Home')))

add('Dashboard','p_pub',760, dcFile(appShell(dashSide(), `
${headerBlock('','Welcome, demo-user','Your repositories and organizations at a glance.', btn('New repository','pri',IC.plus(16)+' ')+btn('New organization','soft',IC.grid(16)+' '))}
<h2 class="h2" style="margin:6px 0 12px">Organizations</h2>
<div style="margin-bottom:26px">${bd('Acme Corp','open')}</div>
<h2 class="h2" style="margin:0 0 12px">Repositories</h2>
<div class="grid2">
<div class="repocard"><div class="rowb"><div style="font-weight:600;font-size:16px">Hello World</div>${bd('Public','ok')}</div><div class="mono muted" style="margin:6px 0 12px;font-size:13px">demo-user/hello-world</div><div class="muted" style="font-size:13px">Updated 3 months ago</div></div>
<div class="repocard"><div class="rowb"><div style="font-weight:600;font-size:16px">Private Notes</div>${bd('Private','n')}</div><div class="mono muted" style="margin:6px 0 12px;font-size:13px">demo-user/private-notes</div><div class="muted" style="font-size:13px">Updated 3 months ago</div></div>
</div>`, true)))

add('Explore','p_pub',600, dcFile(plainShell(`
${headerBlock('','Explore','Discover public repositories on this instance.')}
<div class="inp" style="max-width:420px;color:${c.fa};margin-bottom:22px">${IC.search(16)} <span style="margin-left:8px">Search repositories…</span></div>
<div class="grid2" style="max-width:860px">
<div class="repocard"><div class="rowb"><div style="font-weight:600;font-size:16px">Hello World</div>${bd('Public','ok')}</div><div class="mono muted" style="margin:6px 0 12px;font-size:13px">demo-user/hello-world</div><div class="muted" style="font-size:13px">Updated 3 months ago</div></div>
<div class="repocard" style="opacity:.55;border-style:dashed"><div style="font-weight:600;font-size:16px">More public repos…</div><div class="muted" style="margin-top:8px;font-size:13px">Results list as cards</div></div>
</div>`, false, 'Explore')))

add('OwnerProfile','p_pub',560, dcFile(plainShell(`
<div style="display:flex;align-items:center;gap:12px;margin-bottom:22px"><h1 class="h1">demo-user</h1>${bd('User','n')}</div>
<h2 class="h2" style="margin:0 0 14px">Repositories</h2>
<div class="repocard" style="max-width:640px"><div class="rowb"><div style="font-weight:600;font-size:16px">Hello World</div>${bd('Public','ok')}</div><div class="mono muted" style="margin:6px 0 12px;font-size:13px">demo-user/hello-world</div><div class="muted" style="font-size:13px">Updated 3 months ago</div></div>
<p class="muted" style="margin-top:20px;font-size:13px">Organization profiles add a bio line and “Organization” badge.</p>`, false)))

add('SystemStatus','p_pub',2160, dcFile(plainShell(`
${headerBlock('','System status','Live health for website, API, Git, storage, data stores, and message bus.')}
${card(null, `<div class="rowb"><div><div class="muted" style="font-size:13px;margin-bottom:6px">Overall status</div>${bd('Healthy','ok')}</div><div class="muted" style="font-size:13px">Last updated 7/10/2026, 2:00:00 PM</div></div>`)}
${['Website|1 instances|ok|Healthy','API|1 instances|ok|Healthy','Git|1 instances|w|Degraded','Storage|1 instances|ok|Healthy','Data stores|2 instances|ok|Healthy'].map(r=>{const[n,i,k,l]=r.split('|');return card(null,`<div class="rowb"><div style="display:flex;gap:12px;align-items:center">${IC.chev(16)}<div><div style="font-weight:600">${n}</div><div class="muted" style="font-size:12px">${i}</div></div></div>${bd(l,k)}</div>${n==='Git'?`<div style="margin-top:16px;border-top:1px solid ${c.bd};padding-top:14px;display:grid;grid-template-columns:1.3fr 1fr 1.6fr 1fr 1fr 1.6fr;font-size:13px;color:${c.mu};gap:8px"><span>Instance</span><span>Status</span><span>Last checked</span><span>Response</span><span>Last seen</span><span>Message</span></div><div style="margin-top:8px;display:grid;grid-template-columns:1.3fr 1fr 1.6fr 1fr 1fr 1.6fr;font-size:13px;gap:8px;align-items:center"><span class="mono">dispatcher-1</span>${bd('Degraded','w')}<span>7/10 2:00 PM</span><span>2100ms</span><span>—</span><span>Slow response</span></div>`:''}`)}).join('')}
${card('Outage history', `<div class="rowb" style="margin:-4px 0 14px"><span class="muted" style="font-size:13px">Auto-detected outage windows from the last 7 days.</span><div style="display:flex;gap:6px">${['7d','30d','90d'].map((x,i)=>`<span class="bd" style="background:${i===0?c.tx:c.nBg};color:${i===0?'#fff':c.nTx}">${x}</span>`).join('')}<span style="width:8px"></span>${['UTC','Local'].map((x,i)=>`<span class="bd" style="background:${i===0?c.tx:c.nBg};color:${i===0?'#fff':c.nTx}">${x}</span>`).join('')}</div></div>
<div class="repocard" style="margin-bottom:10px"><div style="display:flex;gap:10px;align-items:center"><b>Message bus down since Jul 19, 2026, 10:00 UTC</b>${bd('Open','e')}</div><div class="muted" style="font-size:13px;margin-top:4px">Duration: 65 min</div></div>
<div class="repocard"><b>Git down Jul 18, 08:00 – 08:40 UTC</b><div class="muted" style="font-size:13px;margin-top:4px">Duration: 40 min · Note: Scheduled failover drill.</div></div>`,'')}
${card('90-day uptime', `<div class="rowb" style="margin:-4px 0 12px"><span class="muted" style="font-size:13px">Daily uptime percentage. Toggle groups to overlay component lines.</span><b style="font-size:13px">Latest daily uptime: 97.2%</b></div><div style="display:flex;gap:8px;margin-bottom:12px">${['Website','API','Git','Storage','Data stores'].map(x=>`<span class="bd" style="background:#fff;border:1px solid ${c.bd};color:${c.mu}">${x}</span>`).join('')}</div><div style="height:150px;border-left:1px solid ${c.bd};border-bottom:1px solid ${c.bd};position:relative"><div style="position:absolute;left:0;right:0;top:8px;height:3px;background:${c.ac};border-radius:3px"></div></div>`)}
`, false, 'System status')))

add('Pitch','p_pub',720, dcFile(`<div class="wrap" style="height:720px;background:${c.bg};position:relative">
<div style="height:50px;border-bottom:1px solid ${c.bd};display:flex;align-items:center;justify-content:space-between;padding:0 20px;background:#fff"><div class="brand" style="font-size:15px">${IC.logo(20)} OpenGitBase</div><span class="muted" style="font-size:13px">Arrow keys to navigate · Esc for slide overview</span><span class="muted" style="font-size:13px">✕ Exit</span></div>
<div style="padding:0 64px;display:flex;flex-direction:column;justify-content:center;height:640px">
<h1 style="font-size:60px;font-weight:700;margin:0 0 20px;max-width:820px">Git that's yours to design</h1>
<p style="font-size:26px;color:${c.mu};max-width:640px;margin:0">Source-open. Privacy-first. Transparent by default — your code, your infra, your rules.</p>
</div>
<div style="position:absolute;bottom:20px;right:24px;color:${c.mu}">${IC.aright(20)}</div>
</div>`))

/* ---------- AUTH ---------- */
const authCard = (title, sub, body, footer='') =>
  `<div class="card" style="width:420px;margin:0"><div class="cb"><div style="font-weight:600;font-size:17px">${title}</div><p class="sub" style="margin:4px 0 0">${sub}</p></div><div style="border-top:1px solid ${c.bd};padding:20px">${body}</div>${footer?`<div style="border-top:1px solid ${c.bd};padding:16px 20px">${footer}</div>`:''}</div>`
add('AuthScreens','p_auth',1120, dcFile(`<div class="wrap" style="padding:36px 48px">
<div class="slabel">Auth layout — centered card, no app chrome (sign-in / sign-up / password / verify / CLI)</div>
<div style="display:flex;gap:28px;flex-wrap:wrap">
${authCard('Sign in','Welcome back. Enter your credentials to continue.', field('Username','',true,'100%')+field('Password','',true,'100%')+btn('Sign in','pri wide'), `<a>Forgot your password?</a><div style="margin-top:10px;font-size:13px">Don't have an account? <a>Sign up</a></div>`)}
${authCard('Create your account','Register to start hosting repositories.', field('Username','',true,'100%')+field('Email','',true,'100%')+field('Password','',true,'100%')+btn('Sign up','pri wide'), `Already have an account? <a>Sign in</a>`)}
${authCard('Forgot password','Enter your email and we will send a reset link.', field('Email','',true,'100%')+btn('Send reset link','pri wide'))}
${authCard('Reset password','Choose a new password for your account.', field('New password','',true,'100%')+field('Confirm password','',true,'100%')+btn('Update password','pri wide'))}
${authCard('Verify your email','Confirm your address to unlock all features.', banner('Verifying your email address…','i')+btn('Back to sign in','out'))}
${authCard('Sign in for CLI','Authenticating against http://localhost:3200', banner('Use an existing account — the ogb CLI supplies port &amp; state parameters.','i')+field('Username','',true,'100%')+field('Password','',true,'100%')+btn('Authorize CLI','pri wide'))}
</div></div>`))

add('InviteAccept','p_auth',600, dcFile(plainShell(`
<h1 class="h1" style="text-align:center;margin-bottom:24px">Organization invitation</h1>
<div class="card" style="max-width:460px;margin:0 auto"><div class="cb">
<div class="muted" style="font-size:12px">Organization</div><div style="font-weight:600;font-size:18px;margin-bottom:14px">Acme Corp</div>
<div class="muted" style="font-size:12px">Invited role</div><div style="font-weight:600;font-size:18px;margin-bottom:16px">Member</div>
<p class="muted" style="margin:0 0 16px">Sign in or create an account to respond to this invitation.</p>
<div style="display:flex;gap:16px;align-items:center">${btn('Sign in','soft')}<a>Create account</a><a style="color:${c.eTx}">Decline</a></div>
</div></div>`, false)))

/* ---------- REPOSITORY ---------- */
add('RepoOverview','p_repo',640, dcFile(appShell(repoSide('code'), `
${repoTitle('Hello World')}
${card(null, `<div style="display:flex;align-items:center;gap:14px"><div class="tabs" style="max-width:220px;flex:none"><span class="tab on">Branches</span><span class="tab">Tags</span></div><span class="muted">Ref</span>${sel('main','110px')}<span class="mono" style="color:${c.ac}">abc123</span></div>`)}
${card(`<span>Clone repository</span><span style="color:${c.mu}">${IC.chev(16)}</span>`, `<div class="tabs" style="max-width:260px;margin-bottom:12px"><span class="tab on">HTTPS</span><span class="tab">CLI</span></div><div class="inp mono" style="color:${c.tx}">http://localhost:8089/demo-user/hello-world.git ${IC.copy(15)}</div>`)}
`, true)))

add('RepoFiles','p_repo',1280, dcFile(`<div class="wrap">
${topbar(true,'',true)}
<div class="body">${repoSide('code')}<div class="main">
<div class="slabel">File tree — /tree/:ref/:path</div>
${repoTitle('Hello World')}
${card(null,`<div style="display:flex;align-items:center;gap:14px;margin-bottom:2px"><span class="muted">Ref</span>${sel('main','110px')}<span class="crumb mono">hello-world / src</span></div>`)}
${card(null, [['folder','components','—'],['folder','utils','—'],['file','README.md','2 months ago'],['file','package.json','2 months ago'],['file','index.ts','3 months ago']].map(([t,n,d])=>`<div class="rowb" style="padding:9px 2px;border-bottom:1px solid ${c.bd}"><span style="display:flex;gap:10px;align-items:center;color:${t==='folder'?c.ac:c.mu}">${t==='folder'?IC.folder(16):IC.file(16)}<span style="color:${c.tx}">${n}</span></span><span class="muted" style="font-size:13px">${d}</span></div>`).join(''))}
<div class="slabel" style="margin-top:30px">File view — /blob/:ref/:path (README.md rendered + raw toggle)</div>
${card(`<span class="mono" style="font-weight:500">README.md</span><span style="display:flex;gap:8px">${bd('Preview','open')}<span class="btn out sm">Raw</span><span class="btn out sm">${IC.copy(14)} Copy</span></span>`, `<div style="font-size:15px"><div style="font-size:22px;font-weight:700;margin-bottom:10px;border-bottom:1px solid ${c.bd};padding-bottom:8px">Hello World</div><p class="muted">A minimal example repository used across the OpenGitBase wireframes.</p><div style="background:#fafafa;border:1px solid ${c.bd};border-radius:8px;padding:12px;font-family:'JetBrains Mono',monospace;font-size:13px;color:${c.tx}">git clone http://localhost:8089/demo-user/hello-world.git</div></div>`)}
</div></div>${foot()}</div>`))

add('CommitDiff','p_repo',720, dcFile(appShell(repoSide('code'), `
${backLink('Back to merge request !7')}
<div class="mono muted" style="font-size:13px">abc123de</div>
<h1 class="h1" style="margin:4px 0 8px">refactor protected branch policy editor</h1>
<div class="muted" style="font-size:13.5px;display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:6px">demo-user · 2 weeks ago · 1 file changed, +2 −1 ${bd('Pipeline passed','ok')}</div>
<div class="muted" style="font-size:13px;margin-bottom:14px">Parents <span class="mono" style="color:${c.ac}">fff00000</span></div>
<div style="display:flex;gap:10px;margin-bottom:22px">${btn('Browse files','soft',IC.folder(15)+' ')}${btn('Copy SHA','out',IC.copy(15)+' ')}</div>
<div class="card"><div class="ch"><span class="mono" style="font-weight:500;font-size:14px">src/policy.ts</span>${bd('modified','n')}</div>
<div class="diff" style="border:none;border-radius:0">
<div class="fh mono">@@ -10,2 +10,3 @@</div>
<div class="ln"><div class="g">10</div><div class="g">10</div><div class="t">const allowed = rules.filter(Boolean)</div></div>
<div class="ln" style="background:${c.beBg}"><div class="g">11</div><div class="g"></div><div class="t">return allowed.some(isAllowed)</div></div>
<div class="ln" style="background:${c.okBg}"><div class="g"></div><div class="g">11</div><div class="t">return allowed.some(rule =&gt; rule.matches(ref))</div></div>
</div></div>
`, true)))

add('Discussions','p_repo',560, dcFile(appShell(repoSide('disc'), `
${backLink('demo-user/hello-world')}
${headerBlock('','Discussions','', btn('New discussion','pri',IC.plus(16)+' '))}
${card(null,[['Architecture review','Open','ok','#1 · demo-user · 2 months ago · 1 comment'],['Protect default branch','Open','ok','#12 · reviewer · 3 weeks ago · 4 comments']].map(([t,s,k,m])=>`<div style="padding:12px 2px;border-bottom:1px solid ${c.bd}"><div style="display:flex;gap:10px;align-items:center"><span style="color:${c.ac}">${IC.chat(16)}</span><b>${t}</b>${bd(s,k)}</div><div class="muted mono" style="font-size:12.5px;margin-top:4px;margin-left:26px">${m}</div></div>`).join(''))}
`, true)))

add('DiscussionDetail','p_repo',760, dcFile(appShell(repoSide('disc'), `
${backLink('All discussions')}
<div style="display:flex;gap:24px">
<div style="flex:1;min-width:0">
<h1 class="h1" style="margin-bottom:8px">Architecture review</h1><div style="margin-bottom:18px">${bd('Open','ok')}</div>
${card('Linked discussions', `<p class="muted" style="margin:0 0 12px;border-bottom:1px solid ${c.bd};padding-bottom:14px">No linked discussions.</p>${btn('Link discussion','soft sm')}`)}
<div class="slabel" style="margin:6px 0 12px">Comments</div>
<div class="card"><div class="cb">
<div class="rowb"><span style="display:flex;gap:8px;align-items:center">${IC.chev(15)}<b>demo-user</b> <span class="muted" style="font-size:12px">1 reply</span></span><span class="muted" style="font-size:12px">2 months ago</span></div>
<p style="margin:10px 0 0">Consider extracting this helper.</p>
<div style="margin-left:20px;margin-top:12px;border-left:2px solid ${c.bd};padding-left:14px"><div class="rowb"><b>reviewer</b><span class="muted" style="font-size:12px">2 months ago</span></div><p style="margin:6px 0 0">Agreed — I pushed a follow-up snippet.</p></div>
</div></div>
<div class="divider"></div><p class="muted">Sign in to join the conversation.</p>${btn('Sign in','pri')}
</div>
<aside style="width:280px;flex:none"><div class="card"><div class="cb" style="font-size:13px;line-height:1.9">
<div class="muted" style="font-size:11px;text-transform:uppercase">Discussion</div><div style="margin-bottom:8px">#1</div>
<div class="muted" style="font-size:11px;text-transform:uppercase">Status</div><div style="margin-bottom:8px">${bd('Open','ok')}</div>
<div class="muted" style="font-size:11px;text-transform:uppercase">Creator</div><div style="margin-bottom:8px">demo-user</div>
<div class="muted" style="font-size:11px;text-transform:uppercase">Opened</div><div style="margin-bottom:8px">2 months ago</div>
<div class="muted" style="font-size:11px;text-transform:uppercase">Linked merge requests</div><div style="margin-bottom:14px">No linked merge requests.</div>
<div class="btn wide" style="background:${c.okBg};color:${c.okTx};margin-bottom:8px">Resolve</div><div class="btn wide" style="background:${c.wBg};color:${c.wTx}">Dismiss</div>
</div></div></aside>
</div>
`, true)))

add('MergeRequests','p_repo',540, dcFile(appShell(repoSide('mr'), `
${headerBlock('demo-user/hello-world','Merge requests','', btn('New merge request','pri',IC.merge(16)+' '))}
<div class="tabs" style="max-width:280px;margin-bottom:18px"><span class="tab on">Open</span><span class="tab">Merged</span><span class="tab">Closed</span></div>
${card(null,[['Refactor branch policy editor','Open','ok','!7 · feature/branch-rules → main · 2 weeks ago'],['Add pipeline caching','Draft','w','!9 · feat/cache → main · 3 days ago']].map(([t,s,k,m])=>`<div style="padding:12px 2px;border-bottom:1px solid ${c.bd}"><div style="display:flex;gap:10px;align-items:center"><span style="color:${c.ac}">${IC.merge(16)}</span><b>${t}</b>${bd(s,k)}</div><div class="muted mono" style="font-size:12.5px;margin-top:4px;margin-left:26px">${m}</div></div>`).join(''))}
`, true)))

add('MergeRequestNew','p_repo',640, dcFile(appShell(repoSide('mr'), `
${backLink('Merge requests')}
<h1 class="h1" style="margin-bottom:20px">New merge request</h1>
${card(null, `
${field('Title','',true,'100%')}
<div style="display:flex;gap:20px"><div style="flex:1"><label class="lb">Source branch <span class="req">*</span></label>${sel('main','100%')}</div><div style="flex:1"><label class="lb">Target branch <span class="req">*</span></label>${sel('main','100%')}</div></div>
<div style="margin:16px 0"><span class="chk"></span>Create as draft</div>
<label class="lb">Description</label>
<div style="border:1px solid ${c.bd2};border-radius:8px"><div style="border-bottom:1px solid ${c.bd};padding:8px 12px;display:flex;gap:14px;color:${c.mu};font-size:13px"><b>B</b><i>I</i><u>U</u><s>S</s><span class="mono">&lt;/&gt;</span><span>🔗</span><span>• list</span><span>1. list</span><span>H2</span><span>H3</span><span>table</span></div><div style="min-height:90px"></div></div>
<div style="margin-top:18px">${btn('Create merge request','pri')}</div>`)}
`, true)))

add('MergeRequestDetail','p_repo',720, dcFile(appShell(repoSide('mr'), `
${backLink('Merge requests')}
<div class="mono muted" style="font-size:13px">!7 · demo-user/hello-world</div>
<h1 class="h1" style="margin:4px 0 10px">Refactor branch policy editor</h1>
<div style="display:flex;gap:10px;align-items:center;margin-bottom:18px">${bd('Open','open')}<span class="muted mono" style="font-size:13px">feature/branch-rules → main</span></div>
<div style="display:flex;gap:24px">
<div style="flex:1;min-width:0">
<div class="tabs" style="margin-bottom:20px"><span class="tab on">Overview</span><span class="tab">Changes</span><span class="tab">Commits</span></div>
${card(null,'<p style="margin:0">This merge request adds reusable policy controls.</p>')}
${card('Overview comments', `<div class="repocard"><div class="rowb"><b>reviewer</b><span class="muted" style="font-size:12px">2 weeks ago</span></div><p style="margin:8px 0 0">Looks good overall. One nit in changes tab.</p></div><p class="muted" style="margin:14px 0 0">Sign in to join the conversation.</p>`)}
</div>
<aside style="width:290px;flex:none">${card('Linked discussions', `<div style="margin-bottom:14px"><div style="display:flex;gap:8px;margin-bottom:6px">${bd('Closes','ok')}<span class="muted" style="font-size:12px">Resolves when this MR merges</span></div><div class="repocard"><b>#12 Protect default branch</b><div class="muted" style="font-size:12px;margin-top:2px">Open</div></div></div><div><div style="display:flex;gap:8px;margin-bottom:6px">${bd('Implements','open')}<span class="muted" style="font-size:12px">Delivers the tracked work</span></div><div class="repocard"><b>#5 Policy matcher refactor</b><div class="muted" style="font-size:12px;margin-top:2px">Open</div></div></div>`)}</aside>
</div>
`, true)))

add('Pipelines','p_repo',1180, dcFile(`<div class="wrap">
${topbar(true,'',true)}<div class="body">${repoSide('pipe')}<div class="main">
<div class="slabel">Pipelines list — /pipelines</div>
${headerBlock('demo-user/hello-world','Pipelines')}
${card(null,`<div class="rowb"><div><div style="font-weight:600">refs/heads/main</div><div class="mono muted" style="font-size:12.5px;margin-top:4px">abc123def4567890abcdef1234567890abcdef12</div><div class="muted" style="font-size:12.5px;margin-top:4px">2 months ago</div></div>${bd('Passed','ok')}</div>`)}
<div class="slabel" style="margin-top:26px">Pipeline run detail — /pipelines/:runId</div>
${card(null,`<div class="rowb"><div><div class="mono" style="font-size:14px">abc123def4567890abcdef1234567890abcdef12</div><div class="muted mono" style="font-size:12.5px;margin-top:4px">refs/heads/main</div></div>${bd('Passed','ok')}</div>`)}
<div class="grid2">
${card('Jobs', `<div class="repocard" style="border-color:${c.ac};border-width:2px;margin-bottom:12px"><div class="rowb"><div><b>build</b><div class="muted" style="font-size:12.5px;margin-top:2px">build · ogb-hosted</div></div>${bd('Passed','ok')}</div></div><div class="repocard"><div class="rowb"><div><b>test</b><div class="muted" style="font-size:12.5px;margin-top:2px">test · ogb-hosted</div></div>${bd('Passed','ok')}</div></div>`)}
${card('Logs', `<div style="background:#fafafa;border:1px solid ${c.bd};border-radius:8px;padding:14px;font-family:'JetBrains Mono',monospace;font-size:12.5px"><div class="muted">WORKSPACE</div><div style="margin-top:6px">Workspace prepared at /tmp/opengitbase-agent/run-1/repo</div></div>`)}
</div>
</div></div>${foot()}</div>`))

add('RepoMembers','p_repo',640, dcFile(appShell(repoSide('members',{owner:true}), `
${backLink('demo-user/hello-world')}
<h1 class="h1" style="margin-bottom:20px">Members</h1>
${card('Add member', `${field('Username or email','username or email@example.com',true,'100%')}<label class="lb">Role</label>${sel('Maintainer','160px')}<div style="margin-top:16px">${btn('Add member','pri')}</div>`)}
${card('Current members', `<div class="rowb" style="padding:6px 0"><div><b>demo-user</b> <span class="muted" style="font-size:12px">· you</span></div>${sel('Owner','140px')}</div>`)}
`, true)))

add('RepoSettings','p_repo',1220, dcFile(appShell(repoSide('settings',{owner:true}), `
${backLink('demo-user/hello-world')}
<h1 class="h1" style="margin-bottom:20px">Repository settings</h1>
${card(null, `${field('Repository name','Hello World',true,'100%')}<div style="margin:2px 0 16px"><span class="chk"></span>Private repository</div>${btn('Save changes','pri')}`)}
${card('Storage usage', `<div class="rowb" style="margin-bottom:8px"><span>500.0 MB / 1.00 GB</span><span class="muted">49%</span></div><div class="bar"><i style="width:49%"></i></div>`)}
${card('Per-repository storage limit', `<p class="muted" style="margin:0">Override eligibility is not available.</p>`)}
${card('Branches &amp; push rules', `<p class="muted" style="margin:0 0 14px">Manage default branch, protected branch rules, and push policies.</p>${btn('Manage branches &amp; push rules','soft',IC.merge(15)+' ')}<div style="margin-top:16px;border-top:1px dashed ${c.bd};padding-top:14px"><div class="slabel">Branch rules sub-page (/settings/branches)</div><div class="rowb" style="padding:8px 0;border-bottom:1px solid ${c.bd}"><span class="mono">main</span><span style="display:flex;gap:8px">${bd('Protected','open')}${bd('Require MR','n')}</span></div><div class="rowb" style="padding:8px 0"><span class="muted">Default branch</span>${sel('main','110px')}</div></div>`)}
${card('Discussion settings', `<p class="muted" style="margin:0 0 14px">Block users from creating discussions or commenting.</p>${btn('Manage blocked users','soft',IC.shield(15)+' ')}`)}
${card(`<span style="color:${c.eTx}">Danger zone</span>`, `<p class="muted" style="margin:0 0 14px">Deleting a repository is permanent and cannot be undone.</p>${btn('Delete repository','dng')}`)}
`, true)))

/* ---------- ORG & ACCOUNT ---------- */
add('OrgOverview','p_org',560, dcFile(appShell(orgSide('ov'), `
${backLink('acme-corp')}
<div style="display:flex;align-items:center;gap:12px;margin-bottom:6px"><h1 class="h1">Acme Corp</h1>${bd('Organization','open')}</div>
<p class="sub" style="margin-bottom:22px">Building things together.</p>
<h2 class="h2" style="margin:0 0 14px">Repositories</h2>
<div class="repocard" style="max-width:640px"><div class="rowb"><div style="font-weight:600;font-size:16px">Hello World</div>${bd('Public','ok')}</div><div class="mono muted" style="margin:6px 0 12px;font-size:13px">acme-corp/hello-world</div><div class="muted" style="font-size:13px">Updated 3 months ago</div></div>
`, true)))

add('OrgMembers','p_org',760, dcFile(appShell(orgSide('members'), `
${backLink('acme-corp')}
<h1 class="h1" style="margin-bottom:20px">Members</h1>
${card('Add member', `${field('Username or email','username or email@example.com',true,'100%')}<label class="lb">Role</label>${sel('Owner','160px')}<div style="margin-top:16px">${btn('Add member','pri')}</div>`)}
${card('Current members', `<div style="padding:6px 0"><b>demo-user</b><div style="margin-top:6px">${sel('Owner','140px')}</div></div>`)}
${card('Pending invitations', `<div class="rowb"><div><b>pe***@example.com</b><div class="muted" style="font-size:12.5px;margin-top:2px">Member · Expires 3 months ago</div></div><div style="display:flex;gap:16px"><a>Resend</a><a style="color:${c.eTx}">Revoke</a></div></div>`)}
`, true)))

add('OrgStorageCompute','p_org',1240, dcFile(`<div class="wrap">
${topbar(true,'',true)}<div class="body">${orgSide('storage')}<div class="main">
<div class="slabel">Organization storage — /:owner/storage</div>
${backLink('acme-corp')}<h1 class="h1" style="margin-bottom:6px">Organization storage</h1><p class="sub" style="margin-bottom:20px">Enroll self-hosted storage nodes to earn quota credits and control placement. <a>Read the storage nodes guide</a></p>
${card('Quota credits', `<div class="grid3"><div><div class="muted" style="font-size:12.5px">Platform limit</div><div style="font-size:16px;margin-top:2px">1.00 GB</div></div><div><div class="muted" style="font-size:12.5px">Contributed capacity</div><div style="font-size:16px;margin-top:2px">2.00 GB</div></div><div><div class="muted" style="font-size:12.5px">Effective limit</div><div style="font-size:16px;margin-top:2px">3.00 GB</div></div></div>`)}
${card('Registered storage nodes', `<div class="rowb" style="padding:8px 0;border-bottom:1px solid ${c.bd}"><span class="mono">storage-1</span>${bd('Healthy','ok')}</div><div class="rowb" style="padding:8px 0"><span class="mono">storage-2</span>${bd('Draining','w')}</div>`)}
${card('Storage enrollments', `<div style="display:flex;gap:16px;align-items:end;flex-wrap:wrap"><div><label class="lb">Node ID <span class="req">*</span></label><div class="inp" style="width:220px">storage-1</div></div><div><label class="lb">Max capacity (GB)</label><div class="inp" style="width:140px;color:${c.fa}">100</div></div>${btn('Create enrollment','pri')}</div>`)}
${card('Placement defaults', `<div style="line-height:2">Default placement: <b>Max self-host</b><br>Self-host preference: <b>Prefer self-host</b></div>`)}
<div class="slabel" style="margin-top:26px">Organization compute — /:owner/compute</div>
${card('Compute nodes', `<div class="rowb" style="padding:8px 0"><span class="mono">org-runner-1</span>${bd('Healthy','ok')}</div>`,`${bd('1/1 Healthy','ok')}`)}
${card('Compute enrollments', `<div style="display:flex;gap:16px;align-items:end;flex-wrap:wrap"><div><label class="lb">Node ID <span class="req">*</span></label><div class="inp" style="width:200px">org-runner-1</div></div><div><label class="lb">Max jobs</label><div class="inp" style="width:80px">2</div></div>${btn('Create enrollment','pri')}</div>`)}
</div></div>${foot()}</div>`))

add('AccountSettings','p_org',1180, dcFile(`<div class="wrap">
${topbar(true,'',true)}<div class="body">${acctSide('profile')}<div class="main">
<div class="slabel">Profile — /settings</div>
${headerBlock('','Account settings','Manage your profile, security, and SSH keys.')}
${card('Profile', `<div class="kv"><span class="muted">Username</span><b>demo-user</b></div><div class="kv" style="border:none"><span class="muted">Email status</span>${bd('Verified','ok')}</div><div style="margin-top:12px">${btn('Access tokens','soft sm')}</div><p class="muted" style="font-size:13px;margin:12px 0 0">This instance uses HTTPS and personal access tokens for Git.</p>`)}
${card('Change password', `${field('Current password','',true,'340px')}${field('New password','',true,'340px')}${btn('Update password','pri')}`)}
${card(`<span style="color:${c.eTx}">Delete account</span>`, `<p class="muted" style="margin:0 0 14px">Permanently delete your account and all associated data. This cannot be undone.</p>${btn('Delete account','dng')}`)}
<div class="slabel" style="margin-top:24px">Access tokens — /settings/access-tokens</div>
${card('New token', `${field('Token name','',true,'340px')}<label class="lb">Scope</label>${sel('Read (clone, fetch, pull)','240px')}<div style="margin:14px 0"><span class="chk"></span>Never expires</div>${btn('Create token','pri')}`)}
<div class="slabel" style="margin-top:12px">SSH keys — /settings/ssh-keys (when SSH enabled)</div>
${card('Add SSH key', `${field('Title','',true,'340px')}<label class="lb">Public key</label><div class="inp ta mono" style="color:${c.fa}">ssh-ed25519 AAAA…</div><div style="margin-top:14px">${btn('Add key','pri')}</div>`)}
</div></div>${foot()}</div>`))

add('CreateForms','p_org',700, dcFile(`<div class="wrap">
${topbar(true,'',true)}<div class="body">${dashSide()}<div class="main">
<div class="slabel">New repository — /repos/new</div>
${headerBlock('','New repository','Create a new Git repository.')}
${card(null,`${field('Repository name','',true,'360px')}${field('Slug','',true,'360px')}<label class="lb">Owner</label>${sel('Personal','160px')}<div style="margin:14px 0"><span class="chk"></span>Private repository</div>${btn('Create repository','pri')}`)}
<div class="slabel" style="margin-top:20px">New organization — /orgs/new</div>
${card(null,`${field('Organization name','',true,'360px')}${field('Slug','',true,'360px')}<div style="margin-top:4px">${btn('Create organization','pri')}</div>`)}
</div></div>${foot()}</div>`))

/* ---------- ADMIN ---------- */
add('AdminHome','p_admin',640, dcFile(appShell(adminSide('home'), `
${headerBlock('','Administration','Instance configuration and fleet management.')}
<div class="grid2">
${[[IC.server(20),'Storage fleet','View storage nodes, create enrollments, and manage dispatcher SSH keys.'],[IC.cpu(20),'Compute fleet','View platform compute nodes, create enrollment tokens, and bootstrap local agents.'],[IC.activity(20),'Status & incidents','Publish operator incident banners on the public status page.'],[IC.dbrepl(20),'Repository replication','Audit RF=3 health, backfill progress, and watermark lag across repositories.']].map(([i,t,d])=>`<div class="repocard"><div style="width:40px;height:40px;border-radius:10px;background:${c.softBg};color:${c.softTx};display:flex;align-items:center;justify-content:center;margin-bottom:12px">${i}</div><div style="font-weight:600;font-size:16px">${t}</div><p class="muted" style="font-size:13.5px;margin:6px 0 12px">${d}</p><a style="display:inline-flex;gap:6px;align-items:center">Open ${IC.aright(15)}</a></div>`).join('')}
</div>
`, true)))

add('AdminStorage','p_admin',940, dcFile(appShell(adminSide('storage'), `
${backLink('Admin')}
${headerBlock('','Storage fleet','View storage nodes, create enrollments, and manage dispatcher SSH keys.', btn('Refresh','soft',IC.refresh(15)+' '))}
${card('Replication rollup', banner('0/3 nodes healthy — RF=3 not available','w'))}
${card('Needs attention', `<p class="muted" style="margin:0">No repositories need attention.</p>`, `<a>View all repositories</a>`)}
${card('Storage nodes', `<p class="muted" style="margin:0">No storage nodes registered yet.</p>`, bd('0','n'))}
${card('Storage enrollments', `<div style="display:flex;gap:16px;align-items:end;flex-wrap:wrap"><div><label class="lb">Node ID <span class="req">*</span></label><div class="inp" style="width:220px">storage-1</div></div><div><label class="lb">Expires in hours (optional)</label><div class="inp" style="width:200px;color:${c.fa}">168</div></div>${btn('Create enrollment','pri')}</div>`)}
${card('Fleet dispatcher SSH keys', btn('Generate new fleet keys','soft'))}
`, true)))

add('AdminCompute','p_admin',720, dcFile(appShell(adminSide('compute'), `
${backLink('Admin')}
${headerBlock('','Compute fleet','View platform compute nodes, create enrollment tokens, and bootstrap local agents.', btn('Refresh','soft',IC.refresh(15)+' '))}
${banner('<b>Local development bootstrap</b> — Run scripts/bootstrap-fleet.sh to mint a platform compute enrollment token and inject it into docker-compose.override.yml.','i')}
${card('Compute nodes', `<p class="muted" style="margin:0">No compute nodes registered yet.</p>`, bd('0/0 Healthy','n'))}
${card('Compute enrollments', `<div style="display:grid;grid-template-columns:repeat(4,1fr);gap:16px">${[['Node ID','compute-agent-1'],['Max concurrent jobs','2'],['Max vCPU','2'],['Max memory (GiB)','2']].map(([l,v])=>`<div><label class="lb">${l} <span class="req">*</span></label><div class="inp">${v}</div></div>`).join('')}</div><div style="margin-top:16px">${btn('Create enrollment','pri')}</div>`)}
`, true)))

add('AdminCiSupply','p_admin',680, dcFile(appShell(adminSide('ci'), `
${headerBlock('','CI supply chain','Manage base image catalog entries and dependency layer promotions.')}
${card('Base image catalog', `<p class="muted" style="margin:0 0 14px">No catalog entries yet.</p><div style="display:grid;grid-template-columns:1fr 1fr;gap:12px"><div class="inp">alpine</div><div class="inp">3.20</div><div class="inp" style="color:${c.fa}">os / arch</div><div class="inp">docker.io/library/alpine:3.20</div></div><div style="margin-top:14px">${btn('Add catalog entry','pri')}</div>`)}
${card('Dependency promotion dashboard', `<p class="muted" style="margin:0">No dependency install analytics yet.</p>`)}
`, true)))

add('AdminEgress','p_admin',640, dcFile(appShell(adminSide('egress'), `
${headerBlock('','Domain allowance','Review platform egress domain requests and submit new allowance requests.')}
${card('Submit request', `<div class="inp" style="margin-bottom:12px">registry.npmjs.org</div><div class="inp ta" style="color:${c.fa};margin-bottom:14px">Reason for this domain…</div>${btn('Submit request','pri wide')}`)}
${card('Pending platform requests', `<p class="muted" style="margin:0">No pending requests.</p>`)}
`, true)))

add('AdminStatus','p_admin',720, dcFile(appShell(adminSide('status'), `
${backLink('Administration')}
${headerBlock('','Status & incidents','Publish operator incident banners on the public status page.', btn('View public page','out',IC.ext(14)+' '))}
${card(null, `<label class="lb">Incident message</label><div class="inp ta" style="color:${c.fa};max-width:420px">Describe the impact and current state for visitors.</div><label class="lb" style="margin-top:16px">Severity</label>${sel('Warning','150px')}<div style="margin-top:16px;display:flex;gap:14px;align-items:center">${btn('Publish banner','pri')}<a style="display:flex;gap:6px;align-items:center">${IC.server(15)} Storage fleet</a></div>`)}
${card('Outage windows', `<p class="muted" style="margin:0 0 4px">Suppress false positives and annotate planned work. Start and end times are not editable.</p><p class="muted" style="margin:0">No outage windows recorded yet.</p>`, `<a style="display:flex;gap:6px;align-items:center">View public page ${IC.ext(14)}</a>`)}
`, true)))

add('AdminReplication','p_admin',620, dcFile(appShell(adminSide('repl'), `
${backLink('Admin')}
${headerBlock('','Repository replication','Paginated fleet view of replication state, quorum, and sync progress.', btn('Refresh','soft',IC.refresh(15)+' '))}
${card(null, `<div style="display:flex;gap:14px;align-items:center"><div class="inp" style="flex:1;color:${c.fa}">${IC.search(15)} <span style="margin-left:8px">Search by name or owner…</span></div>${sel('All','120px')}${sel('Severity','130px')}</div>`)}
${card(null, `<div style="display:grid;grid-template-columns:2fr 1fr 1fr 1.4fr;gap:8px;font-size:12.5px;color:${c.mu};padding-bottom:10px;border-bottom:1px solid ${c.bd}"><span>Repository</span><span>Quorum</span><span>Healthy</span><span>Watermark lag</span></div><div style="display:grid;grid-template-columns:2fr 1fr 1fr 1.4fr;gap:8px;align-items:center;padding:12px 0"><span class="mono">demo-user/hello-world</span>${bd('RF=3','ok')}<span>3 / 3</span><span class="muted">0 commits</span></div>`)}
`, true)))

/* ---------- DOCS ---------- */
add('DocsIndex','p_docs',900, dcFile(plainShell(`
${headerBlock('','Documentation','Guides for using OpenGitBase features.')}
<h2 class="h2" style="font-size:20px;margin:8px 0 4px">Storage</h2><p class="sub" style="margin:0 0 14px">Register organization-contributed storage nodes and manage self-host placement.</p>
<div class="grid2" style="margin-bottom:28px"><div class="repocard"><a style="font-weight:600;font-size:15px">Organization storage nodes</a><p class="muted" style="font-size:13px;margin:4px 0 0">Register hosts that store Git repositories for your organization.</p></div><div></div></div>
<h2 class="h2" style="font-size:20px;margin:0 0 4px">CI/CD</h2><p class="sub" style="margin:0 0 14px">Run pipelines in Firecracker MicroVMs on platform, organization, or community compute.</p>
<div class="grid2">${[['Overview','What OpenGitBase CI/CD is and how the pieces fit together.'],['Quick start','Add your first pipeline in a few minutes.'],['How it works','End-to-end flow from git push to job teardown.'],['Pipeline YAML','Structure of .opengitbase-ci.yml v1.'],['Hosting profiles','Choose where jobs run with runs-on.'],['Compute nodes','Register hosts that execute CI jobs.'],['Base images','Curated root filesystems for MicroVM boot.'],['Network egress','Allowlists and domain allowance requests.']].map(([t,d])=>`<div class="repocard"><a style="font-weight:600;font-size:15px">${t}</a><p class="muted" style="font-size:13px;margin:4px 0 0">${d}</p></div>`).join('')}</div>
`, false, 'Docs')))

add('DocsArticle','p_docs',720, dcFile(`<div class="wrap">
${topbar(false,'Docs')}<div class="body">${docsSide('ov')}<div class="main" style="max-width:760px">
<div class="crumb" style="margin-bottom:4px">CI/CD</div><h1 class="h1">Overview</h1><p class="sub" style="margin-bottom:24px">What OpenGitBase CI/CD is and how the pieces fit together.</p>
<div style="border-top:1px solid ${c.bd};padding-top:20px;font-size:15px;line-height:1.7">
<h2 style="font-size:20px;margin:0 0 10px">OpenGitBase CI/CD</h2>
<p style="color:${c.tx};margin:0 0 8px">Pipelines run inside <b>Firecracker MicroVMs</b> on registered <b>Compute Nodes</b>. Define pipelines in a file at the repository root:</p>
<div style="background:#fafafa;border:1px solid ${c.bd};border-radius:8px;padding:10px 14px;font-family:'JetBrains Mono',monospace;font-size:13px;margin-bottom:16px">.opengitbase-ci.yml</div>
<h3 style="font-size:16px;margin:0 0 8px">What you get</h3>
<ul style="margin:0 0 12px;padding-left:20px;color:${c.tx}"><li>Isolated job sandboxes — each job runs in its own MicroVM.</li><li>Hybrid compute — platform, organization, or community nodes.</li><li>Push-triggered runs (v1).</li><li>Editor support — JSON Schema for validation in VS Code.</li></ul>
</div>
</div></div>${foot()}</div>`))

/* =====================================================================
   WRITE FILES + canvas.json
   ===================================================================== */
for (const s of screens) writeFileSync(OUT + s.name + '.dc.html', s.html)

const pages = [
  ['p_found','Foundation'],['p_pub','Public & discovery'],['p_auth','Auth'],
  ['p_repo','Repository'],['p_org','Org & account'],['p_admin','Admin console'],['p_docs','Docs'],
]
const COLS = 3, XSTEP = 1400
const artboards = []
for (const [pid] of pages){
  const list = screens.filter(s=>s.page===pid)
  let rowY = 0, i = 0
  while (i < list.length){
    const row = list.slice(i, i+COLS)
    let maxH = 0
    row.forEach((s, j)=>{ artboards.push({ file:s.name+'.dc.html', x:j*XSTEP, y:rowY, w:1280, h:s.h, page:pid }); maxH=Math.max(maxH,s.h) })
    rowY += maxH + 180; i += COLS
  }
}
const canvas = {
  artboards,
  pages: pages.map(([id,name])=>({id,name})),
  launch: { view:'canvas', page:'p_found' },
}
writeFileSync(OUT + 'canvas.json', JSON.stringify(canvas, null, 2))
console.log('wrote', screens.length, 'artboards +', 'canvas.json')
console.log(screens.map(s=>s.name).join(', '))
