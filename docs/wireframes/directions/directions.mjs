import { writeFileSync } from 'node:fs'
const OUT = new URL('./', import.meta.url).pathname

const S=(p,s=18,sw=2,col='currentColor')=>`<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="none" stroke="${col}" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" style="flex:none">${p}</svg>`
const logo=(s,col)=>S(`<circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="9" r="2.4"/><path d="M6 8.4v7.2"/><path d="M18 11.4c0 3-2.5 3.6-5.5 4.2C10 16 8.4 16.6 8.4 18"/>`,s,2,col)
const iCode=(s,col)=>S(`<path d="M16 18l4-6-4-6M8 6l-4 6 4 6"/>`,s,2,col)
const iMerge=(s,col)=>S(`<circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="15" r="2.4"/><path d="M6 8.4v7.2M6 9a6 6 0 0 0 6 6h3.6"/>`,s,2,col)
const iPipe=(s,col)=>S(`<rect x="3" y="4" width="7" height="7" rx="1.5"/><rect x="14" y="13" width="7" height="7" rx="1.5"/><path d="M6.5 11v3a3 3 0 0 0 3 3H14"/>`,s,2,col)
const iChat=(s,col)=>S(`<path d="M21 15a2 2 0 0 1-2 2H8l-4 3V6a2 2 0 0 1 2-2h13a2 2 0 0 1 2 2z"/>`,s,2,col)
const iServer=(s,col)=>S(`<rect x="3" y="4" width="18" height="7" rx="1.5"/><rect x="3" y="13" width="18" height="7" rx="1.5"/><path d="M7 7.5h.01M7 16.5h.01"/>`,s,2,col)
const iStar=(s,col)=>S(`<path d="M12 3l2.6 5.3 5.9.9-4.2 4.1 1 5.8L12 16.9 6.7 19.6l1-5.8L3.5 9.7l5.9-.9z"/>`,s,2,col)
const iArrow=(s,col)=>S(`<path d="M5 12h14M13 6l6 6-6 6"/>`,s,2,col)

function dcWrap(headExtra, css, body, pw=1360, ph=1600){
  return `<!doctype html>
<html><head><meta charset="utf-8"><script src="./support.js"></script></head>
<body><x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
${headExtra}
<style>${css}</style>
</helmet>
${body}
</x-dc>
<script data-dc-script data-props='{"$preview":{"width":${pw},"height":${ph}}}'>
class Component extends DCLogic { renderVals(){ return {} } }
</script>
</body></html>`
}

