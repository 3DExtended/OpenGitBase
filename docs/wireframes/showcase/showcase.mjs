import { writeFileSync } from 'node:fs'
const OUT = new URL('./', import.meta.url).pathname

/* tokens lifted from applications/opengitbase-web/app/assets/main.css */
const c = {
  bg:'#fafafa', sf:'#ffffff', bd:'#e4e4e7', bd2:'#d4d4d8',
  tx:'#18181b', mu:'#71717a', fa:'#a1a1aa',
  ac:'#0d9488', ac5:'#14b8a6', acH:'#0f766e', softBg:'#ccfbf1', softTx:'#0f766e',
  okBg:'#dcfce7', okTx:'#15803d', nBg:'#f4f4f5', nTx:'#52525b',
  wBg:'#fef9c3', wTx:'#a16207', opBg:'#e0e7ff', opTx:'#4338ca', eBg:'#fee2e2', eTx:'#dc2626',
}
const S=(p,s=18,sw=2)=>`<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" style="flex:none">${p}</svg>`
const IC={
  logo:(s=22,col=c.ac)=>`<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="none" stroke="${col}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="flex:none"><circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="9" r="2.4"/><path d="M6 8.4v7.2"/><path d="M18 11.4c0 3-2.5 3.6-5.5 4.2C10 16 8.4 16.6 8.4 18"/></svg>`,
  code:(s=18)=>S(`<path d="M16 18l4-6-4-6M8 6l-4 6 4 6"/>`,s),
  chat:(s=18)=>S(`<path d="M21 15a2 2 0 0 1-2 2H8l-4 3V6a2 2 0 0 1 2-2h13a2 2 0 0 1 2 2z"/>`,s),
  merge:(s=18)=>S(`<circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="15" r="2.4"/><path d="M6 8.4v7.2M6 9a6 6 0 0 0 6 6h3.6"/>`,s),
  pipe:(s=18)=>S(`<rect x="3" y="4" width="7" height="7" rx="1.5"/><rect x="14" y="13" width="7" height="7" rx="1.5"/><path d="M6.5 11v3a3 3 0 0 0 3 3H14"/>`,s),
  server:(s=18)=>S(`<rect x="3" y="4" width="18" height="7" rx="1.5"/><rect x="3" y="13" width="18" height="7" rx="1.5"/><path d="M7 7.5h.01M7 16.5h.01"/>`,s),
  cpu:(s=18)=>S(`<rect x="6" y="6" width="12" height="12" rx="2"/><path d="M9 1v3M15 1v3M9 20v3M15 20v3M1 9h3M1 15h3M20 9h3M20 15h3"/><rect x="10" y="10" width="4" height="4"/>`,s),
  activity:(s=18)=>S(`<path d="M3 12h4l3 8 4-16 3 8h4"/>`,s),
  shield:(s=18)=>S(`<path d="M12 2l8 3v6c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V5z"/>`,s),
  bell:(s=18)=>S(`<path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>`,s),
  sun:(s=18)=>S(`<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4 12H2M22 12h-2M5 5l1.4 1.4M17.6 17.6L19 19M19 5l-1.4 1.4M6.4 17.6L5 19"/>`,s),
  chev:(s=15)=>S(`<path d="M6 9l6 6 6-6"/>`,s),
  plus:(s=16)=>S(`<path d="M12 5v14M5 12h14"/>`,s),
  grid:(s=16)=>S(`<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>`,s),
  layers:(s=18)=>S(`<path d="M12 2l9 5-9 5-9-5z"/><path d="M3 12l9 5 9-5M3 17l9 5 9-5"/>`,s),
  search:(s=15)=>S(`<circle cx="11" cy="11" r="7"/><path d="M21 21l-4-4"/>`,s),
  users:(s=18)=>S(`<path d="M16 20v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="8" r="3.2"/><path d="M22 20v-2a4 4 0 0 0-3-3.8"/>`,s),
}
const bd=(t,k='n')=>{const m={ok:[c.okBg,c.okTx],n:[c.nBg,c.nTx],w:[c.wBg,c.wTx],open:[c.opBg,c.opTx],e:[c.eBg,c.eTx]}[k];return `<span class="bd" style="background:${m[0]};color:${m[1]}">${t}</span>`}
const btn=(t,v='pri',ic='')=>`<span class="btn ${v}">${ic}${t}</span>`

