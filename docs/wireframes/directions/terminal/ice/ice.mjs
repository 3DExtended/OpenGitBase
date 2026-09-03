import { writeFileSync } from 'node:fs'
const OUT = new URL('./', import.meta.url).pathname

/* ---- tokens: Terminal / Ice ---- */
const c = {
  bg:'#0b0f10', sf:'#12181a', sf2:'#161d1f', sf3:'#1b2325', bd:'#232b2d', bd2:'#2c3538',
  tx:'#e6edf0', mu:'#8b979c', dim:'#5f6a6e',
  ac:'#38e0e6', acd:'#0e3b40', btx:'#03181b',
  ok:'#7ee787', okd:'#12331d', amber:'#f5b350', amd:'#3a2c12', red:'#ff6b6b', redd:'#3a1a1c', violet:'#a78bff',
}

/* ---- svg icons ---- */
const S=(p,s=18,sw=2,col='currentColor')=>`<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="none" stroke="${col}" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" style="flex:none">${p}</svg>`
const I = {
  logo:(s=22,col=c.ac)=>S(`<circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="9" r="2.4"/><path d="M6 8.4v7.2"/><path d="M18 11.4c0 3-2.5 3.6-5.5 4.2C10 16 8.4 16.6 8.4 18"/>`,s,2,col),
  code:(s=16,col='currentColor')=>S(`<path d="M16 18l4-6-4-6M8 6l-4 6 4 6"/>`,s,2,col),
  merge:(s=16,col='currentColor')=>S(`<circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="15" r="2.4"/><path d="M6 8.4v7.2M6 9a6 6 0 0 0 6 6h3.6"/>`,s,2,col),
  chat:(s=16,col='currentColor')=>S(`<path d="M21 15a2 2 0 0 1-2 2H8l-4 3V6a2 2 0 0 1 2-2h13a2 2 0 0 1 2 2z"/>`,s,2,col),
  pipe:(s=16,col='currentColor')=>S(`<rect x="3" y="4" width="7" height="7" rx="1.5"/><rect x="14" y="13" width="7" height="7" rx="1.5"/><path d="M6.5 11v3a3 3 0 0 0 3 3H14"/>`,s,2,col),
  users:(s=16,col='currentColor')=>S(`<path d="M16 20v-1a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v1"/><circle cx="9" cy="8" r="3.2"/><path d="M22 20v-1a4 4 0 0 0-3-3.8"/>`,s,2,col),
  gear:(s=16,col='currentColor')=>S(`<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-2.7 1.1V21a2 2 0 0 1-4 0v-.1A1.6 1.6 0 0 0 7 19.4a1.6 1.6 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1A1.6 1.6 0 0 0 3 15H2.9a2 2 0 0 1 0-4H3a1.6 1.6 0 0 0 1.1-2.7l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1A1.6 1.6 0 0 0 9 3V2.9a2 2 0 0 1 4 0V3a1.6 1.6 0 0 0 2.7 1.1l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1A1.6 1.6 0 0 0 21 9h.1a2 2 0 0 1 0 4H21a1.6 1.6 0 0 0-1.6 2z"/>`,s,1.6,col),
  book:(s=16,col='currentColor')=>S(`<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>`,s,2,col),
  search:(s=16,col='currentColor')=>S(`<circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/>`,s,2,col),
  server:(s=16,col='currentColor')=>S(`<rect x="3" y="4" width="18" height="7" rx="1.5"/><rect x="3" y="13" width="18" height="7" rx="1.5"/><path d="M7 7.5h.01M7 16.5h.01"/>`,s,2,col),
  cpu:(s=16,col='currentColor')=>S(`<rect x="5" y="5" width="14" height="14" rx="2"/><rect x="9" y="9" width="6" height="6"/><path d="M9 2v2M15 2v2M9 20v2M15 20v2M2 9h2M2 15h2M20 9h2M20 15h2"/>`,s,2,col),
  home:(s=16,col='currentColor')=>S(`<path d="M3 10.5 12 3l9 7.5"/><path d="M5 9.5V20h14V9.5"/>`,s,2,col),
  star:(s=16,col='currentColor')=>S(`<path d="M12 3l2.6 5.3 5.9.9-4.2 4.1 1 5.8L12 16.9 6.7 19.6l1-5.8L3.5 9.7l5.9-.9z"/>`,s,2,col),
  fork:(s=16,col='currentColor')=>S(`<circle cx="6" cy="5" r="2.2"/><circle cx="18" cy="5" r="2.2"/><circle cx="12" cy="19" r="2.2"/><path d="M6 7.2v3a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2v-3M12 12.4v4.4"/>`,s,2,col),
  bell:(s=16,col='currentColor')=>S(`<path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>`,s,2,col),
  plus:(s=16,col='currentColor')=>S(`<path d="M12 5v14M5 12h14"/>`,s,2,col),
  file:(s=16,col='currentColor')=>S(`<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/>`,s,2,col),
  folder:(s=16,col='currentColor')=>S(`<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>`,s,2,col),
  check:(s=16,col='currentColor')=>S(`<path d="M20 6 9 17l-5-5"/>`,s,2,col),
  dot:(s=16,col='currentColor')=>S(`<circle cx="12" cy="12" r="4"/>`,s,2,col),
  arrow:(s=16,col='currentColor')=>S(`<path d="M5 12h14M13 6l6 6-6 6"/>`,s,2,col),
  clock:(s=16,col='currentColor')=>S(`<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>`,s,2,col),
  shield:(s=16,col='currentColor')=>S(`<path d="M12 3l7 3v5c0 4.5-3 8-7 10-4-2-7-5.5-7-10V6z"/>`,s,2,col),
}

