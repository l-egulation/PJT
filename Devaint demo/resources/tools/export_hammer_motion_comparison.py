"""Read actual Unity sprite bindings and export a self-contained review HTML."""
from pathlib import Path
import argparse,base64,hashlib,io,json,math,re,subprocess
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parent
RES=REPO/'unity/Assets/Deviant/Combat/Resources'
SET='user-hammer-basic-color-motion-20261101'
SKILL_SET='user-hammer-skill-cleaned-20261101'
ASSET=ROOT/'characters/ink/weapons/hammer'

HTML=r'''<!doctype html>
<html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>망치 1타·2타 — Unity 적용본 / 새 보정본</title>
<style>
:root{color-scheme:dark;font:15px/1.5 system-ui,'Malgun Gothic',sans-serif;background:#14191e;color:#e9edf1}*{box-sizing:border-box}body{margin:0}main{max-width:1540px;margin:auto;padding:24px}h1{font-size:24px;margin:0 0 5px}p{margin:4px 0;color:#aebbc6}.top{display:flex;gap:20px;justify-content:space-between;align-items:center}.badge{border:1px solid #3a625f;border-radius:30px;padding:6px 12px;color:#9cddd1;white-space:nowrap}.controls{display:flex;gap:10px;align-items:center;flex-wrap:wrap;padding:15px 0}.group{display:flex;gap:4px;background:#20272f;border-radius:9px;padding:4px}button,select{font:inherit;background:#293440;color:#e9edf1;border:1px solid #42505d;border-radius:6px;padding:7px 12px;cursor:pointer}button:hover{border-color:#83b6cb}button.active,button.primary{background:#17677a;border-color:#3e8fa1}label{display:flex;gap:7px;align-items:center;font-size:13px;color:#b7c6d1}input{accent-color:#56b8c6}#seek{width:100%;margin:12px 0}.clock{display:flex;justify-content:space-between;font-variant-numeric:tabular-nums;color:#aebbc6;font-size:13px}.lanes{display:grid;grid-template-columns:1fr 1fr;gap:18px}.lane{background:#1d242c;border:1px solid #34404a;border-radius:12px;overflow:hidden}.lane header{padding:13px 16px;display:flex;justify-content:space-between;align-items:center}h2{font-size:18px;margin:0}.tag{font-size:12px;color:#c5b694}.lane.new .tag{color:#80c5cf}canvas{width:100%;display:block;aspect-ratio:7/5}.info{display:flex;justify-content:space-between;gap:10px;padding:10px 16px;font-variant-numeric:tabular-nums;font-size:13px;color:#b5c4ce;border-top:1px solid #34404a;min-height:43px}.frames{padding:10px 12px;display:flex;gap:5px;flex-wrap:wrap;min-height:66px}.frame{font-size:12px;padding:5px 8px;min-width:34px;background:#252f39}.frame.selected{background:#17677a;border-color:#7ec7d4}.frame.hit{box-shadow:inset 0 -3px #efb94e}.details{font-size:13px;padding:14px;background:#1d242c;border-radius:9px;margin-top:16px;color:#aebbc6}summary{cursor:pointer;color:#d7e1e7}.details pre{white-space:pre-wrap;overflow-wrap:anywhere;font:12px/1.6 ui-monospace,monospace}.hint{font-size:13px;color:#9babb7;margin-top:10px}kbd{background:#293440;border-radius:3px;padding:1px 5px}#ready{font-size:12px;color:#91cdbf}.legend{display:flex;gap:15px;align-items:center;font-size:12px;color:#9babb7}.dot{width:8px;height:8px;display:inline-block;background:#efb94e;border-radius:50%;margin-right:5px}@media(max-width:760px){main{padding:12px}.top{align-items:flex-start}.lanes{gap:9px}h1{font-size:19px}h2{font-size:15px}.info{display:block;padding:8px}.lane header{display:block;padding:10px}.tag{font-size:11px}.frame{padding:4px 6px}.controls{gap:7px}button,select{padding:6px 8px;font-size:13px}.badge{display:none}}
</style>
<main><div class="top"><div><h1>망치 1타·2타 비교</h1><p>현재 Unity에 들어 있는 모션과 새 보정본을 같은 발 기준·크기로 비교합니다.</p></div><span class="badge">미리보기 · 게임 파일 교체 없음</span></div>
<div class="controls"><div class="group" id="attacks"><button data-mode="combo" class="active">1 → 2 연속</button><button data-mode="one">1타만</button><button data-mode="two">2타만</button></div>
<button id="play" class="primary">일시정지</button><button id="restart">처음부터</button><button id="prev" title="이전 프레임">◀ 프레임</button><button id="next" title="다음 프레임">프레임 ▶</button>
<label>재생 <select id="speed"><option value="0.25">0.25배</option><option value="0.5">0.5배</option><option value="1" selected>1배</option><option value="1.5">1.5배</option></select></label>
<button id="fullscreen">크게 보기</button></div>
<div class="controls" style="padding-top:0"><label>시간 기준 <select id="timing"><option value="game" selected>같은 게임 타격 시점에 맞춤</option><option value="source">각 제작본의 원래 시간</option></select></label>
<label>게임 설정 <select id="environment"><option value="trial" selected>현재 씬 시험 공속 ×0.8175</option><option value="base">기본 공속 ×1</option></select></label>
<label>배경 <select id="background"><option value="dark">진회색</option><option value="gray">회색</option><option value="white">흰색</option><option value="checker">체크무늬</option></select></label>
<label>확대 <select id="zoom"><option value="3">3배</option><option value="4" selected>4배</option><option value="5">5배</option><option value="6">6배</option></select></label>
<label><input type="checkbox" id="ground" checked>발 기준선</label></div>
<div class="clock"><span id="clock">0.000초</span><span id="summary"></span></div><input aria-label="모션 시간 이동" type="range" id="seek" min="0" max="1000" step="1" value="0">
<div class="lanes"><section class="lane"><header><h2>현재 Unity 적용본</h2><span class="tag">1타 4장 · 2타 6장</span></header><canvas id="old" width="672" height="480" aria-label="현재 Unity 망치 모션"></canvas><div class="info"><span id="old-phase"></span><span id="old-frame"></span></div><div id="old-frames" class="frames"></div></section>
<section class="lane new"><header><h2>새 색상·움직임 보정본</h2><span class="tag">1타 6장 · 2타 9장</span></header><canvas id="new" width="672" height="480" aria-label="새 망치 보정 모션"></canvas><div class="info"><span id="new-phase"></span><span id="new-frame"></span></div><div id="new-frames" class="frames"></div></section></div>
<div class="controls" style="justify-content:space-between"><span class="legend"><span><i class="dot"></i>타격 프레임</span><span>노란 순간 표식 = 판정 시점</span></span><span id="ready" role="status">이미지 불러오는 중…</span></div>
<p class="hint"><kbd>Space</kbd> 재생/정지 · <kbd>←</kbd><kbd>→</kbd> 프레임 이동. 게임 시간 모드는 추가 장비 공속 1 기준이며, 이동·충돌·히트스톱은 제외합니다. 보정본 끝의 650ms 대기는 반복 간격으로 분리했습니다.</p>
<details class="details"><summary>시간·원본 확인</summary><p id="timing-description"></p><pre id="sources"></pre></details>
</main>
<script id="data" type="application/json">__PAYLOAD__</script>
<script>
'use strict';
const D=JSON.parse(document.getElementById('data').textContent),$=id=>document.getElementById(id);
const state={mode:'combo',timing:'game',environment:'trial',speed:1,time:0,playing:true,zoom:4,bg:'dark',ground:true,ready:false};
const imageCache=new Map();const names=['old','new'];
function label(i){return i===2?'스킬':(i+1)+'타'}
function target(i){if(i===2)return {impact:D.skill.activation,end:D.skill.end};const b=D.basics[i],s=state.environment==='trial'?D.trial.scale:1;return {impact:b.impact/s,end:b.duration/s+(i===1&&state.environment==='trial'?D.trial.lastHitExtraRecoverySeconds:0)}}
function indices(){return state.mode==='all'?[0,1,2]:state.mode==='skill'?[2]:state.mode==='combo'?[0,1]:[state.mode==='one'?0:1]}
function total(side){return indices().reduce((s,i)=>s+(state.timing==='game'?target(i).end:D[side].clips[i].duration),0)}
function cycle(){return Math.max(...names.map(total))+.65}
function actionTime(source,clip,i){if(state.timing==='source')return source;const t=target(i);return source<=clip.impact&&clip.impact>0?source/clip.impact*t.impact:t.impact+(source-clip.impact)/(clip.duration-clip.impact)*Math.max(.001,t.end-t.impact)}
function sourceTime(age,clip,i){if(state.timing==='source')return age;const t=target(i);return age<t.impact&&t.impact>0?age/t.impact*clip.impact:clip.impact+(age-t.impact)/Math.max(.001,t.end-t.impact)*(clip.duration-clip.impact)}
function selection(side,time){let age=time;for(const i of indices()){const c=D[side].clips[i],end=state.timing==='game'?target(i).end:c.duration;if(age<end){let t=Math.max(0,Math.min(c.duration-.00001,sourceTime(age,c,i))),j=0;for(;j<c.frames.length-1;j++){if(t<c.frames[j].seconds)break;t-=c.frames[j].seconds}const impact=state.timing==='game'?target(i).impact:c.impact;return {frame:c.frames[j],clip:i,index:j,age,end,impact,rest:false}}age-=end}return {frame:D[side].idle,clip:indices().at(-1),index:-1,rest:true,age,end:.65,impact:-1}}
function transitions(){const points=new Set([0]);for(const side of names){let offset=0;for(const i of indices()){const c=D[side].clips[i];let source=0;for(const f of c.frames){points.add(Math.min(cycle(),offset+actionTime(source,c,i)));source+=f.seconds}offset+=state.timing==='game'?target(i).end:c.duration;points.add(offset)}}return [...points].sort((a,b)=>a-b)}
function frameStep(direction){state.playing=false;const p=transitions(),t=state.time;state.time=direction>0?(p.find(x=>x>t+.0001)??0):([0,...p].reverse().find(x=>x<t-.0001)??0);$('play').textContent='재생';draw()}
function paint(side,sel){const c=$(side),ctx=c.getContext('2d'),w=c.width,h=c.height,z=state.zoom;ctx.clearRect(0,0,w,h);const colors={dark:'#30363d',gray:'#777c80',white:'#ffffff'};if(state.bg==='checker'){for(let y=0;y<h;y+=24)for(let x=0;x<w;x+=24){ctx.fillStyle=((x/24+y/24)%2)?'#bfc4c7':'#e0e4e7';ctx.fillRect(x,y,24,24)}}else{ctx.fillStyle=colors[state.bg];ctx.fillRect(0,0,w,h)}
const rootX=w*.43,rootY=h*.85;if(state.ground){ctx.strokeStyle=state.bg==='white'?'#9aaab5':'#6f828e';ctx.lineWidth=1;ctx.setLineDash([6,6]);ctx.beginPath();ctx.moveTo(0,rootY+.5);ctx.lineTo(w,rootY+.5);ctx.stroke();ctx.setLineDash([]);ctx.fillStyle='#79b9bd';ctx.fillRect(rootX-3,rootY-3,6,6)}
const f=sel.frame,img=imageCache.get(f.image);if(img){const ratio=32/f.ppu*z;ctx.imageSmoothingEnabled=f.ppu!==32;ctx.drawImage(img,rootX-f.pivot[0]*ratio,rootY-f.pivot[1]*ratio,img.naturalWidth*ratio,img.naturalHeight*ratio)}
if(!sel.rest&&Math.abs(sel.age-sel.impact)<.045){ctx.fillStyle='#efb94e';ctx.fillRect(0,0,w,4);ctx.font='bold 19px system-ui';ctx.fillText(label(sel.clip)+' · 타격',18,30)}
$(side+'-phase').textContent=sel.rest?'복귀 / 반복 대기':label(sel.clip)+' · '+sel.age.toFixed(3)+' / '+sel.end.toFixed(3)+'초';$(side+'-frame').textContent=sel.rest?f.name:(sel.index+1)+' / '+D[side].clips[sel.clip].frames.length+'장 · '+f.name;
document.querySelectorAll('#'+side+'-frames button').forEach(b=>b.classList.toggle('selected',!sel.rest&&Number(b.dataset.clip)===sel.clip&&Number(b.dataset.index)===sel.index));}
function draw(){if(!state.ready)return;for(const s of names)paint(s,selection(s,state.time));$('clock').textContent=state.time.toFixed(3)+' / '+cycle().toFixed(3)+'초';$('seek').max=Math.round(cycle()*1000);$('seek').value=Math.round(state.time*1000)}
function rebuild(){for(const side of names){const box=$(side+'-frames');box.replaceChildren();let offset=0;for(const i of indices()){const c=D[side].clips[i];let s=0;for(const [j,f]of c.frames.entries()){const b=document.createElement('button'),at=offset+actionTime(s,c,i);b.className='frame'+(s<=c.impact&&s+f.seconds>c.impact?' hit':'');b.textContent=(i+1)+'-'+(j+1);b.dataset.clip=i;b.dataset.index=j;b.title=f.name+' · 원본 '+Math.round(f.seconds*1000)+'ms';b.addEventListener('click',()=>{state.time=at;state.playing=false;$('play').textContent='재생';draw()});box.append(b);s+=f.seconds}offset+=state.timing==='game'?target(i).end:c.duration}}
const timing=indices().map(i=>{const t=target(i);return label(i)+': 판정 '+t.impact.toFixed(3)+'초 / 종료 '+t.end.toFixed(3)+'초'}).join(' · ');$('summary').textContent=state.timing==='game'?timing:'각 원본 시간 · Unity '+total('old').toFixed(3)+'초 / 보정 '+total('new').toFixed(3)+'초';$('environment').disabled=state.timing!=='game';$('timing-description').textContent=timing+'\n새 모션은 Unity의 타격 전·후 시간 매핑 방식으로 가상 재생합니다. 실제 적용 결과를 검증한 것은 아닙니다.';draw()}
function reset(){state.time=0;rebuild()}
$('attacks').addEventListener('click',e=>{if(e.target.dataset.mode){state.mode=e.target.dataset.mode;document.querySelectorAll('#attacks button').forEach(b=>b.classList.toggle('active',b===e.target));reset()}});
$('play').onclick=()=>{state.playing=!state.playing;$('play').textContent=state.playing?'일시정지':'재생'};$('restart').onclick=reset;$('prev').onclick=()=>frameStep(-1);$('next').onclick=()=>frameStep(1);
$('speed').onchange=e=>state.speed=Number(e.target.value);$('timing').onchange=e=>{state.timing=e.target.value;reset()};$('environment').onchange=e=>{state.environment=e.target.value;reset()};$('zoom').onchange=e=>{state.zoom=Number(e.target.value);draw()};$('background').onchange=e=>{state.bg=e.target.value;draw()};$('ground').onchange=e=>{state.ground=e.target.checked;draw()};$('seek').oninput=e=>{state.time=Number(e.target.value)/1000;state.playing=false;$('play').textContent='재생';draw()};$('fullscreen').onclick=()=>document.fullscreenElement?document.exitFullscreen():document.documentElement.requestFullscreen();
window.addEventListener('keydown',e=>{if(['INPUT','SELECT','BUTTON'].includes(document.activeElement.tagName))return;if(e.code==='Space'){e.preventDefault();$('play').click()}if(e.code==='ArrowLeft'){e.preventDefault();frameStep(-1)}if(e.code==='ArrowRight'){e.preventDefault();frameStep(1)}});
$('sources').textContent=D.sourcesText;
const urls=[...new Set(names.flatMap(s=>[D[s].idle.image,...D[s].clips.flatMap(c=>c.frames.map(f=>f.image))]))];
Promise.all(urls.map(url=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{imageCache.set(url,im);resolve()};im.onerror=reject;im.src=url}))).then(()=>{state.ready=true;$('ready').textContent=urls.length+'개 이미지 준비 완료 · 자동 재생 중';rebuild()}).catch(()=>{$('ready').textContent='이미지 로딩 실패';state.playing=false});
let previous=performance.now();function tick(now){const elapsed=Math.min(.1,(now-previous)/1000);previous=now;if(state.ready&&state.playing){state.time=(state.time+elapsed*state.speed)%cycle();draw()}requestAnimationFrame(tick)}requestAnimationFrame(tick);
window.hammerComparison={state,selection,transitions,target,data:D};
</script></html>'''