/* ============================================================= COVER (Main) */
const cover = dcWrap(
`<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap">`,
`*{box-sizing:border-box}html{height:100%}
body{margin:0;min-height:100%;font-family:'Inter',system-ui,sans-serif;background:#0f1113;color:#e9edf0;font-size:15px;line-height:1.5}
.wrap{width:1180px;min-height:100%;background:#0f1113;padding:60px 64px}
.mono{font-family:'JetBrains Mono',monospace}
.k{font-family:'JetBrains Mono',monospace;font-size:12px;letter-spacing:.14em;text-transform:uppercase;color:#8a949b}
h1{font-size:44px;font-weight:700;letter-spacing:-.02em;margin:14px 0 12px}
.lede{font-size:19px;color:#aab4bb;max-width:760px;margin:0 0 44px}
.row{display:grid;grid-template-columns:repeat(3,1fr);gap:22px}
.opt{border:1px solid #262b2f;border-radius:16px;overflow:hidden;background:#14181b}
.swatch{height:96px;display:flex;align-items:flex-end;padding:14px}
.optb{padding:20px}
.optname{font-size:22px;font-weight:700;margin:0 0 2px}
.opttag{color:#939 da;font-size:13px}
.line{color:#aab4bb;font-size:13.5px;margin:12px 0}
.meta{border-top:1px solid #262b2f;padding-top:12px;margin-top:14px;font-size:12.5px;line-height:1.7}
.meta b{color:#e9edf0}.meta span{color:#8a949b}
.note{margin-top:40px;color:#8a949b;font-size:13.5px}
`,
`<div class="wrap">
<div class="k">OpenGitBase · design exploration</div>
<h1>Three directions with a point of view</h1>
<p class="lede">The current UI is competent but neutral — it reads as "vibecoded", no feeling attached. These three directions each commit to a distinct personality, shown on the same real screens. Pick the feeling; the winner becomes the new system.</p>
<div class="row">
<div class="opt"><div class="swatch" style="background:linear-gradient(135deg,#0b0f10,#161c1e);border-bottom:1px solid #262b2f"><span class="mono" style="color:#ffb454;font-size:22px;font-weight:700">A · Terminal</span></div><div class="optb">
<div class="line">Dark, developer-native, monospace-forward. Amber on charcoal. The tool power-users brag about.</div>
<div class="meta"><b>Best when</b> <span>your audience is engineers and the product should feel precise, fast, opinionated.</span><br><b>Tradeoff</b> <span>dark-first can feel niche to non-technical buyers.</span></div>
</div></div>
<div class="opt"><div class="swatch" style="background:#f3ece0;border-bottom:1px solid #262b2f"><span style="color:#1c1a17;font-family:Georgia,serif;font-size:26px;font-style:italic">B · Editorial</span></div><div class="optb">
<div class="line">Warm paper, high-contrast serif display, generous space. Human, crafted, a clear voice.</div>
<div class="meta"><b>Best when</b> <span>you want to feel considered and community-first, standing apart from cold dev tools.</span><br><b>Tradeoff</b> <span>serifs + air need discipline to stay information-dense.</span></div>
</div></div>
<div class="opt"><div class="swatch" style="background:#e8ff59;border-bottom:1px solid #262b2f"><span style="color:#111;font-family:'JetBrains Mono',monospace;font-size:24px;font-weight:700">C · Bold</span></div><div class="optb">
<div class="line">Neo-brutalist. Vivid ink + acid accent, chunky type, hard shadows. Loud and unmistakable.</div>
<div class="meta"><b>Best when</b> <span>you want to be memorable and category-defining, not another quiet SaaS.</span><br><b>Tradeoff</b> <span>high energy can tire on long, dense work sessions.</span></div>
</div></div>
</div>
<div class="note">→ The three artboards beside this one apply each direction to the same masthead, a repository screen, and the design language (color · type · components).</div>
</div>`, 1180, 720)

