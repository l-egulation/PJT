"""ExpandingFan review: timing candidates on the game clock (cleaned frames) and an in-game composite.

Game rules from main 7f2fbe8ed (docs/design/chapter2-rose-blue-boss.md pattern 5, data/combat/chapter2_rose_blue.json,
RoseBlue/Patterns.cs, Runtime/RoseBlueEncounter.Patterns.cs and .Presentation.cs): Blue teleports to the arena
centre; at 0 the aoe-whirl back/front sheets start at the centre floor (scale 2, 85 frames x 33.3 ms); at 0.5 the
field spawns and 60 p3 arrows circle it for 2.3 s (StepWhirlArrow, same hash); at 2.5 + 0.1 i six arrows form at
Blue feet + (0, 3.2 u) + 2 u on the upper 120 degree arc, aimed at the player; they fire at 3.0/3.5/3.6/4.1/4.2/4.3 s
toward the player at 20 u/s. 32 px per unit; the 30 u arena is the 960 px logical screen.
"""
import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
CAND = GROUP / 'preview/fan-candidates'
SIM = GROUP / 'preview/fan-sim'
TEMPLATE = GROUP / 'preview/rain-candidates'
VFX_SOURCE = ROOT / 'monsters/stage-02/boss-phase2-cutscene-20261106/preview/assets/vfx'
VFX_APPROVED = ROOT / 'monsters/stage-02/rose-blue-boss-vfx-20261105/runtime'

FRAMES = list(range(1, 32))
CUE_FRAMES = [6, 25]
CUES = [500, 3000]
CLENCH_START = 19
CUE_LABELS = ['광역 시작 (0.5초)', '첫 발사 (3.0초)']
ROLES = {**{f: 'coil' for f in range(1, 6)}, 6: 'hit', **{f: 'lock' for f in range(7, 19)},
         **{f: 'coil' for f in range(19, 24)}, 24: 'strike', 25: 'hit', **{f: 'lock' for f in range(26, 31)}, 31: 'hold'}