/* ---- helpers ---- */
const av=(seed,s=26)=>`<span style="width:${s}px;height:${s}px;border-radius:${s>30?8:6}px;flex:none;background:linear-gradient(135deg,${seed},${c.sf3});border:1px solid ${c.bd};display:inline-block"></span>`
const badge=(t,kind='n')=>{const m={ac:[c.ac,c.ac],ok:[c.ok,c.ok],amber:[c.amber,c.amber],red:[c.red,c.red],violet:[c.violet,c.violet],n:[c.mu,c.bd2]};const[fg,br]=m[kind]||m.n;return`<span class="badge mono" style="color:${fg};border-color:${br}">${t}</span>`}
const btn=(t,kind='p',ic='')=>{const cls=kind==='p'?'btn p':kind==='g'?'btn g':'btn d';return`<span class="${cls} mono">${ic}${t}</span>`}
const card=(inner,pad=18)=>`<div class="card" style="padding:${pad}px">${inner}</div>`
const meter=(pct,col=c.ac)=>`<div class="meter"><i style="width:${pct}%;background:${col}"></i></div>`

/* ---- app chrome ---- */
function topbar(active){
  const nav=[['Dashboard','home'],['Explore','search'],['Docs','book']]
  return `<div class="top">
  <div class="tl">
    <span class="brand mono">${I.logo(22)} <b>opengitbase</b></span>
    <span class="crumb mono"><span style="color:${c.mu}">demo-user</span> / <span style="color:${c.tx}">hello-world</span></span>
  </div>
  <div class="tr">
    <span class="tsearch mono">${I.search(15,c.mu)} <span style="color:${c.dim}">search or jump to…</span> <kbd>⌘K</kbd></span>
    <span class="tibtn">${I.bell(17,c.mu)}</span>
    <span class="tibtn">${I.plus(17,c.mu)}</span>
    ${av('#0e3b40',28)}
  </div>
  </div>`
}
const navItem=(t,ic,on=false)=>`<a class="nav ${on?'on':''} mono">${I[ic](15,on?c.ac:c.mu)} ${t}</a>`
function repoSide(active){
  return `<div class="side">
    <div class="sidehdr mono">${I.code(14,c.mu)} demo-user / <b style="color:${c.tx}">hello-world</b></div>
    ${navItem('Code','code',active==='code')}
    ${navItem('Discussions','chat',active==='chat')}
    ${navItem('Merge requests','merge',active==='merge')}
    ${navItem('Pipelines','pipe',active==='pipe')}
    ${navItem('Members','users',active==='users')}
    ${navItem('Settings','gear',active==='gear')}
  </div>`
}
function adminSide(active){
  return `<div class="side">
    <div class="sidehdr mono">${I.shield(14,c.mu)} <b style="color:${c.tx}">admin console</b></div>
    ${navItem('Overview','home',active==='home')}
    ${navItem('Storage fleet','server',active==='server')}
    ${navItem('Compute fleet','cpu',active==='cpu')}
    ${navItem('CI supply chain','pipe',active==='pipe')}
    ${navItem('Status','dot',active==='dot')}
  </div>`
}
function dashSide(active){
  return `<div class="side">
    <div class="sidehdr mono"><b style="color:${c.tx}">demo-user</b></div>
    ${navItem('Overview','home',active==='home')}
    ${navItem('Repositories','code',active==='code')}
    ${navItem('Merge requests','merge',active==='merge')}
    ${navItem('Discussions','chat',active==='chat')}
    ${navItem('Organizations','users',active==='users')}
    ${navItem('Settings','gear',active==='gear')}
  </div>`
}
const shell=(side,main)=>`${topbar()}<div class="body">${side}<div class="main">${main}</div></div>`

/* ---- shared CSS ---- */
const CSS=`*{box-sizing:border-box}html{height:100%}
body{margin:0;min-height:100%;font-family:'Space Grotesk',system-ui,sans-serif;background:${c.bg};color:${c.tx};font-size:14px;line-height:1.5}
.wrap{width:1360px;min-height:100%;background:${c.bg}}
.mono{font-family:'JetBrains Mono',monospace}
kbd{font-family:'JetBrains Mono',monospace;font-size:10px;border:1px solid ${c.bd2};border-radius:4px;padding:1px 5px;color:${c.mu}}
h2{font-size:22px;font-weight:700;margin:0}
a{text-decoration:none;color:inherit}
.badge{font-family:'JetBrains Mono',monospace;font-size:11px;font-weight:500;padding:3px 9px;border-radius:5px;border:1px solid;white-space:nowrap}
.btn{display:inline-flex;align-items:center;gap:7px;font-weight:500;font-size:13px;padding:9px 15px;border-radius:8px;cursor:pointer;white-space:nowrap}
.btn.p{background:${c.ac};color:${c.btx}}
.btn.g{border:1px solid ${c.bd2};color:${c.tx}}
.btn.d{border:1px solid ${c.red};color:${c.red}}
.card{border:1px solid ${c.bd};border-radius:12px;background:${c.sf}}
.meter{height:8px;border-radius:5px;background:${c.bd};overflow:hidden}.meter i{display:block;height:100%}
/* topbar */
.top{height:56px;border-bottom:1px solid ${c.bd};display:flex;align-items:center;justify-content:space-between;padding:0 22px;background:${c.bg};position:sticky;top:0}
.tl{display:flex;align-items:center;gap:18px}
.brand{display:flex;align-items:center;gap:9px;font-weight:700;font-size:15px}
.crumb{font-size:13px;padding-left:18px;border-left:1px solid ${c.bd}}
.tr{display:flex;align-items:center;gap:14px}
.tsearch{display:flex;align-items:center;gap:9px;background:${c.sf};border:1px solid ${c.bd};border-radius:8px;padding:7px 12px;font-size:12px;width:280px}
.tibtn{width:34px;height:34px;border:1px solid ${c.bd};border-radius:8px;display:flex;align-items:center;justify-content:center;background:${c.sf}}
/* body + side */
.body{display:flex;align-items:flex-start}
.side{width:236px;flex:none;border-right:1px solid ${c.bd};padding:16px 12px;min-height:600px}
.sidehdr{font-size:11px;color:${c.mu};padding:4px 10px 12px;display:flex;align-items:center;gap:7px;flex-wrap:wrap}
.nav{display:flex;align-items:center;gap:10px;padding:9px 11px;border-radius:8px;font-size:13px;color:${c.mu};margin-bottom:3px}
.nav.on{background:${c.sf2};color:${c.ac};border:1px solid ${c.bd}}
.main{flex:1;padding:26px 32px;min-width:0}
/* tabs */
.tabs{display:flex;gap:4px;border-bottom:1px solid ${c.bd};margin-bottom:22px}
.tab{padding:10px 14px;font-size:13px;color:${c.mu};border-bottom:2px solid transparent;margin-bottom:-1px;display:flex;align-items:center;gap:7px}
.tab.on{color:${c.ac};border-bottom-color:${c.ac}}
/* misc */
.row{display:flex;align-items:center}
.muted{color:${c.mu}}
.dim{color:${c.dim}}
.h1big{font-size:26px;font-weight:700}
.grid3{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}
.grid2{display:grid;grid-template-columns:2fr 1fr;gap:22px}
.list>*{border-top:1px solid ${c.bd};padding:14px 0}.list>*:first-child{border-top:0}
.codeln{font-family:'JetBrains Mono',monospace;font-size:12.5px;line-height:1.85;white-space:pre}
.ln{display:inline-block;width:34px;color:${c.dim};text-align:right;margin-right:16px;user-select:none}
.kw{color:${c.violet}}.str{color:${c.ok}}.fn{color:${c.ac}}.cm{color:${c.dim}}
`