/* --- mini app chrome (internal width 1200) for browser-frame thumbnails --- */
const mtop=(loggedIn=true,active='Home')=>`<div class="mtop">${loggedIn?`<span style="color:${c.mu}">${IC.grid(16)}</span>`:''}<span class="mbrand">${IC.logo(20)} OpenGitBase</span><span class="mnav">${['Home','Explore','Docs','Community','System status'].map(n=>`<span class="${n===active?'on':''}">${n}</span>`).join('')}</span><span style="flex:1"></span><span style="display:flex;gap:14px;color:${c.mu}">${IC.sun(17)}${loggedIn?IC.bell(17)+`<span style="color:${c.tx};display:flex;gap:5px;align-items:center">demo-user ${IC.chev(14)}</span>`:'<span>Sign in</span>'}</span></div>`
const mni=(icon,label,on=false)=>`<div class="mni ${on?'on':''}">${icon}<span>${label}</span></div>`

const miniDashboard=`<div style="width:1200px;background:${c.bg}">
${mtop(true)}
<div style="display:flex">
<aside class="mside"><div class="mbtn out" style="justify-content:space-between">${IC.plus(15)}<span style="flex:1;margin-left:4px">Create</span>${IC.chev(14)}</div><div class="mcap">Organizations</div>${mni(IC.grid(15),'Acme Corp')}<div class="mcap">Repositories</div>${mni(IC.logo(15),'Private Notes')}${mni(IC.logo(15),'Hello World')}</aside>
<div class="mmain">
<div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:22px"><div><div class="mh1">Welcome, demo-user</div><div class="msub">Your repositories and organizations at a glance.</div></div><div style="display:flex;gap:10px">${btn('New repository','pri',IC.plus(15)+' ')}${btn('New organization','soft',IC.grid(15)+' ')}</div></div>
<div class="mh2">Organizations</div><div style="margin:10px 0 22px">${bd('Acme Corp','open')}</div>
<div class="mh2" style="margin-bottom:12px">Repositories</div>
<div style="display:grid;grid-template-columns:1fr 1fr;gap:18px">
<div class="mcard"><div style="display:flex;justify-content:space-between"><b style="font-size:16px">Hello World</b>${bd('Public','ok')}</div><div class="mono" style="color:${c.mu};margin:8px 0 14px">demo-user/hello-world</div><div style="color:${c.mu}">Updated 3 months ago</div></div>
<div class="mcard"><div style="display:flex;justify-content:space-between"><b style="font-size:16px">Private Notes</b>${bd('Private','n')}</div><div class="mono" style="color:${c.mu};margin:8px 0 14px">demo-user/private-notes</div><div style="color:${c.mu}">Updated 3 months ago</div></div>
</div></div></div></div>`

const miniMR=`<div style="width:1200px;background:${c.bg}">
${mtop(true,'')}
<div style="display:flex">
<aside class="mside"><div class="mono" style="color:${c.mu};font-size:12px;padding:2px 8px 8px">demo-user/hello-world</div>${mni(IC.code(15),'Code')}${mni(IC.chat(15),'Discussions')}${mni(IC.merge(15),'Merge requests',true)}${mni(IC.pipe(15),'Pipelines')}</aside>
<div class="mmain">
<div class="mono" style="color:${c.mu}">!7 · demo-user/hello-world</div>
<div class="mh1" style="margin:6px 0 10px">Refactor branch policy editor</div>
<div style="display:flex;gap:10px;align-items:center;margin-bottom:18px">${bd('Open','open')}<span class="mono" style="color:${c.mu}">feature/branch-rules → main</span></div>
<div style="display:flex;gap:24px">
<div style="flex:1">
<div class="mtabs"><span class="mtab on">Overview</span><span class="mtab">Changes</span><span class="mtab">Commits</span></div>
<div class="mcard" style="margin-bottom:18px">This merge request adds reusable policy controls.</div>
<div class="mcard"><div style="font-weight:600;margin-bottom:14px">Overview comments</div><div style="border:1px solid ${c.bd};border-radius:10px;padding:14px"><div style="display:flex;justify-content:space-between"><b>reviewer</b><span style="color:${c.mu}">2 weeks ago</span></div><div style="margin-top:8px">Looks good overall. One nit in changes tab.</div></div></div>
</div>
<aside style="width:300px;flex:none"><div class="mcard"><div style="font-weight:600;margin-bottom:14px">Linked discussions</div><div style="display:flex;gap:8px;margin-bottom:6px">${bd('Closes','ok')}</div><div style="border:1px solid ${c.bd};border-radius:10px;padding:12px"><b>#12 Protect default branch</b><div style="color:${c.mu};margin-top:2px">Open</div></div></div></aside>
</div></div></div></div>`