CLENCH = [120, 80, 80, 80, 80, 60]
HOLD = [150, 216, 216, 217, 217, 217, 217]
CANDIDATES = [
    {'id': 'A', 'name': '고르게 늘림', 'durationsMs': [180, 80, 80, 90, 70] + [140] + [155] * 12 + CLENCH + HOLD,
     'note': '01~31 전부. 팔을 뻗은 06이 0.5초, 주먹 19가 2.5초, 손바닥을 펴는 25가 3.0초 첫 발사. 천이 휘날리는 07~18을 155ms씩 고르게 늘림. 26~31 동안 나머지 화살이 발사됨.'},
    {'id': 'B', 'name': '빠른 분출 · 느린 폭풍', 'durationsMs': [240, 60, 60, 70, 70] + [100] + [80] * 5 + [214] * 5 + [215] * 2 + CLENCH + HOLD,
     'note': '같은 31장. 감기를 빠르게 하고, 천이 터지는 07~11을 80ms로 빠르게 지나간 뒤 12~18을 214ms씩 길게 머물러 폭풍 속에 서 있는 느낌을 늘림.'},
    # User: "빠른 분출은 좋으나 뒤에가 끊겨 프레임 추가 해도 될 듯". Same fast eruption as B; the 12-18 cloth stretch is
    # re-sequenced from existing frames (no new drawing) so it plays at about 90-100 ms instead of 214 ms.
    {'id': 'B2', 'name': 'B · 뒤쪽 왕복으로 촘촘하게',
     'sourceFrames': list(range(1, 12)) + list(range(12, 19)) + list(range(17, 12, -1)) + list(range(14, 19)) + list(range(19, 32)),
     'durationsMs': [240, 60, 60, 70, 70] + [100] + [80] * 5 + [88] * 16 + [92] + CLENCH + HOLD,
     'note': 'B의 빠른 분출 그대로. 12→18→13→18로 천이 앞뒤로 펄럭이게 이어 17장을 88ms씩. 이음매에서 튀지 않음(되돌아가는 장은 바로 앞 장과 같은 차이).'},
    {'id': 'B3', 'name': 'B · 뒤쪽 짧은 반복',
     'sourceFrames': list(range(1, 12)) + list(range(12, 19)) + list(range(15, 19)) * 2 + list(range(19, 32)),
     'durationsMs': [240, 60, 60, 70, 70] + [100] + [80] * 5 + [100] * 15 + CLENCH + HOLD,
     'note': 'B의 빠른 분출 그대로. 12~18 뒤에 15~18을 두 번 더 돌려 15장을 100ms씩. 앞으로만 흐르지만 18→15 이음매가 보통 장 사이보다 약 1.6배 크게 바뀜.'},
    # User: "ㅇㅋ 좀 자연스러워졌으나 주먹쪽이 또 끊기네 추가혀". B2 + synthesised in-betweens (x.5 = between x and x+1,
    # tools/build_stage2_fan_tweens.py) across 18->24; 24->25 stays a cut for the release snap.
    {'id': 'B4', 'name': 'B2 · 주먹 사이 장 추가',
     'sourceFrames': list(range(1, 12)) + list(range(12, 19)) + list(range(17, 12, -1)) + list(range(14, 19)) + [18.5, 19, 19.5, 20, 20.5, 21, 21.5, 22, 22.5, 23, 23.5, 24] + list(range(25, 32)),
     'durationsMs': [240, 60, 60, 70, 70] + [100] + [80] * 5 + [85] * 16 + [80] + [60] + [46] * 10 + [40] + HOLD,
     'note': 'B2와 같은 분출·폭풍. 18→24 사이에 합성 중간 장 6장(앞뒤 장 윤곽의 가운데, 원본 픽셀만 사용)을 넣어 주먹 쥐는 동안 천이 솟는 흐름을 촘촘하게. 19는 2.5초 그대로, 주먹 구간 11장을 약 46ms씩.'},
    {'id': 'B5', 'name': 'B2 · 주먹 일찍·천천히',
     'sourceFrames': list(range(1, 12)) + list(range(12, 19)) + [17, 16, 15, 16, 17, 18] + [18.5, 19, 19.5, 20, 20.5, 21, 21.5, 22, 22.5, 23, 23.5, 24] + list(range(25, 32)),
     'durationsMs': [240, 60, 60, 70, 70] + [100] + [80] * 5 + [88] * 12 + [84] + [60] + [73] * 10 + [70] + HOLD,
     'note': '같은 중간 장 6장. 주먹을 2.2초부터 쥐기 시작해 11장을 약 73ms씩 떨리듯 천천히. 폭풍 왕복은 그만큼 짧게(13장). 3.0초 손바닥 펴기는 그대로.'},
]
VFX = {
    'p3-arrow-flight': {'columns': 4, 'rows': 1, 'frames': 4, 'cellWidth': 96, 'cellHeight': 28, 'pivotX': 64, 'pivotY': 14, 'durationsMs': [50] * 4, 'loop': True, 'scale': 1},
    'aoe-whirl-back': {'columns': 8, 'rows': 11, 'frames': 85, 'cellWidth': 440, 'cellHeight': 268, 'pivotX': 220, 'pivotY': 254, 'durationsMs': [100 / 3] * 85, 'loop': False, 'scale': 2},
    'aoe-whirl-front': {'columns': 8, 'rows': 11, 'frames': 85, 'cellWidth': 440, 'cellHeight': 268, 'pivotX': 220, 'pivotY': 254, 'durationsMs': [100 / 3] * 85, 'loop': False, 'scale': 2},
}


def lfs_oid(path):
    for line in path.read_text(encoding='utf-8', errors='ignore').splitlines():
        if line.startswith('oid sha256:'):
            return line.split(':', 1)[1].strip()
    return hashlib.sha256(path.read_bytes()).hexdigest()