const FONTS=`<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;700&family=JetBrains+Mono:wght@400;500;700&display=swap">`
function dc(inner, css=CSS, pw=1360){
  return `<!doctype html>
<html><head><meta charset="utf-8"><script src="./support.js"></script></head>
<body><x-dc>
<helmet><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>${FONTS}<style>${css}</style></helmet>
<div class="wrap">${inner}</div>
</x-dc>
<script data-dc-script data-props='{"$preview":{"width":${pw},"height":1600}}'>
class Component extends DCLogic { renderVals(){ return {} } }
</script>
</body></html>`
}

const files=[]
const add=(name,inner,css,pw)=>{files.push(name);writeFileSync(OUT+name+'.dc.html',dc(inner,css,pw))}

/* ============================================================ 1 · HOME (logged out) */
{
const feat=(ic,t,d)=>card(`<div style="color:${c.ac};margin-bottom:12px">${I[ic](22,c.ac)}</div><div style="font-weight:600;margin-bottom:6px">${t}</div><div class="muted" style="font-size:13px">${d}</div>`,20)
const inner=`
<div class="lhead">
  <span class="brand mono">${I.logo(22)} <b>opengitbase</b></span>
  <div class="row" style="gap:18px">
    <a class="mono muted" style="font-size:13px">Explore</a><a class="mono muted" style="font-size:13px">Docs</a><a class="mono muted" style="font-size:13px">Pitch</a>
    ${btn('Sign in','g')}${btn('Get started','p')}
  </div>
</div>
<div class="hero">
  <div class="glow"></div>
  <div class="prompt mono">$ ogb init --self-hosted</div>
  <div class="htitle">Git that runs<br>on <em>your</em> metal.</div>
  <p class="hsub">Self-hosted repositories, merge requests, CI in Firecracker MicroVMs, and a storage fleet you control. Source-open, privacy-first, no black boxes.</p>
  <div class="row" style="gap:12px;margin-bottom:14px">${btn('Get started','p',I.arrow(15,c.btx))}${btn('Read the docs','g')}</div>
  <div class="mono dim" style="font-size:12px">$ curl -fsSL ogb.sh | sh &nbsp;·&nbsp; self-host in ~2 min</div>
</div>
<div style="padding:0 44px 40px"><div class="grid3">
  ${feat('code','Repositories','Full Git over HTTP &amp; SSH, branch protection, merge requests, and per-repo access control.')}
  ${feat('pipe','CI in MicroVMs','Pipelines run in isolated Firecracker VMs — reproducible, sandboxed, fast to spin up.')}
  ${feat('server','Storage fleet','Encrypted, quorum-replicated object storage you own and watch, node by node.')}
</div></div>
<div class="foot mono">opengitbase · source-open · self-hosted · © 2026</div>`
const css=CSS+`
.lhead{height:60px;display:flex;align-items:center;justify-content:space-between;padding:0 44px;border-bottom:1px solid ${c.bd}}
.hero{padding:70px 44px 56px;position:relative;overflow:hidden}
.glow{position:absolute;top:-160px;right:-60px;width:520px;height:400px;background:radial-gradient(circle,rgba(56,224,230,.16),transparent 68%);pointer-events:none}
.prompt{color:${c.ac};font-size:14px;margin-bottom:18px;position:relative}
.htitle{font-size:64px;font-weight:700;letter-spacing:-.025em;line-height:1.01;position:relative}
.htitle em{color:${c.ac};font-style:normal}
.hsub{color:${c.mu};font-size:18px;max-width:640px;margin:22px 0 28px;position:relative}
.wrap{background:${c.bg};background-image:radial-gradient(${c.bd} 1px,transparent 1px);background-size:26px 26px}
.foot{border-top:1px solid ${c.bd};padding:20px 44px;color:${c.dim};font-size:12px}`
add('Home',inner,css)
}