def write_gif_previews(out,data):
    """Render the same game-time source mapping for a browser-free preview."""
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',22)
    small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
    cache={}
    def target(i):
        if i==2:return data['skill']['activation'],data['skill']['end']
        b=data['basics'][i];t=data['trial']
        return b['impact']/t['scale'],b['duration']/t['scale']+(t['lastHitExtraRecoverySeconds'] if i==1 else 0)
    def frame_at(side,age,indices):
        for i in indices:
            c=data[side]['clips'][i];impact,end=target(i)
            if age<end:
                source=age/impact*c['impact'] if age<impact else c['impact']+(age-impact)/(end-impact)*(c['duration']-c['impact'])
                for f in c['frames']:
                    if source<f['seconds']:return f,i,age,impact
                    source-=f['seconds']
                return c['frames'][-1],i,age,impact
            age-=end
        return data[side]['idle'],-1,age,-1
    for filename,indices in [('basic-comparison.gif',[0,1]),('skill-comparison.gif',[2]),('all-comparison.gif',[0,1,2])]:
        duration=sum(target(i)[1] for i in indices)+.65;images=[]
        for n in range(math.ceil(duration/.02)):
            age=n*.02;canvas=Image.new('RGB',(1344,546),'#1d242c');draw=ImageDraw.Draw(canvas)
            for lane,side in enumerate(['old','new']):
                x=lane*672;f,i,local,impact=frame_at(side,age,indices)
                draw.rectangle((x,54,x+671,533),fill='#30363d')
                draw.text((x+18,12),'현재 Unity 적용본' if side=='old' else '새 평타 15장 · 스킬 14장',font=font,fill='#e9edf1')
                draw.line((x,462,x+671,462),fill='#6f828e')
                key=f['image']
                if key not in cache:cache[key]=Image.open(io.BytesIO(base64.b64decode(key.split(',',1)[1]))).convert('RGBA')
                im=cache[key];ratio=128/f['ppu']
                resized=im.resize((round(im.width*ratio),round(im.height*ratio)),Image.Resampling.NEAREST if f['ppu']==32 else Image.Resampling.LANCZOS)
                canvas.paste(resized,(round(x+672*.43-f['pivot'][0]*ratio),round(54+480*.85-f['pivot'][1]*ratio)),resized)
                label='반복 대기' if i<0 else '스킬' if i==2 else str(i+1)+'타'
                draw.text((x+18,511),label+' · '+f['name'],font=small,fill='#b5c4ce')
                if i>=0 and abs(local-impact)<.045:
                    draw.rectangle((x,54,x+671,58),fill='#efb94e');draw.text((x+18,66),'타격',font=font,fill='#efb94e')
            images.append(canvas)
        images[0].save(out/filename,save_all=True,append_images=images[1:],duration=20,loop=0,disposal=2)
    images[0].save(out/'comparison-still.png')