/* ============================================================= A · TERMINAL */
const T = { bg:'#0b0f10', sf:'#12181a', sf2:'#161d1f', bd:'#232b2d', tx:'#e6edf0', mu:'#8b979c', ac:'#ffb454', ac2:'#7ee787', red:'#ff6b6b' }
const terminal = dcWrap(
`<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;700&family=JetBrains+Mono:wght@400;500;700&display=swap">`,
`*{box-sizing:border-box}html{height:100%}
body{margin:0;min-height:100%;font-family:'Space Grotesk',system-ui,sans-serif;background:${T.bg};color:${T.tx};font-size:14px;line-height:1.5}
.wrap{width:1360px;min-height:100%;background:${T.bg};background-image:radial-gradient(${T.bd} 1px,transparent 1px);background-size:26px 26px;padding:0}
.mono{font-family:'JetBrains Mono',monospace}
.band{padding:14px 40px;border-bottom:1px solid ${T.bd};display:flex;justify-content:space-between;align-items:center;background:${T.bg}}
.tag{font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:.16em;text-transform:uppercase;color:${T.ac}}
.hero{padding:56px 40px 48px;border-bottom:1px solid ${T.bd}}
.prompt{font-family:'JetBrains Mono',monospace;color:${T.ac2};font-size:14px;margin-bottom:18px}
.h1{font-size:58px;font-weight:700;letter-spacing:-.02em;line-height:1.02;margin:0;max-width:900px}
.h1 em{color:${T.ac};font-style:normal}
.sub{color:${T.mu};font-size:18px;margin:20px 0 28px;max-width:620px}
.btn{display:inline-flex;align-items:center;gap:8px;font-family:'JetBrains Mono',monospace;font-weight:500;font-size:14px;padding:12px 18px;border-radius:8px}
.btn.p{background:${T.ac};color:#1a1205}.btn.g{border:1px solid ${T.bd};color:${T.tx}}
.win{margin:40px;border:1px solid ${T.bd};border-radius:12px;overflow:hidden;background:${T.sf}}
.winbar{height:40px;background:${T.sf2};border-bottom:1px solid ${T.bd};display:flex;align-items:center;gap:8px;padding:0 14px}
.dot{width:11px;height:11px;border-radius:50%}
.winurl{font-family:'JetBrains Mono',monospace;color:${T.mu};font-size:12px;margin-left:10px}
.wbody{display:flex}
.wside{width:230px;border-right:1px solid ${T.bd};padding:16px 12px}
.wnav{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:8px;font-family:'JetBrains Mono',monospace;font-size:13px;color:${T.mu}}
.wnav.on{background:${T.sf2};color:${T.ac};border:1px solid ${T.bd}}
.wmain{flex:1;padding:26px 30px}
.badge{font-family:'JetBrains Mono',monospace;font-size:11px;font-weight:500;padding:3px 9px;border-radius:5px;border:1px solid}
.card{border:1px solid ${T.bd};border-radius:10px;padding:16px 18px;background:${T.sf2};margin-bottom:14px}
.sysrow{display:grid;grid-template-columns:1.1fr 1fr 1.4fr;gap:18px;padding:0 40px 48px}
.panel{border:1px solid ${T.bd};border-radius:12px;padding:22px;background:${T.sf}}
.pt{font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:${T.mu};margin-bottom:16px}
`,
`<div class="wrap">
<div class="band"><span style="display:flex;align-items:center;gap:10px;font-family:'JetBrains Mono',monospace;font-weight:700">${logo(22,T.ac)} opengitbase</span><span class="tag">Direction A — Terminal</span></div>
<div class="hero">
<div class="prompt">$ ogb init --self-hosted</div>
<h1 class="h1">Git that runs<br>on <em>your</em> metal.</h1>
<p class="sub">Self-hosted repositories, merge requests, CI in Firecracker MicroVMs, and a storage fleet you control. No black boxes.</p>
<div style="display:flex;gap:12px">${`<span class="btn p">${iArrow(16,'#1a1205')} Get started</span>`}<span class="btn g">Read the docs</span></div>
</div>
<div class="win">
<div class="winbar"><span class="dot" style="background:#ff5f57"></span><span class="dot" style="background:#febc2e"></span><span class="dot" style="background:#28c840"></span><span class="winurl">demo-user/hello-world</span></div>
<div class="wbody">
<div class="wside"><div class="mono" style="color:${T.mu};font-size:11px;padding:2px 10px 10px">demo-user/hello-world</div>
<div class="wnav on">${iCode(15,T.ac)} code</div><div class="wnav">${iChat(15,T.mu)} discussions</div><div class="wnav">${iMerge(15,T.mu)} merge_requests</div><div class="wnav">${iPipe(15,T.mu)} pipelines</div></div>
<div class="wmain">
<div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:20px"><div><div class="mono" style="color:${T.mu};font-size:12px">demo-user / hello-world</div><div style="font-size:26px;font-weight:700;margin-top:4px">Hello World</div></div><span class="badge" style="color:${T.ac2};border-color:${T.ac2}">● public</span></div>
<div class="card" style="display:flex;align-items:center;gap:16px"><span class="mono" style="color:${T.mu};font-size:12px">ref</span><span class="badge" style="color:${T.tx};border-color:${T.bd}">main ▾</span><span class="mono" style="color:${T.ac}">abc123</span><span style="flex:1"></span><span class="mono" style="color:${T.mu};font-size:12px">2 branches · 3 tags</span></div>
<div class="card" style="display:flex;align-items:center;justify-content:space-between"><div style="display:flex;align-items:center;gap:12px">${iPipe(18,T.ac2)}<div><div style="font-weight:500">pipeline #42 · build, test</div><div class="mono" style="color:${T.mu};font-size:12px;margin-top:2px">refs/heads/main · ogb-hosted</div></div></div><span class="badge" style="color:${T.ac2};border-color:${T.ac2}">passed</span></div>
<div class="card" style="margin-bottom:0"><div style="display:flex;justify-content:space-between;margin-bottom:8px"><span class="mono" style="font-size:12px;color:${T.mu}">storage</span><span class="mono" style="font-size:12px;color:${T.ac}">500M / 1.0G · 49%</span></div><div style="height:8px;border-radius:5px;background:${T.bd};overflow:hidden"><i style="display:block;height:100%;width:49%;background:${T.ac}"></i></div></div>
</div></div></div>
<div class="sysrow">
<div class="panel"><div class="pt">Palette</div><div style="display:grid;grid-template-columns:repeat(4,1fr);gap:8px">${[[T.ac,'amber'],[T.ac2,'green'],[T.tx,'fg'],[T.mu,'muted'],[T.sf,'surface'],[T.sf2,'raised'],[T.bd,'border'],[T.bg,'bg']].map(([h,n])=>`<div><div style="height:40px;border-radius:7px;border:1px solid ${T.bd};background:${h}"></div><div class="mono" style="font-size:10px;color:${T.mu};margin-top:5px">${n}</div></div>`).join('')}</div></div>
<div class="panel"><div class="pt">Type</div><div style="font-size:30px;font-weight:700">Space Grotesk</div><div class="mono" style="color:${T.mu};font-size:12px;margin:6px 0 14px">display · UI</div><div class="mono" style="font-size:14px;border-top:1px solid ${T.bd};padding-top:12px;color:${T.tx}">JetBrains Mono — code, refs, labels, metrics</div></div>
<div class="panel"><div class="pt">Components</div><div style="display:flex;gap:10px;flex-wrap:wrap;margin-bottom:14px"><span class="btn p">Primary</span><span class="btn g">Ghost</span></div><div style="display:flex;gap:8px;flex-wrap:wrap"><span class="badge" style="color:${T.ac2};border-color:${T.ac2}">passed</span><span class="badge" style="color:${T.ac};border-color:${T.ac}">degraded</span><span class="badge" style="color:${T.red};border-color:${T.red}">failed</span><span class="badge" style="color:${T.mu};border-color:${T.bd}">private</span></div></div>
</div>
</div>`)