/* ============================================================ 2 · DASHBOARD */
{
const repo=(name,vis,lang,langc,desc,star,t)=>`<div class="row" style="gap:14px;align-items:flex-start">
  ${I.code(16,c.ac)}<div style="flex:1"><div class="row" style="gap:10px"><a style="font-weight:600;color:${c.ac}">demo-user/${name}</a>${badge(vis,vis==='public'?'ok':'n')}</div>
  <div class="muted" style="font-size:13px;margin:5px 0 8px">${desc}</div>
  <div class="row mono" style="gap:16px;font-size:12px;color:${c.mu}"><span class="row" style="gap:6px"><i style="width:9px;height:9px;border-radius:50%;background:${langc}"></i>${lang}</span><span class="row" style="gap:5px">${I.star(13,c.mu)}${star}</span><span>updated ${t}</span></div></div>
</div>`
const act=(ic,txt,t)=>`<div class="row" style="gap:11px;font-size:13px">${I[ic](15,c.mu)}<span style="flex:1">${txt}</span><span class="mono dim" style="font-size:11px">${t}</span></div>`
const inner=shell(dashSide('home'),`
<div class="row" style="justify-content:space-between;margin-bottom:22px">
  <div><div class="h1big">Dashboard</div><div class="muted" style="font-size:13px;margin-top:3px">Welcome back, demo-user</div></div>
  ${btn('New repository','p',I.plus(15,c.btx))}
</div>
<div class="grid3" style="margin-bottom:22px">
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.1em">Repositories</div><div class="h1big" style="margin-top:8px">12</div>`,18)}
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.1em">Open MRs</div><div class="h1big" style="margin-top:8px;color:${c.ac}">3</div>`,18)}
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.1em">Storage used</div><div class="h1big" style="margin-top:8px">6.2<span style="font-size:14px;color:${c.mu}"> / 20 GB</span></div>${meter(31)}`,18)}
</div>
<div class="grid2">
  <div>
    <div class="row" style="justify-content:space-between;margin-bottom:12px"><div style="font-weight:600">Your repositories</div><a class="mono" style="color:${c.ac};font-size:12px">view all →</a></div>
    ${card(`<div class="list">
      ${repo('hello-world','public','TypeScript','#38e0e6','A minimal starter repository with CI wired up.','24','2h ago')}
      ${repo('storage-node','private','Rust','#f5b350','Encrypted quorum-replicated object storage daemon.','8','yesterday')}
      ${repo('ogb-web','private','Vue','#7ee787','The OpenGitBase web frontend — Nuxt 4 + UI.','15','3d ago')}
    </div>`,18)}
  </div>
  <div>
    <div style="font-weight:600;margin-bottom:12px">Recent activity</div>
    ${card(`<div class="list">
      ${act('merge','Opened <b>!7</b> in hello-world','2h')}
      ${act('pipe','Pipeline <b>#42</b> passed on main','2h')}
      ${act('chat','Commented on <b>#12</b>','5h')}
      ${act('star','Starred rust-lang/mdBook','1d')}
      ${act('code','Pushed 3 commits to storage-node','1d')}
    </div>`,18)}
  </div>
</div>`)
add('Dashboard',inner)
}

/* ============================================================ 3 · REPO OVERVIEW */
{
const inner=shell(repoSide('code'),`
<div class="row" style="justify-content:space-between;align-items:flex-start;margin-bottom:18px">
  <div><div class="row" style="gap:11px"><span class="h1big">Hello World</span>${badge('public','ok')}</div>
  <div class="muted" style="font-size:13px;margin-top:6px">A minimal starter repository with CI wired up. Fork it, push, watch it build.</div></div>
  <div class="row" style="gap:10px">${btn('Star · 24','g',I.star(14))}${btn('Fork · 3','g',I.fork(14))}${btn('Clone','p',I.code(14,c.btx))}</div>
</div>
${card(`<div class="row" style="gap:14px">
  ${badge('main ▾','n')}<span class="mono" style="color:${c.ac};font-size:12.5px">abc123f</span><span class="muted mono" style="font-size:12px">Fix quorum replication for large repos</span>
  <span style="flex:1"></span><span class="muted mono" style="font-size:12px">2 branches · 3 tags · 128 commits</span>
</div>`,14)}
<div style="height:14px"></div>
<div class="grid2">
  <div>
    ${card(`<div class="row" style="justify-content:space-between;padding:12px 16px;border-bottom:1px solid ${c.bd}"><span class="mono" style="font-size:12.5px;color:${c.mu}">${I.folder(14,c.mu)} 14 files</span><span class="mono dim" style="font-size:12px">last commit 2h ago</span></div>
      ${['src','tests','.github/workflows','README.md','ogb.yaml','LICENSE'].map((f,i)=>`<div class="row" style="gap:11px;padding:10px 16px;border-bottom:1px solid ${c.bd};font-size:13px">${(i<3?I.folder(15,c.ac):I.file(15,c.mu))}<span class="mono" style="color:${i<3?c.tx:c.mu}">${f}</span><span style="flex:1"></span><span class="muted mono" style="font-size:11.5px">${['add CI matrix','cover heal path','pin runner image','tidy intro','bump quota','—'][i]}</span><span class="dim mono" style="font-size:11.5px;width:52px;text-align:right">${['2h','1d','1d','3d','3d','5d'][i]}</span></div>`).join('')}
      <div style="padding:16px"><div class="mono" style="font-size:11px;color:${c.mu};margin-bottom:10px">${I.book(13,c.mu)} README.md</div><div style="font-size:22px;font-weight:700;margin-bottom:6px"># Hello World</div><div class="muted" style="font-size:13.5px">A minimal starter for OpenGitBase. Push to <span class="mono" style="color:${c.ac}">main</span> and the pipeline builds, tests, and publishes an encrypted artifact to your storage fleet.</div></div>`,0)}
  </div>
  <div>
    ${card(`<div style="font-weight:600;margin-bottom:12px;font-size:13px">Latest pipeline</div>
      <div class="row" style="gap:10px;margin-bottom:12px">${I.pipe(16,c.ok)}<span class="mono" style="font-size:12.5px">#42 · build, test</span><span style="flex:1"></span>${badge('passed','ok')}</div>
      <div class="mono dim" style="font-size:11.5px">refs/heads/main · ogb-hosted · 1m 58s</div>`,16)}
    <div style="height:14px"></div>
    ${card(`<div style="font-weight:600;margin-bottom:12px;font-size:13px">Storage</div><div class="row mono" style="justify-content:space-between;font-size:12px;margin-bottom:8px"><span class="muted">encrypted artifacts</span><span style="color:${c.ac}">500M / 1.0G</span></div>${meter(49)}<div class="mono dim" style="font-size:11px;margin-top:10px">3 nodes · quorum ok</div>`,16)}
    <div style="height:14px"></div>
    ${card(`<div style="font-weight:600;margin-bottom:12px;font-size:13px">About</div>
      <div class="muted" style="font-size:13px;margin-bottom:12px">Languages</div>
      <div class="meter" style="display:flex;height:8px"><i style="width:64%;background:${c.ac}"></i><i style="width:26%;background:${c.amber}"></i><i style="width:10%;background:${c.ok}"></i></div>
      <div class="row mono" style="gap:14px;font-size:11.5px;margin-top:10px;color:${c.mu}"><span>TS 64%</span><span>Shell 26%</span><span>Dockerfile 10%</span></div>`,16)}
  </div>