def main():
    global SET,SKILL_SET
    ap=argparse.ArgumentParser();ap.add_argument('--destination',type=Path,required=True)
    ap.add_argument('--include-skill',action='store_true')
    ap.add_argument('--basic-set',default=SET)
    ap.add_argument('--skill-set',default=SKILL_SET)
    args=ap.parse_args()
    SET,SKILL_SET=args.basic_set,args.skill_set
    def branch():
        assert subprocess.check_output(['git','-C',str(REPO),'branch','--show-current'],text=True).strip()=='feat/story-unity-essentials'
    branch();out=args.destination.resolve();out.mkdir(parents=True,exist_ok=False)
    inputs={}
    def read(path):
        b=path.read_bytes();inputs[path]=hashlib.sha256(b).hexdigest();return b
    catalog=json.loads(read(RES/'CharacterV2/AnimationCatalog.json'))
    approved=json.loads(read(RES/'CharacterV2/ApprovedWeaponCatalog.json'))
    registry=catalog['motionRegistry']
    bindings=catalog['meleeFrames']['actions']
    combat=json.loads(read(RES/'MeleeCatalog.json'))
    basics=next(f for f in combat['forms'] if f['id']=='hammer')['basics']
    environment=json.loads(read(RES/'PlaytestEnvironment.json'))
    trial=next(x for x in environment['basicSpeedTrials'] if x['form']=='hammer')
    source=ASSET/'source'/SET;newan=json.loads(read(source/'animation.json'))
    clips=next(s for s in approved['sets'] if s['form']=='hammer')['clips']
    def sprite(path,name,ppu=None,pivot=None):
        raw=read(path);im=Image.open(path);w,h=im.size
        if ppu is None:
            meta=read(Path(str(path)+'.meta')).decode('utf8')
            ppu=float(re.search(r'spritePixelsToUnits:\s*([\d.]+)',meta)[1])
            match=re.search(r'spritePivot:\s*\{x:\s*([\d.]+), y:\s*([\d.]+)\}',meta)
            pivot=[float(match[1])*w,(1-float(match[2]))*h]
        return {'name':name,'image':'data:image/png;base64,'+base64.b64encode(raw).decode(),'ppu':ppu,'pivot':pivot}
    oldclips=[];newclips=[]
    for i in range(2):
        motionid=f'hammer/attack{i+1}'
        b=next(b for b in bindings if b['actionId']==f'hammer_basic_{i}')
        assert b['motionId']==motionid and b['mode']==0
        m=next(m for m in registry['motions'] if m['id']==motionid)
        media=next(c for c in clips if c['name']==f'attack{i+1}')
        assert [s['frame'] for s in m['steps']]==media['frames']
        assert [s['seconds'] for s in m['steps']]==media['seconds']
        frames=[]
        for s in m['steps']:
            f=sprite(RES/(s['frame']+'.png'),s['frame'].split('/')[-1]);f['seconds']=s['seconds'];frames.append(f)
        oldclips.append({'frames':frames,'duration':sum(s['seconds'] for s in m['steps']),'impact':m['sourceImpact']})
        steps=newan['sequences'][f'attack_{i+1}'];frames=[]
        for j,s in enumerate(steps):
            f=sprite(ASSET/'runtime'/SET/'detail4x'/(s['frame']+'.png'),s['frame'],128,[384,604])
            f['seconds']=0 if i==1 and j==len(steps)-1 else s['ms']/1000
            frames.append(f)
        impact=sum(f['seconds'] for f in frames[:4])
        newclips.append({'frames':frames,'duration':sum(f['seconds'] for f in frames),'impact':impact})
    oldidle=sprite(RES/'CharacterApproved11/hammer/F000.png','대기 F000')
    newidle={k:v for k,v in newclips[1]['frames'][-1].items() if k!='seconds'}
    assert [len(c['frames']) for c in oldclips]==[4,6]
    assert [len(c['frames']) for c in newclips]==[6,9]
    assert abs(newclips[1]['duration']-.68)<1e-8
    skill=None
    if args.include_skill:
        binding=next(b for b in catalog['specialFrames']['actions'] if b['actionId']=='hammer_default')
        assert binding['mode']==0 and binding['motionId']=='hammer/attack1'
        action=next(a for a in registry['actions'] if a['actionId']=='hammer_default')
        assert action['motion']==binding['motionId'] and not action['fallback']
        skill=next(s for f in combat['forms'] if f['id']=='hammer' for s in f['specials'] if s['id']=='hammer_default')
        assert skill['activation']==.4 and skill['end']==.95
        m=next(m for m in registry['motions'] if m['id']==binding['motionId'])
        oldclips.append({'frames':oldclips[0]['frames'],'duration':oldclips[0]['duration'],'impact':m['sourceImpact']})
        an=json.loads(read(ASSET/'source'/SKILL_SET/'animation.json'))
        frames=[]
        for s in an['sequences']['hammer_default']:
            f=sprite(ASSET/'runtime'/SKILL_SET/'detail4x'/(s['frame']+'.png'),s['frame'],an['detailExport']['ppu'],an['detailExport']['pivot'])
            f['seconds']=s['ms']/1000;frames.append(f)
        assert len(frames)==14 and abs(sum(f['seconds'] for f in frames)-.95)<1e-8
        assert abs(sum(f['seconds'] for f in frames[:7])-.4)<1e-8
        newclips.append({'frames':frames,'duration':an['durationSeconds'],'impact':an['contactSeconds']})
    d={'old':{'clips':oldclips,'idle':oldidle},'new':{'clips':newclips,'idle':newidle},'basics':basics,'trial':trial,
       'sourcesText':'Unity 실파일: '+str(RES/'CharacterApproved11/hammer')+'\n현재 바인딩: AnimationCatalog.json → motionRegistry / meleeFrames\n현재 타이밍: AttackMotionTiming.MapSourceTime 방식\n새 보정본: '+str(ASSET/'runtime'/SET/'detail4x')+'\n원본/게임 에셋 변경 없음. 몸체 PNG 비교이며 별도 Unity 효과·충돌·히트스톱·이동은 포함하지 않음.'}
    html=HTML
    if args.include_skill:
        d['skill']=skill
        d['sourcesText']+='\n현재 스킬 바인딩: hammer_default → hammer/attack1 (평타 1타 재사용)\n새 스킬: '+str(ASSET/'runtime'/SKILL_SET/'detail4x')+'\n스킬 타격 0.40초 / 종료 0.95초. 공속 시험 배율과 평타 마지막 타 추가 후딜은 스킬에 적용하지 않음.\n평타→스킬 연속 재생은 모션 확인용 순서이며 실제 콤보 취소·입력 연결을 시뮬레이션하지 않음.'
        html=html.replace('망치 1타·2타','망치 평타·스킬')
        html=html.replace('<button data-mode="combo" class="active">1 → 2 연속</button>','<button data-mode="all" class="active">평타 → 스킬</button><button data-mode="combo">평타 1 → 2</button>')
        html=html.replace('<button data-mode="two">2타만</button>','<button data-mode="two">2타만</button><button data-mode="skill">스킬만</button>')
        html=html.replace("mode:'combo'","mode:'all'")
        html=html.replace('1타 4장 · 2타 6장','평타 4+6장 · 스킬 4장 (1타 재사용)')
        html=html.replace('1타 6장 · 2타 9장','평타 6+9장 · 스킬 14장')
        html=html.replace("b.textContent=(i+1)+'-'+(j+1)","b.textContent=(i===2?'S':i+1)+'-'+(j+1)")
        html=html.replace('보정본 끝의 650ms 대기는 반복 간격으로 분리했습니다.','평타→스킬은 비교용 순서입니다. 스킬 타격 0.40초·종료 0.95초를 유지하며, 650ms 대기는 반복 간격입니다.')
    (out/'index.html').write_text(html.replace('__PAYLOAD__',json.dumps(d,ensure_ascii=False).replace('</',r'<\/')),encoding='utf8')
    if args.include_skill:write_gif_previews(out,d)
    branch()
    for p,h in inputs.items():assert hashlib.sha256(p.read_bytes()).hexdigest()==h,'Input modified while exporting: '+str(p)
    manifest={'currentFrames':[len(c['frames']) for c in oldclips],'newFrames':[len(c['frames']) for c in newclips],
        'trial':trial,'sourceTiming':{'old':[{'duration':c['duration'],'impact':c['impact']} for c in oldclips],
        'new':[{'duration':c['duration'],'impact':c['impact']} for c in newclips]},
        'sourcesUnchanged':True,'files':[{'path':str(p),'sha256':h} for p,h in inputs.items()]}
    (out/'source-snapshot.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps({'html':str(out/'index.html'),'inputsVerified':len(inputs),'oldFrames':manifest['currentFrames'],'newFrames':manifest['newFrames']},ensure_ascii=False))


if __name__=='__main__':main()