/* ============================================================= B · EDITORIAL */
const E = { bg:'#f4ece0', sf:'#fffdf9', ink:'#211d18', mu:'#6b6156', bd:'#e2d7c6', ac:'#c0562f', ac2:'#1d3a5f' }
const editorial = dcWrap(
`<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Instrument+Serif:ital@0;1&family=Inter:wght@400;500;600&display=swap">`,
`*{box-sizing:border-box}html{height:100%}
body{margin:0;min-height:100%;font-family:'Inter',system-ui,sans-serif;background:${E.bg};color:${E.ink};font-size:15px;line-height:1.55}
.wrap{width:1360px;min-height:100%;background:${E.bg};padding:0}
.serif{font-family:'Instrument Serif',Georgia,serif;font-weight:400}
.band{padding:22px 56px;display:flex;justify-content:space-between;align-items:center;border-bottom:1px solid ${E.bd}}
.rule{font-family:'Inter';font-size:11px;letter-spacing:.22em;text-transform:uppercase;color:${E.mu}}
.hero{padding:64px 56px 52px;display:grid;grid-template-columns:1.5fr 1fr;gap:48px;align-items:end;border-bottom:1px solid ${E.bd}}
.h1{font-family:'Instrument Serif',Georgia,serif;font-weight:400;font-size:80px;line-height:.98;letter-spacing:-.01em;margin:0}
.h1 em{color:${E.ac};font-style:italic}
.sub{font-size:19px;color:${E.mu};max-width:440px;margin:22px 0 0}
.btn{display:inline-flex;align-items:center;gap:8px;font-weight:500;font-size:15px;padding:12px 22px;border-radius:999px}
.btn.p{background:${E.ink};color:${E.bg}}.btn.g{border:1px solid ${E.ink};color:${E.ink}}
.drop{font-family:'Instrument Serif',serif;font-size:15px;color:${E.mu};border-left:2px solid ${E.ac};padding-left:16px;line-height:1.6}
.win{margin:48px 56px;border:1px solid ${E.bd};border-radius:6px;overflow:hidden;background:${E.sf};box-shadow:0 24px 60px -30px rgba(33,29,24,.4)}
.winbar{height:44px;background:${E.sf};border-bottom:1px solid ${E.bd};display:flex;align-items:center;gap:8px;padding:0 16px}
.dot{width:10px;height:10px;border-radius:50%;border:1px solid ${E.bd}}
.winurl{color:${E.mu};font-size:12.5px;margin-left:10px}
.wbody{display:flex}
.wside{width:230px;border-right:1px solid ${E.bd};padding:22px 16px}
.wnav{display:flex;align-items:center;gap:11px;padding:9px 12px;border-radius:8px;font-size:14.5px;color:${E.mu}}
.wnav.on{background:${E.bg};color:${E.ink};font-weight:500}
.wmain{flex:1;padding:32px 36px}
.badge{font-size:12px;font-weight:500;padding:3px 11px;border-radius:999px}
.card{border:1px solid ${E.bd};border-radius:10px;padding:18px 20px;margin-bottom:16px;background:${E.sf}}
.sysrow{display:grid;grid-template-columns:1.1fr 1fr 1.4fr;gap:22px;padding:0 56px 52px}
.panel{border:1px solid ${E.bd};border-radius:6px;padding:26px;background:${E.sf}}
.pt{font-size:11px;letter-spacing:.18em;text-transform:uppercase;color:${E.mu};margin-bottom:18px}
`,
`<div class="wrap">
<div class="band"><span class="serif" style="font-size:24px;display:flex;align-items:center;gap:10px">${logo(22,E.ac)} OpenGitBase</span><span class="rule">Direction B — Editorial</span></div>
<div class="hero">
<div><div class="rule" style="margin-bottom:18px">The self-hosted forge</div>
<h1 class="h1">Git that's<br>yours to <em>design.</em></h1>
<p class="sub">Source-open, privacy-first, transparent by default. A place for your code that feels considered — because where it lives matters.</p>
<div style="display:flex;gap:12px;margin-top:30px">${`<span class="btn p">Get started ${iArrow(15,E.bg)}</span>`}<span class="btn g">Read the pitch</span></div></div>
<div class="drop">"Your code, your infra, your rules." A community-first project, built in the open — not another anonymous SaaS dashboard.</div>
</div>
<div class="win">
<div class="winbar"><span class="dot"></span><span class="dot"></span><span class="dot"></span><span class="winurl">demo-user / hello-world</span></div>
<div class="wbody">
<div class="wside"><div style="color:${E.mu};font-size:12.5px;padding:2px 10px 12px">demo-user / hello-world</div>
<div class="wnav on">${iCode(16,E.ink)} Code</div><div class="wnav">${iChat(16,E.mu)} Discussions</div><div class="wnav">${iMerge(16,E.mu)} Merge requests</div><div class="wnav">${iPipe(16,E.mu)} Pipelines</div></div>
<div class="wmain">
<div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:22px"><div><div style="color:${E.mu};font-size:13px">demo-user / hello-world</div><div class="serif" style="font-size:40px;margin-top:2px">Hello World</div></div><span class="badge" style="background:${E.ink};color:${E.bg}">Public</span></div>
<div class="card" style="display:flex;align-items:center;gap:16px"><span style="color:${E.mu};font-size:13px">Ref</span><span class="badge" style="border:1px solid ${E.bd};color:${E.ink}">main ▾</span><span style="color:${E.ac};font-weight:500">abc123</span><span style="flex:1"></span><span style="color:${E.mu};font-size:13px">Updated 2 months ago</span></div>
<div class="card" style="display:flex;align-items:center;justify-content:space-between"><div style="display:flex;align-items:center;gap:12px">${iMerge(18,E.ac2)}<div><div style="font-weight:500">Refactor branch policy editor</div><div style="color:${E.mu};font-size:13px;margin-top:2px">!7 · feature/branch-rules → main</div></div></div><span class="badge" style="background:#dbe7f2;color:${E.ac2}">Open</span></div>
<div class="card" style="margin-bottom:0"><div style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:13.5px"><span>Storage usage</span><span style="color:${E.mu}">500 MB / 1.00 GB · 49%</span></div><div style="height:8px;border-radius:5px;background:${E.bd};overflow:hidden"><i style="display:block;height:100%;width:49%;background:${E.ac}"></i></div></div>
</div></div></div>
<div class="sysrow">
<div class="panel"><div class="pt">Palette</div><div style="display:grid;grid-template-columns:repeat(4,1fr);gap:9px">${[[E.ac,'terracotta'],[E.ac2,'ink blue'],[E.ink,'ink'],[E.mu,'muted'],[E.sf,'paper'],[E.bg,'cream'],[E.bd,'rule'],['#dbe7f2','wash']].map(([h,n])=>`<div><div style="height:42px;border-radius:6px;border:1px solid ${E.bd};background:${h}"></div><div style="font-size:10.5px;color:${E.mu};margin-top:5px">${n}</div></div>`).join('')}</div></div>
<div class="panel"><div class="pt">Type</div><div class="serif" style="font-size:44px;line-height:1">Instrument<br><em style="color:${E.ac};font-style:italic">Serif</em></div><div style="color:${E.mu};font-size:12.5px;margin:12px 0 0;border-top:1px solid ${E.bd};padding-top:12px">Inter — body, UI, labels &amp; metrics</div></div>
<div class="panel"><div class="pt">Components</div><div style="display:flex;gap:10px;flex-wrap:wrap;margin-bottom:16px"><span class="btn p">Primary</span><span class="btn g">Outline</span></div><div style="display:flex;gap:8px;flex-wrap:wrap"><span class="badge" style="background:${E.ink};color:${E.bg}">Public</span><span class="badge" style="background:#dbe7f2;color:${E.ac2}">Open</span><span class="badge" style="background:#f6e0d6;color:${E.ac}">Degraded</span><span class="badge" style="border:1px solid ${E.bd};color:${E.mu}">Private</span></div></div>
</div>
</div>`)