</div>`)
add('RepoOverview',inner)
}

/* ============================================================ 4 · REPO FILES (blob) */
{
const tree=(f,ic,on=false)=>`<div class="row" style="gap:9px;padding:6px 10px;border-radius:6px;font-size:12.5px;${on?`background:${c.sf2}`:''}"><span style="color:${on?c.ac:c.mu}">${ic}</span><span class="mono" style="color:${on?c.tx:c.mu}">${f}</span></div>`
const L=(n,html)=>`<span class="codeln"><span class="ln">${n}</span>${html}</span>`
const inner=shell(repoSide('code'),`
<div class="tabs">
  <span class="tab on">${I.code(14,c.ac)} Code</span><span class="tab">${I.chat(14,c.mu)} Discussions <span class="dim">12</span></span><span class="tab">${I.merge(14,c.mu)} Merge requests <span class="dim">3</span></span><span class="tab">${I.pipe(14,c.mu)} Pipelines</span>
</div>
<div class="row" style="gap:14px;margin-bottom:16px">${badge('main ▾','n')}<span class="mono muted" style="font-size:12.5px">demo-user / hello-world / <span style="color:${c.tx}">src / heal.ts</span></span><span style="flex:1"></span>${btn('History','g',I.clock(14))}${btn('Raw','g')}</div>
<div style="display:grid;grid-template-columns:240px 1fr;gap:18px">
  ${card(`<div style="padding:8px">${tree('src','',false)}<div style="padding-left:14px">${tree('heal.ts','',true)}${tree('replicate.ts','')}${tree('index.ts','')}</div>${tree('tests','')}${tree('README.md','')}${tree('ogb.yaml','')}</div>`,0)}
  ${card(`<div class="row" style="justify-content:space-between;padding:11px 16px;border-bottom:1px solid ${c.bd}"><span class="mono" style="font-size:12px;color:${c.mu}">${I.file(14,c.mu)} src/heal.ts · <span style="color:${c.tx}">2.4 KB</span></span><span class="mono dim" style="font-size:11.5px">42 lines · abc123f</span></div>
    <div style="padding:14px 6px 16px">
    ${L('1',`<span class="kw">import</span> { quorum } <span class="kw">from</span> <span class="str">'./replicate'</span>`)}
    ${L('2',`<span class="cm">// re-heal repos whose on-disk artifacts drifted</span>`)}
    ${L('3',`<span class="kw">export async function</span> <span class="fn">healAll</span>(nodes: Node[]) {`)}
    ${L('4',`  <span class="kw">for</span> (<span class="kw">const</span> n <span class="kw">of</span> nodes) {`)}
    ${L('5',`    <span class="kw">const</span> drift = <span class="kw">await</span> <span class="fn">scan</span>(n)`)}
    ${L('6',`    <span class="kw">if</span> (drift.length) <span class="kw">await</span> <span class="fn">quorum</span>.<span class="fn">repair</span>(n, drift)`)}
    ${L('7',`  }`)}
    ${L('8',`}`)}
    </div>`,0)}
</div>`)
add('RepoFiles',inner)
}

/* ============================================================ 5 · MERGE REQUEST DETAIL */
{
const diff=(sign,txt)=>{const col=sign==='+'?c.ok:sign==='-'?c.red:c.mu;const bg=sign==='+'?'rgba(126,231,135,.06)':sign==='-'?'rgba(255,107,107,.06)':'transparent';return`<div class="codeln" style="background:${bg};padding:0 12px"><span class="ln">${sign===' '?'':''}</span><span style="color:${col};width:14px;display:inline-block">${sign}</span><span style="color:${sign==='-'?c.mu:c.tx}">${txt}</span></div>`}
const cmt=(who,seed,t,body)=>`<div class="card" style="padding:0;margin-bottom:14px"><div class="row" style="gap:10px;padding:11px 16px;border-bottom:1px solid ${c.bd};background:${c.sf2}">${av(seed,24)}<span style="font-weight:600;font-size:13px">${who}</span><span class="mono dim" style="font-size:11.5px">${t}</span></div><div style="padding:14px 16px;font-size:13.5px" class="muted">${body}</div></div>`
const inner=shell(repoSide('merge'),`
<div class="row" style="gap:11px;margin-bottom:6px">${badge('Open','ac')}<span class="mono dim" style="font-size:12px">!7</span></div>
<div class="row" style="justify-content:space-between;align-items:flex-start;margin-bottom:16px">
  <div style="max-width:70%"><div class="h1big">Refactor branch policy editor</div>
  <div class="mono muted" style="font-size:12.5px;margin-top:6px">${av('#0e3b40',18)} demo-user wants to merge <span style="color:${c.ac}">feature/branch-rules</span> into <span style="color:${c.ac}">main</span></div></div>
  ${btn('Merge','p',I.merge(15,c.btx))}
</div>
<div class="tabs">
  <span class="tab on">Conversation <span class="dim">4</span></span><span class="tab">Commits <span class="dim">6</span></span><span class="tab">Changes <span class="dim">8</span></span><span class="tab">Pipelines</span>
</div>
<div class="grid2">
  <div>
    ${cmt('demo-user','#0e3b40','opened 2h ago','Splits the monolithic policy form into composable rule rows and adds validation. Ready for review — CI is green.')}
    ${card(`<div class="row" style="justify-content:space-between;padding:11px 16px;border-bottom:1px solid ${c.bd}"><span class="mono" style="font-size:12px;color:${c.mu}">${I.file(14,c.mu)} app/policy/Editor.vue</span><span class="mono" style="font-size:11.5px"><span style="color:${c.ok}">+18</span> <span style="color:${c.red}">−7</span></span></div>
      <div style="padding:8px 0">
      ${diff(' ','const rules = ref(props.initial)')}
      ${diff('-','function add() { rules.value.push({}) }')}
      ${diff('+','function add(kind: RuleKind) {')}
      ${diff('+','  rules.value.push(makeRule(kind))')}
      ${diff('+','}')}
      </div>`,0)}
    <div style="height:14px"></div>
    ${cmt('reviewer-bot','#12331d','1h ago','Approved. Nice split — the rule factory keeps this testable. One nit inline on the empty-state copy.')}
  </div>
  <div>
    ${card(`<div style="font-weight:600;font-size:13px;margin-bottom:14px">Merge checks</div>
      <div class="list" style="font-size:13px">
      <div class="row" style="gap:10px">${I.check(15,c.ok)}<span>2 approvals</span></div>
      <div class="row" style="gap:10px">${I.check(15,c.ok)}<span>Pipeline #48 passed</span></div>
      <div class="row" style="gap:10px">${I.check(15,c.ok)}<span>No conflicts with main</span></div>
      <div class="row" style="gap:10px">${I.shield(15,c.ac)}<span>Branch protection satisfied</span></div>
      </div>`,16)}
    <div style="height:14px"></div>
    ${card(`<div style="font-weight:600;font-size:13px;margin-bottom:12px">Reviewers</div>
      <div class="row" style="gap:10px;margin-bottom:9px">${av('#12331d',22)}<span style="font-size:13px">reviewer-bot</span>${badge('approved','ok')}</div>
      <div class="row" style="gap:10px">${av('#3a2c12',22)}<span style="font-size:13px">maintainer</span>${badge('pending','amber')}</div>`,16)}
  </div>
</div>`)
add('MergeRequest',inner)
}

