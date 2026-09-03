import { writeFileSync } from 'node:fs'
const OUT = new URL('./', import.meta.url).pathname

const S=(p,s=18,sw=2,col='currentColor')=>`<svg width="${s}" height="${s}" viewBox="0 0 24 24" fill="none" stroke="${col}" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" style="flex:none">${p}</svg>`
const logo=(s,col)=>S(`<circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="9" r="2.4"/><path d="M6 8.4v7.2"/><path d="M18 11.4c0 3-2.5 3.6-5.5 4.2C10 16 8.4 16.6 8.4 18"/>`,s,2,col)
const iCode=(s,col)=>S(`<path d="M16 18l4-6-4-6M8 6l-4 6 4 6"/>`,s,2,col)
const iMerge=(s,col)=>S(`<circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="15" r="2.4"/><path d="M6 8.4v7.2M6 9a6 6 0 0 0 6 6h3.6"/>`,s,2,col)
const iPipe=(s,col)=>S(`<rect x="3" y="4" width="7" height="7" rx="1.5"/><rect x="14" y="13" width="7" height="7" rx="1.5"/><path d="M6.5 11v3a3 3 0 0 0 3 3H14"/>`,s,2,col)
const iChat=(s,col)=>S(`<path d="M21 15a2 2 0 0 1-2 2H8l-4 3V6a2 2 0 0 1 2-2h13a2 2 0 0 1 2 2z"/>`,s,2,col)
const iArrow=(s,col)=>S(`<path d="M5 12h14M13 6l6 6-6 6"/>`,s,2,col)

const BASE = { bg:'#0b0f10', sf:'#12181a', sf2:'#161d1f', bd:'#232b2d', tx:'#e6edf0', mu:'#8b979c', red:'#ff6b6b', amber:'#f5b350' }

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
const FONTS = `<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;700&family=JetBrains+Mono:wght@400;500;700&display=swap">`

