"""Composite review: the ArrowRain body timing candidates with the game's arrow rain, on the game clock.

Game rules from main 7f2fbe8ed (data/combat/chapter2_rose_blue.json, RoseBlue/Patterns.cs,
HazardRules.RainX, Runtime/RoseBlueEncounter.Patterns.cs and .Presentation.cs):
tell at 0, 12 arrows spawn 0.5 + i*1.5/11 s from the arena top and fall 0.25 s, land at
floor+0.5u, blast 0.2 s after the last landing in the same order, p3-pillar sheet from the
floor. Arrow centres 1.25 + 2.5*i u from the start wall. VFX sheets: 32 px per unit; the
arena's 30 u width is the 960 px logical screen. The start warning is drawn as an
approximate violet floor glow (the game uses a procedural glow sprite).
"""
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
OUT = GROUP / 'preview/rain-sim'
VFX_SOURCE = ROOT / 'monsters/stage-02/boss-phase2-cutscene-20261106/preview/assets/vfx'
VFX_APPROVED = ROOT / 'monsters/stage-02/rose-blue-boss-vfx-20261105/runtime'
SHEETS = {
    'p3-arrow-flight': {'columns': 4, 'rows': 1, 'frames': 4, 'cellWidth': 96, 'cellHeight': 28, 'pivotX': 64, 'pivotY': 14, 'durationsMs': [50] * 4, 'loop': True},
    'p3-pillar': {'columns': 7, 'rows': 3, 'frames': 18, 'cellWidth': 208, 'cellHeight': 488, 'pivotX': 104, 'pivotY': 478, 'durationsMs': [100 / 3] * 18, 'loop': False},
}