/* ============================================================ 6 · PIPELINE / CI RUN */
{
const stage=(name,state,t)=>{const col=state==='passed'?c.ok:state==='running'?c.ac:state==='failed'?c.red:c.dim;const glow=state==='running'?`box-shadow:0 0 10px ${c.ac}`:'';return`<div class="row" style="gap:12px;padding:12px 16px;border:1px solid ${c.bd};border-radius:10px;background:${c.sf}">
  <span style="width:10px;height:10px;border-radius:50%;background:${col};${glow}"></span>
  <span class="mono" style="font-size:13px;color:${state==='pending'?c.mu:c.tx}">${name}</span><span style="flex:1"></span>
  <span class="mono dim" style="font-size:11.5px">${t}</span>${badge(state,state==='passed'?'ok':state==='running'?'ac':state==='failed'?'red':'n')}</div>`}
const logl=(html)=>`<div class="codeln" style="padding:0 4px;color:${c.mu}"><span style="color:${c.dim}">›</span> ${html}</div>`
const inner=shell(repoSide('pipe'),`
<div class="row" style="justify-content:space-between;align-items:flex-start;margin-bottom:18px">
  <div><div class="row" style="gap:11px"><span class="h1big">Pipeline #42</span>${badge('running','ac')}</div>
  <div class="mono muted" style="font-size:12.5px;margin-top:6px">refs/heads/main · <span style="color:${c.ac}">abc123f</span> · triggered by push · ogb-hosted</div></div>
  ${btn('Cancel run','g')}
</div>
<div class="grid3" style="margin-bottom:20px">
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.08em">Elapsed</div><div class="h1big mono" style="margin-top:8px;color:${c.ac}">0:41</div>`,16)}
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.08em">Stages</div><div class="h1big" style="margin-top:8px">2<span style="color:${c.mu};font-size:14px"> / 3</span></div>`,16)}
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.08em">Runner</div><div class="mono" style="margin-top:10px;font-size:13px">${I.cpu(14,c.ac)} microVM · 4 vCPU</div>`,16)}
</div>
<div class="grid2">
  <div style="display:flex;flex-direction:column;gap:10px">
    ${stage('checkout','passed','4s')}
    ${stage('build','passed','1m 12s')}
    ${stage('test','running','0:41…')}
    ${stage('publish','pending','—')}
  </div>
  <div>
    ${card(`<div class="row" style="justify-content:space-between;padding:11px 16px;border-bottom:1px solid ${c.bd}"><span class="mono" style="font-size:12px;color:${c.mu}">${I.pipe(14,c.mu)} test · log</span><span class="mono" style="font-size:11.5px;color:${c.ac}">live</span></div>
    <div style="padding:12px 12px 14px;font-size:12px">
    ${logl('spawning microVM <span style="color:'+c.tx+'">ogb-hosted-3</span>')}
    ${logl('$ npm ci <span style="color:'+c.ok+'">✓</span>')}
    ${logl('$ npm test')}
    ${logl('PASS src/heal.test.ts')}
    ${logl('PASS src/replicate.test.ts')}
    ${logl('<span style="color:'+c.ac+'">128 passed</span> · <span style="color:'+c.mu+'">2 running</span>')}
    <div class="row" style="gap:8px;margin-top:6px"><span class="mono" style="color:'+c.ac+'">▍</span></div>
    </div>`,0)}
  </div>