/* one Terminal board, parameterised by accent theme A */
function board(A){
  const c = { ...BASE, ...A }
  const css = `*{box-sizing:border-box}html{height:100%}
body{margin:0;min-height:100%;font-family:'Space Grotesk',system-ui,sans-serif;background:${c.bg};color:${c.tx};font-size:14px;line-height:1.5}
.wrap{width:1360px;min-height:100%;background:${c.bg};background-image:radial-gradient(${c.bd} 1px,transparent 1px);background-size:26px 26px}
.mono{font-family:'JetBrains Mono',monospace}
.band{padding:14px 40px;border-bottom:1px solid ${c.bd};display:flex;justify-content:space-between;align-items:center;background:${c.bg}}
.tag{font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:.16em;text-transform:uppercase;color:${c.ac}}
.hero{padding:52px 40px 46px;border-bottom:1px solid ${c.bd};position:relative;overflow:hidden}
.glow{position:absolute;top:-140px;right:-80px;width:460px;height:340px;background:radial-gradient(circle,${c.glow},transparent 68%);pointer-events:none}
.prompt{font-family:'JetBrains Mono',monospace;color:${c.ac};font-size:14px;margin-bottom:16px;position:relative}
.h1{font-size:56px;font-weight:700;letter-spacing:-.02em;line-height:1.02;margin:0;max-width:900px;position:relative}
.h1 em{color:${c.ac};font-style:normal}
.sub{color:${c.mu};font-size:18px;margin:20px 0 26px;max-width:640px;position:relative}
.btn{display:inline-flex;align-items:center;gap:8px;font-family:'JetBrains Mono',monospace;font-weight:500;font-size:14px;padding:12px 18px;border-radius:8px}
.btn.p{background:${c.ac};color:${c.btx}}.btn.g{border:1px solid ${c.bd};color:${c.tx}}
.win{margin:38px 40px 0;border:1px solid ${c.bd};border-radius:12px;overflow:hidden;background:${c.sf}}
.winbar{height:40px;background:${c.sf2};border-bottom:1px solid ${c.bd};display:flex;align-items:center;gap:8px;padding:0 14px}
.dot{width:11px;height:11px;border-radius:50%}
.winurl{font-family:'JetBrains Mono',monospace;color:${c.mu};font-size:12px;margin-left:10px}
.wbody{display:flex}
.wside{width:230px;border-right:1px solid ${c.bd};padding:16px 12px}
.wnav{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:8px;font-family:'JetBrains Mono',monospace;font-size:13px;color:${c.mu}}
.wnav.on{background:${c.sf2};color:${c.ac};border:1px solid ${c.bd}}
.wmain{flex:1;padding:26px 30px}
.badge{font-family:'JetBrains Mono',monospace;font-size:11px;font-weight:500;padding:3px 9px;border-radius:5px;border:1px solid}
.card{border:1px solid ${c.bd};border-radius:10px;padding:16px 18px;background:${c.sf2};margin-bottom:14px}
.dual{display:grid;grid-template-columns:1fr 1fr;gap:20px;padding:20px 40px 0}
.panel{border:1px solid ${c.bd};border-radius:12px;padding:20px 22px;background:${c.sf}}
.pt{font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:${c.mu};margin-bottom:16px}
.mr{display:flex;align-items:center;gap:12px;padding:11px 0;border-top:1px solid ${c.bd}}
.mr:first-of-type{border-top:0}
.stage{display:flex;align-items:center;gap:10px;font-family:'JetBrains Mono',monospace;font-size:12.5px;padding:7px 0}
.log{font-family:'JetBrains Mono',monospace;font-size:12px;color:${c.mu};line-height:1.7;border-top:1px solid ${c.bd};margin-top:12px;padding-top:12px}
.log b{color:${c.ac};font-weight:500}
.sysrow{display:grid;grid-template-columns:1.1fr 1fr 1.4fr;gap:18px;padding:20px 40px 44px}
`
  const body = `<div class="wrap">
<div class="band"><span style="display:flex;align-items:center;gap:10px;font-family:'JetBrains Mono',monospace;font-weight:700">${logo(22,c.ac)} opengitbase</span><span class="tag">Terminal — ${c.name}</span></div>
<div class="hero"><div class="glow"></div>
<div class="prompt">$ ogb init --self-hosted</div>
<h1 class="h1">Git that runs<br>on <em>your</em> metal.</h1>
<p class="sub">Self-hosted repositories, merge requests, CI in Firecracker MicroVMs, and a storage fleet you control. No black boxes.</p>
<div style="display:flex;gap:12px">${`<span class="btn p">${iArrow(16,c.btx)} Get started</span>`}<span class="btn g">Read the docs</span></div>
</div>
<div class="win">
<div class="winbar"><span class="dot" style="background:#ff5f57"></span><span class="dot" style="background:#febc2e"></span><span class="dot" style="background:#28c840"></span><span class="winurl">demo-user/hello-world</span></div>
<div class="wbody">
<div class="wside"><div class="mono" style="color:${c.mu};font-size:11px;padding:2px 10px 10px">demo-user/hello-world</div>
<div class="wnav on">${iCode(15,c.ac)} code</div><div class="wnav">${iChat(15,c.mu)} discussions</div><div class="wnav">${iMerge(15,c.mu)} merge_requests</div><div class="wnav">${iPipe(15,c.mu)} pipelines</div></div>
<div class="wmain">
<div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:20px"><div><div class="mono" style="color:${c.mu};font-size:12px">demo-user / hello-world</div><div style="font-size:26px;font-weight:700;margin-top:4px">Hello World</div></div><span class="badge" style="color:${c.ok};border-color:${c.ok}">● public</span></div>
<div class="card" style="display:flex;align-items:center;gap:16px"><span class="mono" style="color:${c.mu};font-size:12px">ref</span><span class="badge" style="color:${c.tx};border-color:${c.bd}">main ▾</span><span class="mono" style="color:${c.ac}">abc123</span><span style="flex:1"></span><span class="mono" style="color:${c.mu};font-size:12px">2 branches · 3 tags</span></div>
<div class="card" style="display:flex;align-items:center;justify-content:space-between;margin-bottom:0"><div style="display:flex;justify-content:space-between;flex:1;margin-right:8px"><span class="mono" style="font-size:12px;color:${c.mu}">storage</span><span class="mono" style="font-size:12px;color:${c.ac}">500M / 1.0G · 49%</span></div></div>
<div style="height:8px;border-radius:5px;background:${c.bd};overflow:hidden;margin-top:10px"><i style="display:block;height:100%;width:49%;background:${c.ac}"></i></div>
</div></div></div>
<div class="dual">
<div class="panel"><div class="pt" style="display:flex;justify-content:space-between"><span>Merge requests</span><span style="color:${c.ac}">3 open</span></div>
<div class="mr">${iMerge(16,c.ac)}<div style="flex:1"><div style="font-weight:500">Refactor branch policy editor</div><div class="mono" style="color:${c.mu};font-size:11.5px;margin-top:2px">!7 · feature/branch-rules → main</div></div><span class="badge" style="color:${c.ac};border-color:${c.ac}">open</span></div>
<div class="mr">${iMerge(16,c.mu)}<div style="flex:1"><div style="font-weight:500">Add quorum replication heal</div><div class="mono" style="color:${c.mu};font-size:11.5px;margin-top:2px">!6 · storage/heal → main</div></div><span class="badge" style="color:${c.mu};border-color:${c.bd}">draft</span></div>
<div class="mr">${iMerge(16,c.mu)}<div style="flex:1"><div style="font-weight:500">Persist encrypted-artifact store</div><div class="mono" style="color:${c.mu};font-size:11.5px;margin-top:2px">!5 · storage/persist → main</div></div><span class="badge" style="color:${c.ok};border-color:${c.ok}">merged</span></div>
</div>
<div class="panel"><div class="pt" style="display:flex;justify-content:space-between"><span>Pipeline #42</span><span style="color:${c.ac}">running</span></div>
<div class="stage" style="color:${c.tx}"><span style="width:8px;height:8px;border-radius:50%;background:${c.ok}"></span>checkout <span style="flex:1"></span><span style="color:${c.mu}">4s</span></div>
<div class="stage" style="color:${c.tx}"><span style="width:8px;height:8px;border-radius:50%;background:${c.ok}"></span>build <span style="flex:1"></span><span style="color:${c.mu}">1m 12s</span></div>
<div class="stage" style="color:${c.ac}"><span style="width:8px;height:8px;border-radius:50%;background:${c.ac};box-shadow:0 0 8px ${c.ac}"></span>test <span style="flex:1"></span><span style="color:${c.mu}">0:41…</span></div>
<div style="height:6px;border-radius:4px;background:${c.bd};overflow:hidden;margin:6px 0 2px"><i style="display:block;height:100%;width:64%;background:${c.ac}"></i></div>
<div class="log">$ ogb ci run --stage test<br><b>›</b> spawning microVM ogb-hosted-3<br><b>›</b> 128 passed · <span style="color:${c.mu}">2 running</span></div>
</div>
</div>
<div class="sysrow">
<div class="panel"><div class="pt">Palette</div><div style="display:grid;grid-template-columns:repeat(4,1fr);gap:8px">${[[c.ac,'accent'],[c.ok,'passed'],[c.tx,'fg'],[c.mu,'muted'],[c.sf,'surface'],[c.sf2,'raised'],[c.bd,'border'],[c.bg,'bg']].map(([h,n])=>`<div><div style="height:40px;border-radius:7px;border:1px solid ${c.bd};background:${h}"></div><div class="mono" style="font-size:10px;color:${c.mu};margin-top:5px">${n}</div></div>`).join('')}</div>
<div class="mono" style="font-size:11px;color:${c.mu};margin-top:12px">accent ${c.ac}</div></div>
<div class="panel"><div class="pt">Type</div><div style="font-size:30px;font-weight:700">Space Grotesk</div><div class="mono" style="color:${c.mu};font-size:12px;margin:6px 0 14px">display · UI</div><div class="mono" style="font-size:14px;border-top:1px solid ${c.bd};padding-top:12px;color:${c.tx}">JetBrains Mono — code, refs, labels, metrics</div></div>
<div class="panel"><div class="pt">Components</div><div style="display:flex;gap:10px;flex-wrap:wrap;margin-bottom:14px"><span class="btn p">Primary</span><span class="btn g">Ghost</span></div><div style="display:flex;gap:8px;flex-wrap:wrap"><span class="badge" style="color:${c.ac};border-color:${c.ac}">accent</span><span class="badge" style="color:${c.ok};border-color:${c.ok}">passed</span><span class="badge" style="color:${c.amber};border-color:${c.amber}">degraded</span><span class="badge" style="color:${c.red};border-color:${c.red}">failed</span><span class="badge" style="color:${c.mu};border-color:${c.bd}">private</span></div></div>
</div>
</div>`
  return dcWrap(FONTS, css, body, 1360, 1600)
}