def candidates_page():
    clean = json.loads((CAND / 'clean-all.json').read_text(encoding='utf-8'))
    for c in CANDIDATES:
        d, frames = c['durationsMs'], c.get('sourceFrames', FRAMES)
        assert len(d) == len(frames), (c['id'], len(d), len(frames))
        starts = [sum(d[:i]) for i in range(len(d))]
        assert [starts[frames.index(f)] for f in CUE_FRAMES] == CUES, (c['id'], [starts[frames.index(f)] for f in CUE_FRAMES])
        c.update(sourceFrames=frames, roles=[ROLES[int(f)] for f in frames], frameCount=len(frames), totalMs=sum(d),
                 clenchStartMs=starts[frames.index(CLENCH_START)], synthesisedTweens=sum(1 for f in frames if f != int(f)),
                 releaseCuesMs=CUES, repeat=False, holdLastFrame=True, clean=True,
                 frameUrls=[f'clean/{int(f):02}.png' if f == int(f) else f'clean/t{int(f):02}-{int(f) + 1:02}.png' for f in frames])
    data = {'format': 'stage2-fan-timing-candidates-v1', 'status': 'user-selected-B4', 'selected': 'B4', 'runtime': '../../runtime/fan-final/motion.json', 'pattern': 'ExpandingFan',
            'source': '../../source/fan-aether-03/aether-original.png', 'prompt': '../../source/fan-aether/prompt.txt (v4)', 'cleanFrames': 'clean-all.json',
            'frameBase': '../../source/fan-aether-03/frames/', 'logicalCanvas': [384, 256],
            'commonTransform': {'destinationSize': clean['transform']['destinationSize'], 'rasterOffset': clean['transform']['rasterOffset']},
            'previewMatte': {'whiteMinimum': 250, 'maximumChannelDifference': 4},
            'cueFrames': CUE_FRAMES, 'releaseCuesMs': CUES, 'cueLabels': CUE_LABELS, 'visualDirections': ['spread', 'release'], 'tweens': 'tweens.json', 'frameIdNote': 'x.5 = synthesised in-between of x and x+1 (clean/tXX-YY.png)',
            'gameTimeline': {'tell': 0, 'fieldSpawn': 0.5, 'fieldFull': 2.5, 'arrowsForm': [2.5, 3.0], 'fire': [3.0, 3.5, 3.6, 4.1, 4.2, 4.3]},
            'candidates': CANDIDATES}
    (CAND / 'candidates.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    js = (TEMPLATE / 'candidates.js').read_text(encoding='utf-8')
    js = js.replace("입을 지운 가면과 승인 석궁이 적용된 투명 PNG입니다.", "배경제거·승인 석궁·발 정리를 마친 투명 PNG입니다.")
    (CAND / 'candidates.js').write_text(js, encoding='utf-8')
    tpl = (TEMPLATE / 'index.html').read_text(encoding='utf-8')
    style = tpl[tpl.index('<style>'):tpl.index('</style>') + 8]
    controls = tpl[tpl.index('<div class="controls"><button id="play">'):tpl.index('<div id="choices"')].replace('max="3110"', 'max="4450"')
    sections = tpl[tpl.index('<section><h2>승인 기본과 비교'):tpl.index('<p id="status"')].replace('화살비 · 같은 축척', '광역기 · 같은 축척')
    html = f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>광역기 타이밍 비교</title><link rel="stylesheet" href="../review.css">{style}
<main><p class="eyebrow">STAGE 02 / PHASE 03 / EXPANDING FAN / TIMING REVIEW</p><h1>확장 광역기 · 타이밍 비교</h1><p class="notice">사용자 확정: <strong>B4 (주먹 사이 장 추가)</strong>. 게임용 결과는 <a href="../../runtime/fan-final/sheet.png" target="_blank">runtime/fan-final</a>. Unity 적용 전입니다.</p>
<p>프롬프트 v4 결과(Aether 원본 3840×2160 · 6×6)의 01~31을 씁니다. 32~35는 기본 자세로 돌아가는 장이라 뺐습니다. 06에서 팔을 뻗어 0.5초 광역 시작, 19에서 주먹을 쥐어 2.5초 화살 생성, 25에서 손바닥을 확 펴 3.0초 첫 발사에 맞춥니다. 끝은 편 손바닥 그대로입니다.</p>
<p class="notice">배경제거·외곽 정리·승인 석궁·발 정리를 마친 투명 PNG입니다. 뒷부분(25~31) 편 손을 손목 기준 0.7배로 줄였고 외곽을 한 번 더 다듬었습니다. 이번 결과는 확대되지 않았고(5연사와 같은 1.04배) 가면에 입이 없어 생성된 가면을 그대로 썼습니다. 고른 뒤 확정본으로 내보냅니다.</p>
<nav><a href="../fan-sim/">실제 광역기와 함께 보기</a><a href="../fan-input/">입력·프롬프트</a><a href="../../source/fan-aether-03/aether-original.png" target="_blank">원본 시트</a><a href="../../source/fan-aether-03/inspection-contact.png" target="_blank">36장 한눈에</a><a href="all-clean-black.png" target="_blank">정리본 31장 · 검정</a><a href="candidates.json" target="_blank">프레임·시간표</a></nav>
{controls}<p class="legend">막대 색:<i class="coil"></i>감기·주먹<i class="strike"></i>내지르기 직전<i class="hit"></i>정점<i class="lock"></i>유지<i class="hold"></i>끝. 주황 세로선은 0.5초 광역 시작, 2.5초 화살 생성, 3.0초 첫 발사입니다.</p>
<div id="choices" class="choices"></div>
{sections}<p id="status" role="status">프레임 로딩 중…</p></main><script type="module" src="candidates.js"></script></html>'''
    (CAND / 'index.html').write_text(html, encoding='utf-8')


def sim_page():
    (SIM / 'assets').mkdir(parents=True, exist_ok=True)
    for name, spec in VFX.items():
        src = VFX_SOURCE / f'{name}.png'
        digest = hashlib.sha256(src.read_bytes()).hexdigest()
        assert digest == lfs_oid(VFX_APPROVED / f'{name}.png'), name
        shutil.copy2(src, SIM / f'assets/{name}.png')
        spec.update(file=f'assets/{name}.png', sha256=digest, approvedRuntime=f'monsters/stage-02/rose-blue-boss-vfx-20261105/runtime/{name}.png')
    cands = json.loads((CAND / 'candidates.json').read_text(encoding='utf-8'))
    data = {'format': 'stage2-fan-sim-v1',
            'source': 'main 7f2fbe8ed: chapter2-rose-blue-boss.md pattern 5, chapter2_rose_blue.json, RoseBlue/Patterns.cs, Runtime/RoseBlueEncounter.Patterns.cs, Presentation.cs (StartWhirl, StepWhirlArrow)',
            'pixelsPerUnit': 32, 'screen': [960, 540], 'floorY': 445, 'arenaTopUnitsAboveFloor': 15,
            'fan': {'tell': 0.5, 'expand': 2.0, 'width': 24.375, 'count': 6, 'createGap': 0.1, 'radius': 2, 'arc': 120, 'speed': 20,
                    'fire': [3.0, 3.5, 3.6, 4.1, 4.2, 4.3], 'blueHeightUnits': 3.2, 'whirlArrows': 60},
            'positions': {'playerX': 180, 'bossX': 480, 'note': 'Blue teleports to the arena centre; the player position is an example.'},
            'warning': 'the fan-area tell glow is approximated as a thin violet floor line',
            'sheets': VFX, 'bodyFrames': '../fan-candidates/',
            'candidates': [{k: c[k] for k in ('id', 'name', 'sourceFrames', 'durationsMs', 'frameUrls', 'totalMs')} for c in cands['candidates']],
            'totalMs': 5000}
    (SIM / 'sim.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (SIM / 'sim.js').write_text(JS, encoding='utf-8')
    (SIM / 'index.html').write_text(HTML, encoding='utf-8')


JS = r'''const $=s=>document.querySelector(s),load=p=>new Promise((ok,no)=>{const i=new Image();i.onload=()=>ok(i);i.onerror=()=>no(new Error('Image failed: '+p));i.src=p;});
window.fanSim={ready:false,errors:[]};
try{
 const D=await (await fetch('sim.json')).json();const U=D.pixelsPerUnit,F=D.floorY,Z=D.fan,BX=D.positions.bossX,PX=D.positions.playerX;
 const [room,player]=await Promise.all(['../assets/room.png','../assets/player.png'].map(load));
 const sheet={};for(const [k,s] of Object.entries(D.sheets))sheet[k]={...s,img:await load(s.file)};
 for(const c of D.candidates)c.imgs=await Promise.all(c.frameUrls.map(u=>load(D.bodyFrames+u)));
 const hash=(i,k)=>{const v=Math.sin((i*7.31+k*10.48)*12.9898)*43758.5453;return v-Math.floor(v);};
 function frameOf(s,t){const ms=t*1000,total=s.durationsMs.reduce((a,b)=>a+b,0);if(!s.loop&&ms>=total)return -1;let u=s.loop?ms%total:ms;for(let i=0;i<s.frames;i++){if(u<s.durationsMs[i])return i;u-=s.durationsMs[i];}return s.frames-1;}
 function drawSheet(ctx,name,t,x,y,rot=0,scale=1,alpha=1,flipY=false){const s=sheet[name],f=frameOf(s,t);if(f<0)return;const sx=f%s.columns*s.cellWidth,sy=Math.floor(f/s.columns)*s.cellHeight;ctx.save();ctx.globalAlpha=alpha;ctx.translate(x,y);ctx.rotate(rot);ctx.scale(scale,flipY?-scale:scale);ctx.drawImage(s.img,sx,sy,s.cellWidth,s.cellHeight,-s.pivotX,-s.pivotY,s.cellWidth,s.cellHeight);ctx.restore();}
 function bodyFrame(c,t){let ms=t*1000;if(ms>=c.totalMs)return c.imgs.length-1;for(let i=0;i<c.durationsMs.length;i++){if(ms<c.durationsMs[i])return i;ms-=c.durationsMs[i];}return c.imgs.length-1;}
 const target={x:PX,y:F-32};
 function fanAt(i){const a=(90-Z.arc/2+i*Z.arc/(Z.count-1))*Math.PI/180;return{x:BX+Math.cos(a)*Z.radius*U,y:F-Z.blueHeightUnits*U-Math.sin(a)*Z.radius*U};}
 function whirlArrows(t,back){const age=t-Z.tell,out=[];if(age<0||age>Z.expand+.3)return out;const g=Math.min(1,Math.max(0,age/Z.expand)),end=Math.min(1,Math.max(0,(age-Z.expand)/.3));if(g<=0)return out;
  for(let i=0;i<Z.whirlArrows;i++){const tt=age+Z.tell,hw=Z.width/2*g,hh=D.arenaTopUnitsAboveFloor*g,yf=.04+.92*hash(i,1),speed=5+4.5*hash(i,2);const a=hash(i,3)*Math.PI*2+tt*speed,r=hw*(.5+.5*yf)*(.75+.45*hash(i,4))*(1+end*.8);
   const y=hh*yf+Math.sin(tt*(2+3*hash(i,5))+i)*.5*g;const isBack=Math.sin(a)>0;if(isBack!==back)continue;const ux=r*Math.cos(a),uy=y+.45*Math.sin(a)*(.6+.4*g);
   const ang=Math.atan2(.45*Math.cos(a),-Math.sin(a)*Math.max(r,.01))+(hash(i,7)-.5)*.5;const sc=(.75+.6*hash(i,6))*(isBack?.8:1.1);
   out.push({x:BX+ux*U,y:F-uy*U,rot:-ang,scale:sc,alpha:(isBack?.5:1)*(1-end),age:age+i*.05,flipY:Math.cos(ang)<0});}
  return out;}
 function phase(t){if(t<Z.tell)return'전조';if(t<Z.tell+Z.expand)return'광역 확장';if(t<Z.fire[0])return`화살 생성 ${Math.min(6,Math.floor((t-2.5)/.1)+1)}/6`;const n=Z.fire.filter(x=>t>=x).length;return n<6||t<Z.fire[5]+.6?`발사 ${n}/6`:'끝';}
 function draw(card){const {ctx,c,state}=card,t=state.time;ctx.imageSmoothingEnabled=false;ctx.drawImage(room,0,0);
  if(t<Z.tell){ctx.fillStyle='rgba(155,99,224,.7)';ctx.fillRect(BX-Z.width*U/2,F-3,Z.width*U,3);}
  drawSheet(ctx,'aoe-whirl-back',t,BX,F,0,2);
  for(const w of whirlArrows(t,true))drawSheet(ctx,'p3-arrow-flight',w.age,w.x,w.y,w.rot,w.scale,w.alpha,w.flipY);
  const fi=bodyFrame(c,t);ctx.drawImage(c.imgs[fi],BX-192,F-232);ctx.drawImage(player,PX-192,F-232);
  drawSheet(ctx,'aoe-whirl-front',t,BX,F,0,2);
  for(const w of whirlArrows(t,false))drawSheet(ctx,'p3-arrow-flight',w.age,w.x,w.y,w.rot,w.scale,w.alpha,w.flipY);
  for(let i=0;i<Z.count;i++){const form=2.5+i*Z.createGap;if(t<form)continue;const p=fanAt(i);const fire=Z.fire[i];let x=p.x,y=p.y,dx=target.x-p.x,dy=target.y-p.y;const len=Math.hypot(dx,dy);dx/=len;dy/=len;
   if(t>=fire){const d=(t-fire)*Z.speed*U;x+=dx*d;y+=dy*d;if(x<-60||x>1020||y>600||y<-60)continue;}
   drawSheet(ctx,'p3-arrow-flight',t-form,x,y,Math.atan2(dy,dx),1,1,dx<0);}
  card.info.textContent=`${t.toFixed(2)}초 · ${phase(t)} · 몸 원본 ${String(c.sourceFrames[fi]).padStart(2,'0')}${t*1000>=c.totalMs?' (끝 자세 유지)':''} · ${state.playing?'재생 중':'정지'}`;
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
 window.fanSim={ready:true,errors:[],cards:cards.length,renderAt:t=>cards.map(c=>{c.state.playing=false;c.state.time=t;draw(c);return c.info.textContent;})};
 $('#status').textContent=`몸 ${D.candidates.map(c=>c.imgs.length).join('·')}장, 회오리·화살 시트 3종 로딩 완료.`;
}catch(e){window.fanSim.errors.push(String(e));$('#status').textContent=String(e);$('#status').className='errors';throw e;}
'''

HTML = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>광역기 실제 연출</title><link rel="stylesheet" href="../review.css">
<style>main{max-width:1040px}.card{background:#211b2a;border:1px solid #5d4c70;padding:14px;margin-top:18px}.card canvas{width:100%;height:auto;image-rendering:pixelated;display:block}.local-controls{display:flex;gap:6px;flex-wrap:wrap;margin:10px 0}.local-controls button{padding:6px 9px;font-size:13px}.local-seek{display:flex;width:100%}.local-seek input{flex:1}.number{font:13px ui-monospace,monospace;color:#e5cda6}.errors{color:#ffa6a6}</style>
<main><p class="eyebrow">STAGE 02 / PHASE 03 / EXPANDING FAN / IN-GAME TIMING</p><h1>확장 광역기 · 실제 연출과 함께 보기</h1><p class="notice">사용자 확정: <strong>B4 (주먹 사이 장 추가)</strong>. 게임용 결과는 <a href="../../runtime/fan-final/sheet.png" target="_blank">runtime/fan-final</a>. Unity 적용 전입니다.</p>
<p>몸 동작 A·B를 게임 광역기 시간표와 같은 시계로 재생합니다. 0초 중앙 순간이동·회오리 시작 → 0.5~2.5초 광역 확장(회오리를 도는 화살 60개) → 2.5초부터 0.1초 간격으로 머리 위 화살 6개 생성 → 3.0 / 3.5·3.6 / 4.1·4.2·4.3초에 플레이어를 향해 발사.</p>
<p class="notice">에셋 합성 미리보기입니다. 회오리 앞뒤 시트와 화살은 확정 VFX 시트(<code>aoe-whirl-back/front</code>, <code>p3-arrow-flight</code>) 그대로이고, 회오리를 도는 화살 60개는 게임 코드의 계산식을 그대로 옮겼습니다. 32px=1유닛, 보스는 1배 크기로 중앙에 둡니다. 광역 전조 빛은 얇은 보라 바닥 선으로 비슷하게 그렸습니다. Unity 화면이 아닙니다.</p>
<nav><a href="../fan-candidates/">몸 동작만 비교</a><a href="../fan-input/">입력·프롬프트</a><a href="sim.json" target="_blank">시간표·배치</a></nav>
<div class="controls" style="margin-top:14px"><button id="playAll">둘 다 처음부터</button><button id="pauseAll">일시정지</button><label>속도 <select id="speed"><option value="0.25">0.25배</option><option value="0.5">0.5배</option><option value="1" selected>1배</option></select></label></div>
<div id="cards"></div><p id="status" role="status">로딩 중…</p></main><script type="module" src="sim.js"></script></html>'''


def main():
    candidates_page()
    sim_page()
    for c in CANDIDATES:
        print(c['id'], c['totalMs'])


if __name__ == '__main__':
    main()