</div>`)
add('Pipeline',inner)
}

/* ============================================================ 7 · DISCUSSION DETAIL */
{
const cmt=(who,seed,t,body,op=false)=>`<div class="card" style="padding:0;margin-bottom:14px"><div class="row" style="gap:10px;padding:11px 16px;border-bottom:1px solid ${c.bd};background:${c.sf2}">${av(seed,24)}<span style="font-weight:600;font-size:13px">${who}</span>${op?badge('author','ac'):''}<span style="flex:1"></span><span class="mono dim" style="font-size:11.5px">${t}</span></div><div style="padding:14px 16px;font-size:13.5px" class="muted">${body}</div></div>`
const inner=shell(repoSide('chat'),`
<div class="row" style="gap:11px;margin-bottom:6px">${badge('Open','ok')}<span class="mono dim" style="font-size:12px">#12</span></div>
<div class="h1big" style="margin-bottom:6px">Reconciler trusts DB watermark on artifact loss</div>
<div class="mono muted" style="font-size:12.5px;margin-bottom:20px">${av('#0e3b40',18)} opened by demo-user · 5h ago · 4 comments</div>
<div class="grid2">
  <div>
    ${cmt('demo-user','#0e3b40','5h ago','When on-disk encrypted artifacts are lost but the DB watermark is intact, the reconciler reports the repo as healthy and never re-heals. Repro + logs attached.',true)}
    ${cmt('maintainer','#3a2c12','3h ago','Confirmed. The watermark check short-circuits before the on-disk scan. We should verify artifact presence, not just the watermark, in the healthy path.')}
    ${cmt('demo-user','#0e3b40','1h ago','Agreed — I will add a presence probe to <span class="mono" style="color:'+c.ac+'">healAll()</span> and open an MR.')}
    ${card(`<div class="mono dim" style="font-size:12px;margin-bottom:10px">${I.chat(14,c.mu)} write a comment</div><div style="height:60px;border:1px solid ${c.bd};border-radius:8px;background:${c.bg}"></div><div class="row" style="justify-content:flex-end;margin-top:12px">${btn('Comment','p')}</div>`,16)}
  </div>
  <div>
    ${card(`<div style="font-weight:600;font-size:13px;margin-bottom:12px">Details</div>
    <div class="list" style="font-size:13px">
    <div class="row" style="justify-content:space-between"><span class="muted">Assignee</span><span class="row" style="gap:7px">${av('#3a2c12',20)}maintainer</span></div>
    <div class="row" style="justify-content:space-between"><span class="muted">Labels</span><span class="row" style="gap:6px">${badge('bug','red')}${badge('storage','ac')}</span></div>
    <div class="row" style="justify-content:space-between"><span class="muted">Milestone</span><span class="mono" style="font-size:12px">v0.4 hardening</span></div>
    <div class="row" style="justify-content:space-between"><span class="muted">Linked MR</span><span class="mono" style="color:${c.ac};font-size:12px">!8</span></div>
    </div>`,16)}
  </div>
</div>`)
add('Discussion',inner)
}

/* ============================================================ 8 · ADMIN — STORAGE FLEET */
{
const node=(id,region,cap,used,state)=>{const col=state==='healthy'?c.ok:state==='degraded'?c.amber:c.red;return`<div class="row" style="gap:14px;padding:13px 16px;border-top:1px solid ${c.bd};font-size:13px">
  <span class="mono" style="width:110px;color:${c.tx}">${id}</span>
  <span class="mono muted" style="width:90px;font-size:12px">${region}</span>
  <div style="flex:1;max-width:220px">${meter(used,used>85?c.amber:c.ac)}<div class="mono dim" style="font-size:11px;margin-top:5px">${(cap*used/100).toFixed(0)}G / ${cap}G · ${used}%</div></div>
  <span style="flex:1"></span>
  <span class="row" style="gap:7px">${I.dot(10,col)}${badge(state,state==='healthy'?'ok':state==='degraded'?'amber':'red')}</span>
</div>`}
const inner=shell(adminSide('server'),`
<div class="row" style="justify-content:space-between;align-items:flex-start;margin-bottom:22px">
  <div><div class="h1big">Storage fleet</div><div class="muted" style="font-size:13px;margin-top:3px">6 nodes · quorum replication · encrypted at rest</div></div>
  <div class="row" style="gap:10px">${btn('Run heal','g',I.check(14))}${btn('Add node','p',I.plus(14,c.btx))}</div>
</div>
<div class="grid3" style="margin-bottom:22px">
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.08em">Healthy nodes</div><div class="h1big" style="margin-top:8px;color:${c.ok}">5<span style="color:${c.mu};font-size:14px"> / 6</span></div>`,18)}
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.08em">Capacity</div><div class="h1big" style="margin-top:8px">4.1<span style="color:${c.mu};font-size:14px"> / 12 TB</span></div>${meter(34)}`,18)}
  ${card(`<div class="muted mono" style="font-size:11px;text-transform:uppercase;letter-spacing:.08em">Replication</div><div class="h1big mono" style="margin-top:8px;color:${c.ac}">3×</div><div class="mono dim" style="font-size:11px">quorum 2 of 3</div>`,18)}
</div>
${card(`<div class="row" style="padding:12px 16px;font-size:11px;color:${c.mu}" class="mono"><span class="mono" style="width:110px">NODE</span><span class="mono" style="width:90px">REGION</span><span class="mono" style="flex:1;max-width:220px">CAPACITY</span><span style="flex:1"></span><span class="mono">STATUS</span></div>
  ${node('stor-a1','eu-central','2000',42,'healthy')}
  ${node('stor-a2','eu-central','2000',49,'healthy')}
  ${node('stor-b1','eu-west','2000',88,'degraded')}
  ${node('stor-b2','eu-west','2000',31,'healthy')}
  ${node('stor-c1','us-east','2000',27,'healthy')}
  ${node('stor-c2','us-east','2000',0,'offline')}`,0)}
