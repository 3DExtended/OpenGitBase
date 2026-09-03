// Builds the parity website: report/index.html with side-by-side, overlay-slider,
// and single views for each screen. Set OGB_PARITY_INLINE=1 to embed the PNGs as
// data URIs (a single self-contained file, publishable / shareable).
import { readFileSync, writeFileSync, existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import { execSync } from 'node:child_process'
import { SCREENS, WIDTH } from './screens.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const REPORT = join(HERE, 'report')
const INLINE = process.env.OGB_PARITY_INLINE === '1'

const dim = (p) => {
  try {
    const w = execSync(`sips -g pixelWidth "${p}" | tail -1 | awk '{print $2}'`).toString().trim()
    const h = execSync(`sips -g pixelHeight "${p}" | tail -1 | awk '{print $2}'`).toString().trim()
    return `${w}×${h}`
  } catch { return '' }
}
const srcFor = (kind, id) => {
  const p = join(REPORT, kind, id + '.png')
  if (!existsSync(p)) return null
  if (INLINE) return 'data:image/png;base64,' + readFileSync(p).toString('base64')
  return kind + '/' + id + '.png'
}
const screens = SCREENS.map(s => ({
  id: s.id, label: s.label, route: s.route, note: s.note,
  mock: srcFor('mock', s.id), real: srcFor('real', s.id),
  mockDim: existsSync(join(REPORT, 'mock', s.id + '.png')) ? dim(join(REPORT, 'mock', s.id + '.png')) : '',
  realDim: existsSync(join(REPORT, 'real', s.id + '.png')) ? dim(join(REPORT, 'real', s.id + '.png')) : '',
}))
const stamp = new Date().toISOString().replace('T', ' ').slice(0, 16) + ' UTC'
const pairs = screens.filter(s => s.mock && s.real).length

const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>OpenGitBase · Design Parity</title>
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;700&family=JetBrains+Mono:wght@400;500;700&display=swap">
<style>
:root{--bg:#0b0f10;--sf:#12181a;--sf2:#161d1f;--bd:#232b2d;--bd2:#2c3538;--tx:#e6edf0;--mu:#8b979c;--dim:#5f6a6e;--ac:#38e0e6;--ok:#7ee787}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--tx);font-family:'Space Grotesk',system-ui,sans-serif;background-image:radial-gradient(var(--bd) 1px,transparent 1px);background-size:26px 26px}
.mono{font-family:'JetBrains Mono',monospace}
header{position:sticky;top:0;z-index:20;background:rgba(11,15,16,.92);backdrop-filter:blur(8px);border-bottom:1px solid var(--bd);padding:16px 28px}
.htop{display:flex;align-items:center;justify-content:space-between;gap:20px;flex-wrap:wrap}
h1{font-size:19px;font-weight:700;margin:0;display:flex;align-items:center;gap:10px}
.dot{width:10px;height:10px;border-radius:50%;background:var(--ac);box-shadow:0 0 10px var(--ac)}
.sub{color:var(--mu);font-size:12.5px;margin-top:3px}
.modes{display:flex;gap:4px;background:var(--sf);border:1px solid var(--bd);border-radius:9px;padding:3px}
.mode{padding:7px 13px;border-radius:7px;font-size:12.5px;color:var(--mu);cursor:pointer;border:0;background:transparent;font-family:inherit}
.mode.on{background:var(--sf2);color:var(--ac);border:1px solid var(--bd)}
.tabs{display:flex;gap:6px;flex-wrap:wrap;margin-top:14px}
.tab{padding:8px 13px;border-radius:8px;font-size:12.5px;color:var(--mu);cursor:pointer;border:1px solid var(--bd);background:var(--sf);font-family:inherit;display:flex;align-items:center;gap:8px}
.tab.on{color:var(--ac);border-color:var(--ac)}
.tab .r{font-family:'JetBrains Mono',monospace;font-size:10.5px;color:var(--dim)}
main{padding:26px 28px 80px;max-width:2820px;margin:0 auto}
.note{color:var(--mu);font-size:13.5px;margin:0 0 18px;padding:12px 16px;border:1px solid var(--bd);border-left:3px solid var(--ac);border-radius:8px;background:var(--sf)}
.cols{display:grid;grid-template-columns:1fr 1fr;gap:22px}
.pane{border:1px solid var(--bd);border-radius:12px;overflow:hidden;background:var(--sf)}
.pane h2{margin:0;font-size:12px;letter-spacing:.12em;text-transform:uppercase;color:var(--mu);padding:11px 16px;border-bottom:1px solid var(--bd);display:flex;justify-content:space-between;align-items:center}
.pane h2 .d{font-family:'JetBrains Mono',monospace;font-size:11px;color:var(--dim);letter-spacing:0;text-transform:none}
.pane img{display:block;width:100%;height:auto}
.tag{font-size:10px;padding:2px 7px;border-radius:5px;font-family:'JetBrains Mono',monospace}
.tag.m{color:var(--ac);border:1px solid var(--ac)}
.tag.r{color:var(--ok);border:1px solid var(--ok)}
.slider{position:relative;max-width:${WIDTH}px;margin:0 auto;border:1px solid var(--bd);border-radius:12px;overflow:hidden;user-select:none}
.slider img{display:block;width:100%;height:auto}
.slider .over{position:absolute;inset:0;overflow:hidden;width:50%;border-right:2px solid var(--ac)}
.slider .over img{position:absolute;top:0;left:0;height:auto}
.handle{position:absolute;top:0;bottom:0;width:2px;background:var(--ac);left:50%;cursor:ew-resize;z-index:5}
.handle::after{content:'⇄';position:absolute;top:14px;left:50%;transform:translateX(-50%);background:var(--ac);color:#03181b;font-size:12px;width:26px;height:26px;border-radius:50%;display:flex;align-items:center;justify-content:center}
.slabels{position:absolute;top:10px;left:12px;right:12px;display:flex;justify-content:space-between;pointer-events:none;z-index:6}
.single{max-width:${WIDTH}px;margin:0 auto;border:1px solid var(--bd);border-radius:12px;overflow:hidden}
.single img{display:block;width:100%}
.miss{padding:40px;text-align:center;color:var(--dim)}
@media(max-width:1200px){.cols{grid-template-columns:1fr}}
</style></head>
<body>
<header>
  <div class="htop">
    <div><h1><span class="dot"></span>OpenGitBase — Design Parity</h1><div class="sub">Terminal / Ice mockup vs. the live app · ${pairs}/${screens.length} pairs · captured ${stamp} · desktop ${WIDTH}px</div></div>
    <div class="modes" id="modes">
      <button class="mode on" data-mode="side">Side by side</button>
      <button class="mode" data-mode="slider">Overlay slider</button>
      <button class="mode" data-mode="mock">Mockup</button>
      <button class="mode" data-mode="real">Real</button>
    </div>
  </div>
  <div class="tabs" id="tabs"></div>
</header>
<main><p class="note" id="note"></p><div id="view"></div></main>
<script>
const SCREENS = ${JSON.stringify(screens)};
let cur = 0, mode = 'side';
const tabs = document.getElementById('tabs'), view = document.getElementById('view'), note = document.getElementById('note');
SCREENS.forEach((s,i)=>{const b=document.createElement('button');b.className='tab'+(i===0?' on':'');b.innerHTML=s.label+' <span class="r">'+s.route+'</span>';b.onclick=()=>{cur=i;render()};tabs.appendChild(b)});
document.querySelectorAll('.mode').forEach(m=>m.onclick=()=>{mode=m.dataset.mode;document.querySelectorAll('.mode').forEach(x=>x.classList.toggle('on',x===m));render()});
function pane(kind,s){const src=kind==='mock'?s.mock:s.real;const tag=kind==='mock'?'<span class="tag m">MOCKUP</span> target design':'<span class="tag r">REAL</span> live app';const d=kind==='mock'?s.mockDim:s.realDim;return '<div class="pane"><h2><span>'+tag+'</span><span class="d">'+d+'</span></h2>'+(src?'<img loading="lazy" src="'+src+'">':'<div class="miss">missing</div>')+'</div>'}
function render(){
  const s = SCREENS[cur];
  tabs.querySelectorAll('.tab').forEach((t,i)=>t.classList.toggle('on',i===cur));
  note.textContent = s.note;
  if(mode==='side') view.innerHTML='<div class="cols">'+pane('mock',s)+pane('real',s)+'</div>';
  else if(mode==='mock') view.innerHTML='<div class="single">'+(s.mock?'<img src="'+s.mock+'">':'<div class="miss">missing</div>')+'</div>';
  else if(mode==='real') view.innerHTML='<div class="single">'+(s.real?'<img src="'+s.real+'">':'<div class="miss">missing</div>')+'</div>';
  else {
    view.innerHTML='<div class="slider" id="sl"><div class="slabels"><span class="tag r">REAL ↓</span><span class="tag m">MOCKUP ↓</span></div>'
      +'<img id="base" src="'+s.real+'"><div class="over" id="over"><img id="ov" src="'+s.mock+'"></div><div class="handle" id="handle"></div></div>';
    initSlider();
  }
}
function initSlider(){
  const sl=document.getElementById('sl'), over=document.getElementById('over'), handle=document.getElementById('handle'), ov=document.getElementById('ov'), base=document.getElementById('base');
  function sync(){ ov.style.width = sl.clientWidth+'px'; }
  base.complete?sync():base.onload=sync; ov.onload=sync; sync();
  let drag=false;
  const move=(x)=>{const r=sl.getBoundingClientRect();let p=(x-r.left)/r.width;p=Math.max(0,Math.min(1,p));over.style.width=(p*100)+'%';handle.style.left=(p*100)+'%'};
  handle.onmousedown=()=>drag=true; window.addEventListener('mouseup',()=>drag=false);
  window.addEventListener('mousemove',e=>{if(drag)move(e.clientX)});
  sl.addEventListener('click',e=>{if(e.target!==handle)move(e.clientX)});
  window.addEventListener('resize',sync);
}
render();
</script>
</body></html>`
writeFileSync(join(REPORT, 'index.html'), html)
console.log('wrote report/index.html (' + pairs + '/' + screens.length + ' pairs' + (INLINE ? ', inlined' : '') + ')')