const miniStatus=`<div style="width:1200px;background:${c.bg}">
${mtop(false,'System status')}
<div style="padding:30px 90px">
<div class="mh1" style="font-size:28px">System status</div><div class="msub" style="margin-bottom:22px">Live health for website, API, Git, storage, data stores, and message bus.</div>
<div class="mcard" style="display:flex;justify-content:space-between;align-items:center"><div><div style="color:${c.mu};margin-bottom:8px">Overall status</div>${bd('Healthy','ok')}</div><div style="color:${c.mu}">Last updated 7/10/2026, 2:00 PM</div></div>
${[['Website','Healthy','ok'],['API','Healthy','ok'],['Git','Degraded','w'],['Storage','Healthy','ok']].map(([n,l,k])=>`<div class="mcard" style="display:flex;justify-content:space-between;align-items:center;padding:16px 20px"><div style="display:flex;gap:12px;align-items:center">${IC.chev(15)}<b>${n}</b></div>${bd(l,k)}</div>`).join('')}
<div class="mcard"><div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px"><div style="font-weight:600">90-day uptime</div><b>Latest daily uptime: 97.2%</b></div><div style="height:70px;border-left:1px solid ${c.bd};border-bottom:1px solid ${c.bd};position:relative"><div style="position:absolute;left:0;right:0;top:6px;height:3px;background:${c.ac};border-radius:3px"></div></div></div>
</div></div>`

const win=(label,inner,h=310)=>`<div class="win"><div class="winbar"><span class="dot" style="background:#f87171"></span><span class="dot" style="background:#fbbf24"></span><span class="dot" style="background:#34d399"></span><div style="flex:1"></div><div class="winurl mono">${label}</div><div style="flex:1"></div></div><div class="winport" style="height:${h}px"><div class="winscale">${inner}</div></div></div>`

const pill=(icon,name,desc)=>`<div class="pillar"><div class="pillicon">${icon}</div><div><div style="font-weight:600;font-size:15px">${name}</div><div style="color:${c.mu};font-size:13px;margin-top:2px">${desc}</div></div></div>`
const swatch=(hex,name)=>`<div style="text-align:center"><div style="width:100%;height:52px;border-radius:8px;border:1px solid ${c.bd};background:${hex}"></div><div class="mono" style="font-size:11px;margin-top:5px">${hex}</div><div style="font-size:11px;color:${c.mu}">${name}</div></div>`

