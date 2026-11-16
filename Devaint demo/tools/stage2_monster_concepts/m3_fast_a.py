"""Retain selected A pixels and shorten its cycle for a quicker approach cadence."""
import json
import shutil
import zipfile

from PIL import Image,ImageDraw,ImageFont
from k4_d_pixel_walk import ROOT,N,sha
from b3_walk_return import white,dump
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_m3_fast_a.json'


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));source=ROOT/spec['source'];out=ROOT/spec['output'];out.mkdir(parents=True,exist_ok=True)
    original=json.loads((source/'A/manifest.json').read_text(encoding='utf-8'));common=json.loads((source/'manifest.json').read_text(encoding='utf-8'))
    assert original['frameCount']==spec['frameCount'] and spec['frameCount']*spec['frameMs']==spec['cycleMs']
    for folder in ('native','native-indexed','white-native','enlarged-4x','source'):
        (out/folder).mkdir(exist_ok=True)
        for p in sorted((source/'A'/folder).glob('*.png')):shutil.copy2(p,out/folder/p.name)
    (out/'references').mkdir(exist_ok=True);(out/'previews').mkdir(exist_ok=True)
    for p in (source/'references').iterdir():
        if p.is_file():shutil.copy2(p,out/'references'/p.name)
    shutil.copy2(source/'A/manifest.json',out/'references/original-A.json');shutil.copy2(SPEC,out/'timing-spec.json')
    frames=[Image.open(out/'native'/f'walk-{i+1:02}.png').convert('RGBA') for i in range(spec['frameCount'])];ds=[spec['frameMs']]*len(frames);box=common['previewCrop']
    animate(frames,ds,out/'previews/fast-native.webp');animate(frames,ds,out/'previews/fast-native.apng')
    crops=[white(f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N)).convert('RGBA') for f in frames]
    animate(crops,ds,out/'previews/fast-white-4x.webp')
    # Export a single synchronized before/after animation for timing review.
    # Sample the finite inspection clip at20ms; do not call its outer reset a gait seam.
    # Individual exports and the interactive canvas retain exact source durations.
    review=[];prior=original['durationsMs'];font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18);w,h=crops[0].size
    def key_at(t,durations):
        rem=t%sum(durations)
        for k,duration in enumerate(durations):
            if rem<duration:return k
            rem-=duration
        return 0
    # Three seconds is a finite side-by-side inspection clip; the outer repeat is labeled.
    for t in range(0,3000,20):
        im=Image.new('RGBA',(w*2+20,h+32),'white');im.alpha_composite(crops[key_at(t,prior)],(0,32));im.alpha_composite(crops[key_at(t,ds)],(w+20,32))
        draw=ImageDraw.Draw(im);draw.text((8,3),'기존 A · 0.8초',font=font,fill='black');draw.text((w+28,3),'빠른 A · 0.54초',font=font,fill='black');review.append(im)
    animate(review,[20]*len(review),out/'previews/timing-comparison.webp')
    result={**spec,'sourceIndices':original['indices'],'durationsMs':ds,'priorDurationsMs':prior,'speedRatio':spec['priorCycleMs']/spec['cycleMs'],'canvas':[384,224],'previewCrop':box,'paletteRgb':common['paletteRgb'],'sourceFrameHashes':[sha(source/'A/native'/f'walk-{i+1:02}.png') for i in range(len(frames))],'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',result);page(out,result)
    (out/'README.md').write_text('''# 먹침필경사 선택 A · 빠른 접근 재생

사용자가 선택한 A의18개 PNG와 팔레트·크기·위치를 무변형 복사한다. 기존800ms에서 각30ms·총540ms로 줄여 약1.48배 빠르게 재생한다. 프레임 추가·삭제·픽셀 이동·색 변경은 없으며 이번 검토는 재생 시간만 바꾼 것이다. 캐릭터의 실제 이동속도와 공격 규칙·Unity 적용은 변경하지 않는다.

native/indexed/흰 비교/정수4배/반환 원본 셀과 참조·해시를 보존한다. 무손실 WebP/APNG는 정확한18장·30ms다. 대조 WebP는20ms 간격의3초 비교 클립이며 클립 끝의 전체 리셋은 각 보행 루프 접합과 다르다. 대화형 비교는 각 타이밍을 정확히 누적해 연속 재생한다. A 동작은 사용자 선택이고 새 빠른 타이밍은 검토 단계다.
''',encoding='utf-8')
    package(out)