/* ============================================================= C · BOLD */
const B = { bg:'#f5f4ef', ink:'#111111', card:'#ffffff', ac:'#3b1cff', ac2:'#e8ff40', mu:'#5b5b57', bd:'#111111' }
const bold = dcWrap(
`<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@500;700&family=Archivo:wght@600;700;800;900&family=Inter:wght@400;500;600&display=swap">`,
`*{box-sizing:border-box}html{height:100%}
body{margin:0;min-height:100%;font-family:'Inter',system-ui,sans-serif;background:${B.bg};color:${B.ink};font-size:15px;line-height:1.5}
.wrap{width:1360px;min-height:100%;background:${B.bg};padding:0}
.disp{font-family:'Archivo',system-ui,sans-serif;font-weight:800;letter-spacing:-.02em}
.grot{font-family:'Space Grotesk',monospace}
.band{padding:16px 44px;display:flex;justify-content:space-between;align-items:center;border-bottom:3px solid ${B.ink};background:${B.ac2}}
.tag{font-family:'Space Grotesk';font-weight:700;font-size:12px;letter-spacing:.1em;text-transform:uppercase}
.hero{padding:56px 44px 52px;border-bottom:3px solid ${B.ink}}
.eyebrow{font-family:'Space Grotesk';font-weight:700;font-size:13px;letter-spacing:.14em;text-transform:uppercase;color:${B.ac};margin-bottom:16px}
.h1{font-family:'Archivo';font-weight:900;font-size:76px;line-height:.94;letter-spacing:-.03em;text-transform:uppercase;margin:0;max-width:960px}
.h1 mark{background:${B.ac2};color:${B.ink};padding:0 8px}
.sub{font-size:19px;color:${B.mu};max-width:560px;margin:24px 0 30px}
.btn{display:inline-flex;align-items:center;gap:8px;font-family:'Space Grotesk';font-weight:700;font-size:15px;padding:13px 22px;border:3px solid ${B.ink};box-shadow:5px 5px 0 ${B.ink}}
.btn.p{background:${B.ac};color:#fff}.btn.g{background:#fff;color:${B.ink}}
.win{margin:44px;border:3px solid ${B.ink};overflow:hidden;background:#fff;box-shadow:10px 10px 0 ${B.ink}}
.winbar{height:44px;border-bottom:3px solid ${B.ink};display:flex;align-items:center;gap:9px;padding:0 16px;background:${B.ac2}}
.dot{width:12px;height:12px;border-radius:50%;border:2px solid ${B.ink}}
.winurl{font-family:'Space Grotesk';font-weight:500;color:${B.ink};font-size:12.5px;margin-left:10px}
.wbody{display:flex}
.wside{width:230px;border-right:3px solid ${B.ink};padding:16px 12px}
.wnav{display:flex;align-items:center;gap:10px;padding:10px 12px;font-family:'Space Grotesk';font-weight:500;font-size:14px;color:${B.ink};margin-bottom:6px}
.wnav.on{background:${B.ink};color:${B.ac2}}
.wmain{flex:1;padding:26px 30px}
.badge{font-family:'Space Grotesk';font-weight:700;font-size:12px;padding:3px 10px;border:2px solid ${B.ink}}
.card{border:2px solid ${B.ink};padding:16px 18px;margin-bottom:14px;background:#fff}
.sysrow{display:grid;grid-template-columns:1.1fr 1fr 1.4fr;gap:22px;padding:0 44px 52px}
.panel{border:3px solid ${B.ink};padding:22px;background:#fff;box-shadow:6px 6px 0 ${B.ink}}
.pt{font-family:'Space Grotesk';font-weight:700;font-size:12px;letter-spacing:.12em;text-transform:uppercase;margin-bottom:16px}
`,
`<div class="wrap">
<div class="band"><span class="disp" style="font-size:20px;display:flex;align-items:center;gap:10px">${logo(22,B.ink)} OPENGITBASE</span><span class="tag">Direction C — Bold</span></div>
<div class="hero">
<div class="eyebrow">Self-hosted git forge</div>
<h1 class="h1">Host your own git. <mark>Own the whole stack.</mark></h1>
<p class="sub">Repositories, merge requests, CI in MicroVMs, and a storage fleet you actually control. Loud about it, on purpose.</p>
<div style="display:flex;gap:16px">${`<span class="btn p">Get started ${iArrow(15,'#fff')}</span>`}<span class="btn g">Read the docs</span></div>
</div>
<div class="win">
<div class="winbar"><span class="dot" style="background:#ff5f57"></span><span class="dot" style="background:#febc2e"></span><span class="dot" style="background:#28c840"></span><span class="winurl">demo-user/hello-world</span></div>
<div class="wbody">
<div class="wside"><div class="grot" style="font-weight:500;color:${B.mu};font-size:12px;padding:2px 10px 10px">demo-user/hello-world</div>
<div class="wnav on">${iCode(15,B.ac2)} Code</div><div class="wnav">${iChat(15,B.ink)} Discussions</div><div class="wnav">${iMerge(15,B.ink)} Merge requests</div><div class="wnav">${iPipe(15,B.ink)} Pipelines</div></div>
<div class="wmain">
<div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:20px"><div><div class="grot" style="font-weight:500;color:${B.mu};font-size:12.5px">demo-user / hello-world</div><div class="disp" style="font-size:36px;text-transform:uppercase;margin-top:2px">Hello World</div></div><span class="badge" style="background:${B.ac2}">PUBLIC</span></div>
<div class="card" style="display:flex;align-items:center;gap:14px"><span class="grot" style="font-weight:700;font-size:12px;text-transform:uppercase">Ref</span><span class="badge">main ▾</span><span class="grot" style="font-weight:700;color:${B.ac}">abc123</span><span style="flex:1"></span><span class="grot" style="color:${B.mu};font-size:12.5px">2 branches · 3 tags</span></div>
<div class="card" style="display:flex;align-items:center;justify-content:space-between"><div style="display:flex;align-items:center;gap:12px">${iPipe(18,B.ac)}<div><div class="grot" style="font-weight:700">Pipeline #42 — build, test</div><div class="grot" style="color:${B.mu};font-size:12.5px;margin-top:2px">refs/heads/main · ogb-hosted</div></div></div><span class="badge" style="background:${B.ac2}">PASSED</span></div>
<div class="card" style="margin-bottom:0"><div style="display:flex;justify-content:space-between;margin-bottom:8px" class="grot"><span style="font-weight:700;font-size:12px;text-transform:uppercase">Storage</span><span style="font-size:12.5px;color:${B.mu}">500MB / 1.0GB · 49%</span></div><div style="height:12px;border:2px solid ${B.ink};overflow:hidden"><i style="display:block;height:100%;width:49%;background:${B.ac}"></i></div></div>
</div></div></div>
<div class="sysrow">
<div class="panel"><div class="pt">Palette</div><div style="display:grid;grid-template-columns:repeat(4,1fr);gap:9px">${[[B.ac,'ultra'],[B.ac2,'acid'],[B.ink,'ink'],[B.mu,'muted'],['#fff','card'],[B.bg,'bg'],['#ff5f57','alert'],['#28c840','ok']].map(([h,n])=>`<div><div style="height:42px;border:2px solid ${B.ink};background:${h}"></div><div class="grot" style="font-size:10.5px;margin-top:5px">${n}</div></div>`).join('')}</div></div>
<div class="panel"><div class="pt">Type</div><div class="disp" style="font-size:40px;text-transform:uppercase;line-height:.95">Archivo<br>Black</div><div class="grot" style="font-size:12.5px;margin:12px 0 0;border-top:2px solid ${B.ink};padding-top:12px">Space Grotesk — UI, labels, metrics</div></div>
<div class="panel"><div class="pt">Components</div><div style="display:flex;gap:14px;flex-wrap:wrap;margin-bottom:18px"><span class="btn p">Primary</span><span class="btn g">Secondary</span></div><div style="display:flex;gap:10px;flex-wrap:wrap"><span class="badge" style="background:${B.ac2}">PASSED</span><span class="badge" style="background:#ffe08a">DEGRADED</span><span class="badge" style="background:#ffd0cc">FAILED</span><span class="badge">PRIVATE</span></div></div>
</div>
</div>`)

/* write */
writeFileSync(OUT+'Main.dc.html', cover)
writeFileSync(OUT+'Terminal.dc.html', terminal)
writeFileSync(OUT+'Editorial.dc.html', editorial)
writeFileSync(OUT+'Bold.dc.html', bold)
const canvas = {
  artboards: [
    { file:'Main.dc.html', x:0, y:0, w:1180, h:720 },
    { file:'Terminal.dc.html', x:0, y:900, w:1360, h:1600 },
    { file:'Editorial.dc.html', x:1460, y:900, w:1360, h:1600 },
    { file:'Bold.dc.html', x:2920, y:900, w:1360, h:1600 },
  ],
  launch: { view:'canvas' },
}
writeFileSync(OUT+'canvas.json', JSON.stringify(canvas, null, 2))
console.log('wrote cover + 3 directions + canvas.json')