const board = `<div class="wrap">
<!-- masthead -->
<div class="mast">
  <div style="display:flex;align-items:center;gap:16px">
    <div class="masklogo">${IC.logo(40,'#fff')}</div>
    <div><div class="wordmark">OpenGitBase</div><div class="tagline">A community-first Git forge for self-hosting and collaboration.</div></div>
  </div>
  <div style="text-align:right">
    <div style="display:flex;gap:8px;justify-content:flex-end;margin-bottom:10px">${['Self-hosted','Privacy-first','Open source'].map(t=>`<span class="mastpill">${t}</span>`).join('')}</div>
    <div class="mono" style="color:${c.softTx};font-size:12.5px">Nuxt 4 · Nuxt UI · teal / zinc · Inter</div>
  </div>
</div>

<!-- signature screens -->
<div class="seclabel">Signature screens</div>
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:24px;margin-bottom:44px">
  ${win('demo-user — dashboard', miniDashboard)}
  ${win('demo-user/hello-world · !7', miniMR)}
  ${win('opengitbase.dev/status', miniStatus)}
</div>

<!-- interface language -->
<div class="seclabel">The interface language</div>
<div style="display:grid;grid-template-columns:1.1fr 1fr 1.5fr;gap:24px;margin-bottom:44px">
  <div class="panel"><div class="paneltitle">Color</div>
    <div style="display:grid;grid-template-columns:repeat(4,1fr);gap:10px">
      ${swatch(c.ac,'accent-600')}${swatch(c.ac5,'accent-500')}${swatch('#5eead4','accent-300')}${swatch(c.softBg,'accent-100')}
      ${swatch(c.tx,'zinc-900')}${swatch(c.mu,'zinc-500')}${swatch(c.bd,'zinc-200')}${swatch(c.bg,'zinc-50')}
    </div>
    <div style="display:flex;gap:8px;margin-top:16px;flex-wrap:wrap">${bd('Public','ok')}${bd('Open','open')}${bd('Degraded','w')}${bd('Private','n')}${bd('Forbidden','e')}</div>
  </div>
  <div class="panel"><div class="paneltitle">Typography</div>
    <div style="font-size:34px;font-weight:700;letter-spacing:-.01em">Inter</div>
    <div style="color:${c.mu};font-size:13px;margin-bottom:14px">Display &amp; UI · 400 / 500 / 600 / 700</div>
    <div style="font-size:15px;line-height:1.5;border-top:1px solid ${c.bd};padding-top:12px">Merge requests, discussions, and pipelines — the body copy voice.</div>
    <div class="mono" style="font-size:14px;margin-top:14px;background:${c.bg};border:1px solid ${c.bd};border-radius:8px;padding:8px 12px">JetBrains Mono · abc123 · main</div>
  </div>
  <div class="panel"><div class="paneltitle">Components</div>
    <div style="display:flex;gap:10px;flex-wrap:wrap;margin-bottom:14px">${btn('Primary','pri')}${btn('Soft','soft')}${btn('Outline','out')}${btn('Danger','dng')}</div>
    <div style="display:flex;gap:12px;flex-wrap:wrap;align-items:center;margin-bottom:14px">
      <div class="minp">${IC.search(14)}<span style="margin-left:8px;color:${c.fa}">Search repositories…</span></div>
      <div class="mtabs" style="max-width:220px"><span class="mtab on">Branches</span><span class="mtab">Tags</span></div>
    </div>
    <div style="border:1px solid ${c.bd};border-radius:10px;padding:14px;background:#fff">
      <div style="display:flex;justify-content:space-between;font-size:13px;margin-bottom:8px"><span>Storage usage</span><span style="color:${c.mu}">500 MB / 1.00 GB · 49%</span></div>
      <div style="height:8px;border-radius:5px;background:${c.bd};overflow:hidden"><i style="display:block;height:100%;width:49%;background:${c.ac}"></i></div>
    </div>
  </div>
</div>

<!-- feature pillars -->
<div class="seclabel">What the app does</div>
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:20px;margin-bottom:38px">
  ${pill(IC.code(20),'Repositories','Browse code, files, commits and diffs over HTTPS &amp; SSH.')}
  ${pill(IC.merge(20),'Merge requests','Review changes with linked discussions and threaded comments.')}
  ${pill(IC.pipe(20),'CI/CD pipelines','Push-triggered runs in Firecracker MicroVMs on hybrid compute.')}
  ${pill(IC.server(20),'Storage fleet','Self-hosted storage nodes with RF=3 replication &amp; quotas.')}
  ${pill(IC.cpu(20),'Compute &amp; org','Enroll compute nodes; manage members, roles and invites.')}
  ${pill(IC.activity(20),'Status &amp; admin','Live health, outage history, and an operator admin console.')}
</div>

<div class="foot"><span style="display:flex;align-items:center;gap:8px">${IC.logo(18)} OpenGitBase — self-hosted Git forge</span><span class="mono" style="color:${c.mu}">34 screens · 7 areas · one design system</span></div>
<div class="accentline"></div>
</div>`