def lfs_oid(path):
    for line in path.read_text(encoding='utf-8', errors='ignore').splitlines():
        if line.startswith('oid sha256:'):
            return line.split(':', 1)[1].strip()
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    (OUT / 'assets').mkdir(parents=True, exist_ok=True)
    for name in SHEETS:
        src = VFX_SOURCE / f'{name}.png'
        digest = hashlib.sha256(src.read_bytes()).hexdigest()
        assert digest == lfs_oid(VFX_APPROVED / f'{name}.png'), name
        shutil.copy2(src, OUT / f'assets/{name}.png')
        SHEETS[name].update(file=f'assets/{name}.png', sha256=digest, approvedRuntime=f'monsters/stage-02/rose-blue-boss-vfx-20261105/runtime/{name}.png')
    cands = json.loads((GROUP / 'preview/rain-candidates/candidates.json').read_text(encoding='utf-8'))
    data = {
        'format': 'stage2-rain-sim-v1',
        'source': 'main 7f2fbe8ed: data/combat/chapter2_rose_blue.json, RoseBlue/Patterns.cs, HazardRules.RainX, Runtime/RoseBlueEncounter.Patterns.cs, Presentation.cs',
        'pixelsPerUnit': 32, 'screen': [960, 540], 'floorY': 445, 'arenaWidthUnits': 30, 'arenaTopUnitsAboveFloor': 15,
        'rain': {'count': 12, 'tell': 0.5, 'fall': 0.25, 'landingSpan': 1.5, 'fuse': 0.2, 'firstCentreUnits': 1.25, 'pitchUnits': 2.5,
                 'landedCentreAboveFloorUnits': 0.5, 'explosionWidthUnits': 2.5, 'explosionHeightUnits': 15, 'explosionActive': 0.15},
        'warning': {'until': 0.5, 'approximation': 'violet floor glow 0.4x0.8 u; game uses a procedural glow sprite tinted (.61,.39,.88,.9)'},
        'positions': {'playerX': 180, 'bossX': 620, 'note': 'Blue stays where it stands for this pattern; this x is an example.'},
        'sheets': SHEETS,
        'bodyFrames': '../rain-candidates/',
        'candidates': [{k: c[k] for k in ('id', 'name', 'sourceFrames', 'durationsMs', 'frameUrls', 'totalMs')} for c in cands['candidates']],
        'totalMs': 4800,
    }
    (OUT / 'sim.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (OUT / 'sim.js').write_text(JS, encoding='utf-8')
    (OUT / 'index.html').write_text(HTML, encoding='utf-8')


JS = r'''const $=s=>document.querySelector(s),load=p=>new Promise((ok,no)=>{const i=new Image();i.onload=()=>ok(i);i.onerror=()=>no(new Error('Image failed: '+p));i.src=p;});
window.rainSim={ready:false,errors:[]};
try{
 const D=await (await fetch('sim.json')).json();const U=D.pixelsPerUnit,F=D.floorY,R=D.rain;
 const [room,player,idle]=await Promise.all(['../assets/room.png','../assets/player.png','../assets/fused-idle.png'].map(load));
 const sheet={};for(const [k,s] of Object.entries(D.sheets))sheet[k]={...s,img:await load(s.file)};
 for(const c of D.candidates)c.imgs=await Promise.all(c.frameUrls.map(u=>load(D.bodyFrames+u)));
 const step=R.landingSpan/(R.count-1);
 const arrows=fromRight=>Array.from({length:R.count},(_,i)=>{const k=fromRight?R.count-1-i:i;return{x:(R.firstCentreUnits+k*R.pitchUnits)*U,spawn:R.tell+i*step,blast:R.tell+R.fall+R.landingSpan+R.fuse+i*step};});
 function sheetFrame(s,t){const ms=t*1000,total=s.durationsMs.reduce((a,b)=>a+b,0);let u=s.loop?ms%total:Math.min(ms,total-0.001);for(let i=0;i<s.frames;i++){if(u<s.durationsMs[i])return i;u-=s.durationsMs[i];}return s.frames-1;}
 function drawSheet(ctx,name,t,x,y,rot=0){const s=sheet[name],f=sheetFrame(s,t),sx=f%s.columns*s.cellWidth,sy=Math.floor(f/s.columns)*s.cellHeight;ctx.save();ctx.translate(x,y);ctx.rotate(rot);ctx.drawImage(s.img,sx,sy,s.cellWidth,s.cellHeight,-s.pivotX,-s.pivotY,s.cellWidth,s.cellHeight);ctx.restore();}
 function bodyFrame(c,t){let ms=t*1000;if(ms>=c.totalMs)return c.imgs.length-1;for(let i=0;i<c.durationsMs.length;i++){if(ms<c.durationsMs[i])return i;ms-=c.durationsMs[i];}return c.imgs.length-1;}
 function phase(t){if(t<R.tell)return'경고';const a=arrows(false);const land=a[R.count-1].spawn+R.fall;if(t<land)return`낙하 ${a.filter(x=>t>=x.spawn).length}/12`;if(t<a[0].blast)return'박힘 · 폭발 대기';if(t<a[R.count-1].blast+R.explosionActive)return`폭발 ${a.filter(x=>t>=x.blast).length}/12`;return'끝';}
 function draw(card){const {ctx,c,state}=card,t=state.time,fromRight=$('#side').value==='right',bx=D.positions.bossX,px=D.positions.playerX;
  ctx.imageSmoothingEnabled=false;ctx.drawImage(room,0,0);ctx.drawImage(player,px-192,F-232);
  const fi=bodyFrame(c,t);ctx.drawImage(c.imgs[fi],bx-192,F-232);
  const list=arrows(fromRight);
  if(t<D.warning.until){const x=list[0].x,g=ctx.createRadialGradient(x,F-6,1,x,F-6,16);g.addColorStop(0,'rgba(196,150,255,.95)');g.addColorStop(1,'rgba(155,99,224,0)');ctx.fillStyle=g;ctx.fillRect(x-14,F-28,28,30);}
  const top=F-D.arenaTopUnitsAboveFloor*U,landY=F-R.landedCentreAboveFloorUnits*U;
  for(const a of list){if(t<a.spawn||t>=a.blast)continue;const u=Math.min(1,(t-a.spawn)/R.fall),y=top+(landY-top)*u;drawSheet(ctx,'p3-arrow-flight',t-a.spawn,a.x,y,Math.PI/2);}
  for(const a of list){const age=t-a.blast;if(age<0)continue;const s=sheet['p3-pillar'],dur=s.durationsMs.reduce((x,y)=>x+y,0)/1000;if(age<dur)drawSheet(ctx,'p3-pillar',age,a.x,F);}
  if($('#hit').checked){ctx.strokeStyle='rgba(255,80,80,.9)';ctx.lineWidth=1;for(const a of list){if(t>=a.spawn&&t<a.blast){const u=Math.min(1,(t-a.spawn)/R.fall),y=top+(landY-top)*u;ctx.strokeRect(a.x-.15*U,y-.5*U,.3*U,U);}if(t>=a.blast&&t<a.blast+R.explosionActive){ctx.strokeRect(a.x-R.explosionWidthUnits*U/2,F-R.explosionHeightUnits*U,R.explosionWidthUnits*U,R.explosionHeightUnits*U);}}}
  card.info.textContent=`${(t).toFixed(2)}초 · ${phase(t)} · 몸 원본 ${String(c.sourceFrames[fi]).padStart(2,'0')}${t*1000>=c.totalMs?' (끝 자세 유지)':''} · ${state.playing?'재생 중':'정지'}`;
  card.seek.value=Math.round(t*1000);card.toggle.textContent=state.playing?'Ⅱ 일시정지':'▶ 재생';}
 const cards=D.candidates.map(c=>{const el=document.createElement('article');el.className='card';el.innerHTML=`<h2>${c.id}. ${c.name}</h2><canvas width="960" height="540"></canvas><p class="number info"></p><div class="local-controls"><button class="toggle">▶ 재생</button><button class="restart">↺ 처음부터</button><button class="back">◀ 0.05초</button><button class="fwd">0.05초 ▶</button><label><input class="loop" type="checkbox"> 반복</label></div><label class="local-seek">시간 <input type="range" min="0" max="${D.totalMs}" step="1" value="0"></label>`;$('#cards').append(el);
  const card={el,c,ctx:el.querySelector('canvas').getContext('2d'),info:el.querySelector('.info'),seek:el.querySelector('input[type=range]'),toggle:el.querySelector('.toggle'),state:{time:0,playing:false,loop:false}};
  card.toggle.onclick=()=>{if(card.state.playing)card.state.playing=false;else{for(const o of cards)o.state.playing=false;if(card.state.time*1000>=D.totalMs)card.state.time=0;card.state.playing=true;}};
  el.querySelector('.restart').onclick=()=>{for(const o of cards)o.state.playing=false;card.state.time=0;card.state.playing=true;};
  el.querySelector('.back').onclick=()=>{card.state.playing=false;card.state.time=Math.max(0,card.state.time-.05);};
  el.querySelector('.fwd').onclick=()=>{card.state.playing=false;card.state.time=Math.min(D.totalMs/1000,card.state.time+.05);};
  el.querySelector('.loop').onchange=e=>card.state.loop=e.target.checked;
  card.seek.oninput=e=>{card.state.playing=false;card.state.time=Number(e.target.value)/1000;};
  return card;});
 $('#playAll').onclick=()=>{for(const o of cards){o.state.time=0;o.state.playing=true;}};
 $('#pauseAll').onclick=()=>{for(const o of cards)o.state.playing=false;};
 let last=performance.now();
 function tick(now){const dt=(now-last)/1000*Number($('#speed').value);last=now;for(const card of cards){const s=card.state;if(s.playing){s.time+=dt;if(s.time*1000>=D.totalMs){if(s.loop)s.time=0;else{s.time=D.totalMs/1000;s.playing=false;}}}draw(card);}requestAnimationFrame(tick);}
 requestAnimationFrame(tick);
 window.rainSim={ready:true,errors:[],cards:cards.length,state:()=>cards.map(c=>({id:c.c.id,time:c.state.time,playing:c.state.playing,info:c.info.textContent})),renderAt:t=>cards.map(c=>{c.state.playing=false;c.state.time=t;draw(c);return c.info.textContent;})};
 $('#status').textContent=`몸 ${D.candidates.map(c=>c.imgs.length).join('·')}장, 화살·폭발 시트 2종 로딩 완료.`;
}catch(e){window.rainSim.errors.push(String(e));$('#status').textContent=String(e);$('#status').className='errors';throw e;}
'''

HTML = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>화살비 실제 연출</title><link rel="stylesheet" href="../review.css">
<style>main{max-width:1040px}.card{background:#211b2a;border:1px solid #5d4c70;padding:14px;margin-top:18px}.card canvas{width:100%;height:auto;image-rendering:pixelated;display:block}.local-controls{display:flex;gap:6px;flex-wrap:wrap;margin:10px 0}.local-controls button{padding:6px 9px;font-size:13px}.local-seek{display:flex;width:100%}.local-seek input{flex:1}.number{font:13px ui-monospace,monospace;color:#e5cda6}.errors{color:#ffa6a6}</style>
<main><p class="eyebrow">STAGE 02 / PHASE 03 / ARROW RAIN / IN-GAME TIMING</p><h1>화살비 · 실제 연출과 함께 보기</h1><p class="notice">사용자 확정: <strong>A-x2 (앞부분 2배속)</strong>. 게임용 결과는 <a href="../../runtime/rain-a-x2/sheet.png" target="_blank">runtime/rain-a-x2</a>. Unity 적용 전입니다.</p>
<p>몸 동작 A·B를 게임의 화살비 시간표와 같은 시계로 재생합니다. 0초 시작 쪽 경고 → 0.5초부터 12발이 1.5/11초 간격으로 전장 위에서 0.25초 동안 떨어져 바닥에 박힘 → 2.45초부터 같은 순서로 폭 2.5·높이 15유닛 기둥 폭발(마지막 3.95초).</p>
<p class="notice">에셋 합성 미리보기입니다. 화살과 폭발 기둥은 확정 VFX 시트(<code>p3-arrow-flight</code>, <code>p3-pillar</code>) 그대로이고 32px=1유닛, 전장 30유닛=화면 960px입니다. 시작 쪽 경고는 게임의 절차 생성 빛을 비슷하게 그린 것입니다. 보스 위치는 예시이며 Unity 화면이 아닙니다.</p>
<nav><a href="../rain-candidates/">몸 동작만 비교</a><a href="../rain-input/">입력·프롬프트</a><a href="sim.json" target="_blank">시간표·배치</a></nav>
<div class="controls" style="margin-top:14px"><button id="playAll">둘 다 처음부터</button><button id="pauseAll">일시정지</button><label>시작 쪽 <select id="side"><option value="left">왼쪽부터</option><option value="right">오른쪽부터</option></select></label><label>속도 <select id="speed"><option value="0.25">0.25배</option><option value="0.5">0.5배</option><option value="1" selected>1배</option></select></label><label><input id="hit" type="checkbox"> 판정 상자</label></div>
<div id="cards"></div><p id="status" role="status">로딩 중…</p></main><script type="module" src="sim.js"></script></html>'''

if __name__ == '__main__':
    main()