def package(out):
    with zipfile.ZipFile(out/'m3-A-fast-20261101.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip':archive.write(p,p.relative_to(out).as_posix())


def page(out,info):
    js='''const data=DATA,images=[];let playing=true,time=0,previous=null,scale=3;
function keyAt(t,ds){let rem=t%ds.reduce((a,b)=>a+b,0);for(let i=0;i<ds.length;i++){if(rem<ds[i])return i;rem-=ds[i]}return 0}
function draw(){const box=data.previewCrop,w=box[2]-box[0],h=box[3]-box[1];document.querySelectorAll('canvas').forEach((c,i)=>{let key=keyAt(time,i?data.durationsMs:data.priorDurationsMs);c.width=w;c.height=h;c.style.width=w*scale+'px';c.style.height=h*scale+'px';const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.drawImage(images[key],box[0],box[1],w,h,0,0,w,h);document.querySelectorAll('.key')[i].textContent=(key+1)+' /18'})}
Promise.all(data.durationsMs.map((_,i)=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src='native/walk-'+String(i+1).padStart(2,'0')+'.png'}))).then(arr=>{images.push(...arr);draw();requestAnimationFrame(tick)});
function tick(t){if(previous===null)previous=t;if(playing){time+=t-previous;draw()}previous=t;requestAnimationFrame(tick)}document.querySelector('#play').onclick=()=>{playing=!playing;previous=performance.now();document.querySelector('#play').textContent=playing?'정지':'재생'};document.querySelector('#restart').onclick=()=>{time=0;previous=performance.now();draw()};document.querySelector('#scale').onchange=e=>{scale=+e.target.value;draw()};'''.replace('DATA',json.dumps(info))
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>먹침필경사 A · 빠른 접근</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:24px}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}.loop{display:block;margin:auto}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}section{background:white;padding:18px;min-width:0}.viewport{overflow:auto}button,select{padding:8px;margin:4px}a{color:#815149}@media(max-width:800px){.grid{grid-template-columns:1fr}}</style><main><h1>먹침필경사 · 선택 A를 빠르게</h1><p>A18장은 그대로 두고0.8초 → <b>0.54초</b>로 줄였습니다. 장당30ms로 약1.48배 빠른 발 교대입니다. 픽셀·색·크기와 자세는 같습니다. A 동작은 사용자 선택이며 새 속도는 검토 단계입니다.</p><p><a download href="m3-A-fast-20261101.zip">18장 PNG·4배·시간표 ZIP</a> · <a href="manifest.json">속도·프레임 정보</a></p><img class="loop" src="previews/fast-white-4x.webp"><h2>기존 / 빠른 A · 연속 동시 비교</h2><button id="play">정지</button><button id="restart">같이 처음부터</button><select id="scale"><option value="1">실제1배</option><option value="3" selected>3배</option><option value="4">4배</option><option value="8">8배</option></select><div class="grid"><section><h2>기존 A · 0.8초</h2><div class="viewport"><canvas></canvas></div><span class="key"></span></section><section><h2>빠른 A · 0.54초</h2><div class="viewport"><canvas></canvas></div><span class="key"></span></section></div><p>이 비교는 동일18장을 각 시간표로 계속 재생합니다. 실제 게임 이동속도·공격·Unity 연결은 바꾸지 않았습니다. 캐릭터 모션 검토본이며 실제 게임 캡처가 아닙니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')


if __name__=='__main__':build()