/* three accent themes — same base, only the primary changes */
const PHOSPHOR = { name:'Phosphor', ac:'#5ef08a', ok:'#5ef08a', btx:'#04140a', glow:'rgba(94,240,138,.20)' }
const ICE      = { name:'Ice',      ac:'#38e0e6', ok:'#7ee787', btx:'#03181b', glow:'rgba(56,224,230,.20)' }
const UV       = { name:'Ultraviolet', ac:'#a78bff', ok:'#7ee787', btx:'#0d0724', glow:'rgba(167,139,255,.22)' }

writeFileSync(OUT+'v-Phosphor.dc.html', board(PHOSPHOR))
writeFileSync(OUT+'v-Ice.dc.html', board(ICE))
writeFileSync(OUT+'v-Ultraviolet.dc.html', board(UV))

/* cover comparing the three accents (amber shown as the retired one) */
const chip = (label, ac, note) => `<div class="opt">
<div class="cap" style="background:linear-gradient(135deg,#0b0f10,#161c1e)"><span class="mono" style="color:${ac};font-size:22px;font-weight:700">${label}</span><span class="sw" style="background:${ac}"></span></div>
<div class="optb"><div class="mono" style="font-size:12px;color:${ac};margin-bottom:8px">${ac}</div><div class="line">${note}</div>
<div style="display:flex;gap:8px;margin-top:14px"><span class="mini" style="background:${ac};color:#08120c">Get started</span><span class="mini g">Ghost</span></div></div>
</div>`
const cover = dcWrap(FONTS, `*{box-sizing:border-box}html{height:100%}
body{margin:0;min-height:100%;font-family:'Space Grotesk',system-ui,sans-serif;background:#0b0f10;color:#e6edf0;font-size:15px;line-height:1.5}
.wrap{width:1180px;min-height:100%;background:#0b0f10;background-image:radial-gradient(#232b2d 1px,transparent 1px);background-size:26px 26px;padding:56px 60px}
.mono{font-family:'JetBrains Mono',monospace}
.k{font-family:'JetBrains Mono',monospace;font-size:12px;letter-spacing:.15em;text-transform:uppercase;color:#8b979c}
h1{font-size:42px;font-weight:700;letter-spacing:-.02em;margin:12px 0 12px}
.lede{font-size:18px;color:#8b979c;max-width:760px;margin:0 0 20px}
.was{display:inline-flex;align-items:center;gap:8px;font-family:'JetBrains Mono',monospace;font-size:12px;color:#8b979c;border:1px solid #232b2d;border-radius:8px;padding:7px 12px;margin-bottom:38px}
.was s{color:#f5b350}
.row{display:grid;grid-template-columns:repeat(3,1fr);gap:20px}
.opt{border:1px solid #232b2d;border-radius:16px;overflow:hidden;background:#12181a}
.cap{height:92px;display:flex;align-items:center;justify-content:space-between;padding:0 20px;border-bottom:1px solid #232b2d;position:relative}
.sw{width:34px;height:34px;border-radius:9px}
.optb{padding:18px 20px}
.line{color:#aeb8bd;font-size:13.5px}
.mini{font-family:'JetBrains Mono',monospace;font-size:12px;font-weight:500;padding:8px 12px;border-radius:7px}
.mini.g{border:1px solid #232b2d;color:#e6edf0}
.note{margin-top:34px;color:#8b979c;font-size:13.5px}`,
`<div class="wrap">
<div class="k">Terminal · accent study</div>
<h1>Same system, three primaries</h1>
<p class="lede">You liked Terminal but not the amber. Everything else stays exactly as it was — dark charcoal, dotted grid, Space Grotesk + JetBrains Mono, the full layout. Only the primary accent changes. Each board applies its accent across buttons, links, active nav, refs, progress, and a live pipeline so you can judge it in context, not as a swatch.</p>
<div class="was">was <s>amber #ffb454</s> ${iArrow(14,'#8b979c')} replaced below</div>
<div class="row">
${chip('Phosphor','#5ef08a','Classic CRT terminal green. Monochrome-leaning — accent doubles as the success color. Most "developer-native", unmistakably a tool.')}
${chip('Ice','#38e0e6','Electric cyan. Cool, clinical, modern. Reads as precise and infrastructural; furthest from the old teal while staying calm.')}
${chip('Ultraviolet','#a78bff','Soft violet. Premium and distinctive, least common in dev tools. Warmer personality without going warm-hued.')}
</div>
<div class="note">→ Three full boards beside this one — pick one, or tell me a hue and I'll dial it in.</div>
</div>`, 1180, 640)
writeFileSync(OUT+'Main.dc.html', cover)

const canvas = { artboards: [
  { file:'Main.dc.html', x:0, y:0, w:1180, h:640 },
  { file:'v-Phosphor.dc.html', x:0, y:820, w:1360, h:1600 },
  { file:'v-Ice.dc.html', x:1460, y:820, w:1360, h:1600 },
  { file:'v-Ultraviolet.dc.html', x:2920, y:820, w:1360, h:1600 },
], launch:{ view:'canvas' } }
writeFileSync(OUT+'canvas.json', JSON.stringify(canvas, null, 2))
console.log('wrote terminal accent cover + 3 variations')