<div style="height:14px"></div>
${card(`<div class="row" style="gap:11px">${I.shield(16,c.amber)}<div style="flex:1"><div style="font-weight:600;font-size:13px">1 node degraded — <span class="mono" style="color:${c.amber}">stor-b1</span> at 88%</div><div class="muted" style="font-size:12.5px;margin-top:3px">Reconciler verified artifact presence across quorum; no data loss. Consider adding capacity in eu-west.</div></div>${btn('Rebalance','g')}</div>`,16)}`)
add('AdminStorage',inner)
}

/* ============================================================ 9 · REPO SETTINGS */
{
const field=(label,val,hint)=>`<div style="margin-bottom:18px"><div class="mono" style="font-size:12px;color:${c.mu};margin-bottom:7px">${label}</div><div style="border:1px solid ${c.bd};border-radius:8px;background:${c.bg};padding:11px 13px;font-size:13px;font-family:'JetBrains Mono',monospace">${val}</div>${hint?`<div class="dim" style="font-size:11.5px;margin-top:6px">${hint}</div>`:''}</div>`
const toggle=(on)=>`<span style="width:38px;height:22px;border-radius:12px;background:${on?c.ac:c.bd2};display:inline-flex;align-items:center;padding:2px;${on?'justify-content:flex-end':''}"><span style="width:18px;height:18px;border-radius:50%;background:${on?c.btx:c.mu}"></span></span>`
const rule=(t,d,on)=>`<div class="row" style="gap:14px;padding:14px 0;border-top:1px solid ${c.bd}"><div style="flex:1"><div style="font-weight:500;font-size:13.5px">${t}</div><div class="muted" style="font-size:12.5px;margin-top:2px">${d}</div></div>${toggle(on)}</div>`
const inner=shell(repoSide('gear'),`
<div class="h1big" style="margin-bottom:4px">Settings</div>
<div class="muted" style="font-size:13px;margin-bottom:22px">demo-user / hello-world</div>
<div style="display:grid;grid-template-columns:200px 1fr;gap:28px">
  <div class="mono" style="font-size:13px;display:flex;flex-direction:column;gap:4px">
    <span style="padding:8px 11px;border-radius:7px;background:${c.sf2};color:${c.ac};border:1px solid ${c.bd}">General</span>
    <span style="padding:8px 11px;color:${c.mu}">Branch protection</span>
    <span style="padding:8px 11px;color:${c.mu}">Access &amp; members</span>
    <span style="padding:8px 11px;color:${c.mu}">Webhooks</span>
    <span style="padding:8px 11px;color:${c.mu}">Storage</span>
  </div>
  <div>
    ${card(`<div style="font-weight:600;margin-bottom:16px">General</div>${field('Repository name','hello-world')}${field('Description','A minimal starter repository with CI wired up.')}${field('Default branch','main','Used as the base for new merge requests and pipelines.')}<div class="row" style="justify-content:flex-end">${btn('Save changes','p')}</div>`,20)}
    <div style="height:16px"></div>
    ${card(`<div style="font-weight:600;margin-bottom:4px">Branch protection · <span class="mono" style="color:${c.ac};font-size:13px">main</span></div>
      ${rule('Require merge request','Direct pushes to main are blocked.',true)}
      ${rule('Require passing pipeline','Merges wait for CI to go green.',true)}
      ${rule('Require 2 approvals','At least two reviewers must approve.',true)}
      ${rule('Allow force pushes','Rewriting history on main.',false)}`,20)}
    <div style="height:16px"></div>
    ${card(`<div style="font-weight:600;color:${c.red};margin-bottom:6px">Danger zone</div><div class="row" style="justify-content:space-between"><div class="muted" style="font-size:13px">Delete this repository and all its artifacts. This cannot be undone.</div>${btn('Delete repository','d')}</div>`,20)}
  </div>
</div>`)
add('Settings',inner)
}

/* ============================================================ COVER / INDEX */
{
const cover=`<div class="cwrap">
<div class="mono k">Terminal · Ice · applied</div>
<h1 class="ctitle">The Ice direction, across the app</h1>
<p class="clede">Your pick — Terminal with the Ice cyan accent <span class="mono" style="color:${c.ac}">#38e0e6</span> — applied to nine real screens in full app chrome: marketing, dashboard, repository, code, merge request, CI, discussions, admin, and settings. This is the look end to end, before any code changes. Approve it, or point at anything to adjust.</p>
<div class="cgrid">
${[['Home','marketing / masthead'],['Dashboard','logged-in home'],['RepoOverview','repository code tab'],['RepoFiles','code browser + blob'],['MergeRequest','review + diff + checks'],['Pipeline','live CI run + logs'],['Discussion','issue thread'],['AdminStorage','storage fleet ops'],['Settings','config + danger zone']].map(([n,d],i)=>`<div class="cchip"><span class="mono" style="color:${c.ac};font-size:12px">${String(i+1).padStart(2,'0')}</span><div><div style="font-weight:600;font-size:13px">${n}</div><div class="muted mono" style="font-size:11px">${d}</div></div></div>`).join('')}
</div>
<div class="mono dim" style="margin-top:30px;font-size:12.5px">→ Nine boards beside this one. Accent lives in one token — say the word to nudge the hue, spacing, or any screen.</div>
</div>`
const css=CSS+`
.cwrap{padding:52px 56px;background-image:radial-gradient(${c.bd} 1px,transparent 1px);background-size:26px 26px;min-height:100%}
.k{font-size:12px;letter-spacing:.15em;text-transform:uppercase;color:${c.mu}}
.ctitle{font-size:40px;font-weight:700;letter-spacing:-.02em;margin:12px 0 14px}
.clede{font-size:17px;color:${c.mu};max-width:780px;margin:0 0 34px}
.cgrid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}
.cchip{display:flex;gap:13px;align-items:center;border:1px solid ${c.bd};border-radius:12px;background:${c.sf};padding:16px 18px}`
writeFileSync(OUT+'Main.dc.html',dc(cover,css,1180))
}

/* ---- canvas.json: cover on top, screens in rows of 3 ---- */
const XSTEP=1460, YGAP=180
const artboards=[{file:'Main.dc.html',x:0,y:0,w:1180,h:760}]
let y=940
files.forEach((f,i)=>{
  const col=i%3, row=Math.floor(i/3)
  artboards.push({file:f+'.dc.html',x:col*XSTEP,y:y+row*(1600+YGAP),w:1360,h:1600})
})
writeFileSync(OUT+'canvas.json',JSON.stringify({artboards,launch:{view:'canvas'}},null,2))
console.log('wrote Ice cover +',files.length,'screens:',files.join(', '))