const CSS=`
*{box-sizing:border-box}
html{height:100%}
body{margin:0;min-height:100%;font-family:'Inter',ui-sans-serif,system-ui,sans-serif;background:${c.bg};color:${c.tx};font-size:14px;line-height:1.45;-webkit-font-smoothing:antialiased}
.mono{font-family:'JetBrains Mono',ui-monospace,monospace}
a{color:${c.ac};text-decoration:none}
.wrap{width:1480px;min-height:100%;background:${c.bg};padding:52px 56px 40px}
.mast{border-radius:20px;padding:38px 44px;background:linear-gradient(120deg,${c.acH} 0%,${c.ac} 45%,${c.ac5} 100%);color:#fff;display:flex;justify-content:space-between;align-items:center;margin-bottom:44px}
.masklogo{width:64px;height:64px;border-radius:16px;background:rgba(255,255,255,.14);display:flex;align-items:center;justify-content:center}
.wordmark{font-size:34px;font-weight:700;letter-spacing:-.02em}
.tagline{font-size:16px;color:rgba(255,255,255,.85);margin-top:2px}
.mastpill{background:rgba(255,255,255,.16);color:#fff;font-size:12.5px;font-weight:500;padding:5px 12px;border-radius:999px}
.seclabel{font-size:12px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:${c.mu};margin-bottom:16px}
/* browser frame */
.win{border:1px solid ${c.bd};border-radius:14px;overflow:hidden;background:#fff;box-shadow:0 6px 20px -8px rgba(24,24,27,.15)}
.winbar{height:36px;background:#fff;border-bottom:1px solid ${c.bd};display:flex;align-items:center;gap:7px;padding:0 14px}
.dot{width:10px;height:10px;border-radius:50%}
.winurl{background:${c.nBg};border-radius:7px;height:20px;flex:2.4;font-size:11px;color:${c.mu};display:flex;align-items:center;justify-content:center}
.winport{overflow:hidden;position:relative;background:${c.bg}}
.winscale{width:1200px;transform:scale(.3567);transform-origin:top left}
/* mini chrome */
.mtop{height:56px;background:#fff;border-bottom:1px solid ${c.bd};display:flex;align-items:center;gap:20px;padding:0 24px}
.mbrand{display:flex;align-items:center;gap:8px;font-weight:700;font-size:16px}
.mnav{display:flex;gap:22px;color:${c.mu};font-size:14px}.mnav .on{color:${c.tx};font-weight:500}
.mside{width:256px;background:#fff;border-right:1px solid ${c.bd};padding:14px 12px;flex:none;min-height:560px}
.mcap{font-size:11px;letter-spacing:.05em;text-transform:uppercase;color:${c.fa};font-weight:600;margin:16px 8px 6px}
.mni{display:flex;align-items:center;gap:10px;padding:8px 10px;border-radius:8px;font-size:14px;color:${c.tx}}.mni.on{background:${c.nBg};font-weight:500}
.mbtn{display:flex;align-items:center;gap:8px;padding:9px 12px;border-radius:8px;font-size:14px}.mbtn.out{border:1px solid ${c.bd2}}
.mmain{flex:1;padding:28px 34px}
.mh1{font-size:26px;font-weight:700}.mh2{font-size:18px;font-weight:600}.msub{color:${c.mu};margin-top:6px}
.mcard{background:#fff;border:1px solid ${c.bd};border-radius:12px;padding:18px 20px;margin-bottom:16px}
.mtabs{display:flex;background:${c.nBg};border-radius:10px;padding:4px;gap:4px}.mtab{padding:8px 16px;border-radius:8px;color:${c.mu}}.mtab.on{background:${c.ac};color:#fff;font-weight:500}
/* shared */
.bd{display:inline-flex;align-items:center;padding:2px 9px;border-radius:6px;font-size:12px;font-weight:600}
.btn{display:inline-flex;align-items:center;gap:6px;height:36px;padding:0 14px;border-radius:8px;font-size:14px;font-weight:500;border:1px solid transparent}
.btn.pri{background:${c.ac};color:#fff}.btn.soft{background:${c.softBg};color:${c.softTx}}.btn.out{background:#fff;border-color:${c.bd2};color:${c.tx}}.btn.dng{background:${c.eBg};color:${c.eTx}}
.panel{background:#fff;border:1px solid ${c.bd};border-radius:14px;padding:22px}
.paneltitle{font-size:13px;font-weight:600;color:${c.mu};margin-bottom:16px}
.minp{display:flex;align-items:center;height:36px;border:1px solid ${c.bd2};border-radius:8px;padding:0 12px;color:${c.mu};font-size:13px}
.pillar{display:flex;gap:14px;background:#fff;border:1px solid ${c.bd};border-radius:14px;padding:18px 20px}
.pillicon{width:42px;height:42px;border-radius:11px;background:${c.softBg};color:${c.softTx};display:flex;align-items:center;justify-content:center;flex:none}
.foot{display:flex;justify-content:space-between;align-items:center;color:${c.mu};font-size:14px;padding-top:6px}
.accentline{height:5px;border-radius:3px;background:linear-gradient(90deg,${c.ac},${c.ac5},${c.softBg});margin-top:14px}
`
const dc=`<!doctype html>
<html><head><meta charset="utf-8"><script src="./support.js"></script></head>
<body><x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap">
<style>${CSS}</style>
</helmet>
${board}
</x-dc>
<script data-dc-script data-props='{"$preview":{"width":1480,"height":1500}}'>
class Component extends DCLogic { renderVals(){ return {} } }
</script>
</body></html>`
writeFileSync(OUT+'Main.dc.html', dc)
writeFileSync(OUT+'canvas.json', JSON.stringify({ artboards:[{file:'Main.dc.html',x:0,y:0,w:1480,h:1500}], launch:{view:'focused',file:'Main.dc.html'} }, null, 2))
console.log('wrote showcase Main.dc.html + canvas.json')
