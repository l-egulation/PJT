"""Publish distinct unmodified-source loop options for the user's visual selection."""
from pathlib import Path
from html import escape
import hashlib,json,shutil,zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
SPEC=ROOT/'data/presentation/stage2_k4_loop_candidates.json'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));prior=ROOT/spec['source'];out=ROOT/spec['output']
    for sub in ('frames-original','previews/frames'):(out/sub).mkdir(parents=True,exist_ok=True)
    unique=sorted({i for c in spec['candidates'] for i in range(c['start'],c['end']+1)});images={};boxes=[];hashes={}
    for i in unique:
        src=prior/'all-frames'/f'frame-{i:03}.png';dst=out/'frames-original'/src.name;shutil.copy2(src,dst);im=Image.open(src).convert('RGBA');images[i]=im;hashes[str(i)]=sha(src)
        a=np.array(im);y,x=np.where(a[:,:,:3].min(2)<200);boxes.append([int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)])
    b=np.array(boxes);box=(max(0,int(b[:,0].min())-24),max(0,int(b[:,1].min())-24),min(1280,int(b[:,2].max())+24),min(720,int(b[:,3].max())+24))
    crops={i:im.crop(box) for i,im in images.items()}
    for i,im in crops.items():im.save(out/'previews/frames'/f'frame-{i:03}.png')
    candidates=[];cards=[]
    for c in spec['candidates']:
        p=out/c['id'];p.mkdir(exist_ok=True);indices=list(range(c['start'],c['end']+1));frames=[crops[i] for i in indices];ds=[c['frameMs']]*len(indices)
        for j in range(c.get('edgeCount',0)):ds[j]=ds[-1-j]=c['edgeFrameMs']
        frames[0].save(p/'loop.webp',save_all=True,append_images=frames[1:],duration=ds,loop=0,lossless=True,exact=True)
        cw,ch=frames[0].size;tw,th=cw//3,ch//3;board=Image.new('RGB',(tw*6,th+30),'white');draw=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',14)
        seam=indices[-3:]+indices[:3]
        for k,i in enumerate(seam):
            board.paste(crops[i].resize((tw,th),Image.Resampling.NEAREST).convert('RGB'),(k*tw,30));draw.text((k*tw+4,4),f'{"끝" if k<3 else "시작"} / 원본{i+1}',fill='black',font=font)
        board.save(p/'seam.png')
        row={**c,'indices':indices,'durations':ds,'frameCount':len(indices),'cycleMs':sum(ds),'seamPlaybackIndices':list(range(len(indices)-3,len(indices)))+list(range(3)),'loop':'%s/loop.webp'%c['id'],'nativePaths':['frames-original/frame-%03d.png'%i for i in indices],'previewPaths':['previews/frames/frame-%03d.png'%i for i in indices]};candidates.append(row);(p/'manifest.json').write_text(json.dumps(row,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        cards.append(f'''<section><h2>{c['id']} · {escape(c['name'])}{' ✓ 사용자 선택' if c['id']==spec.get('selectedCandidate') else (' ★ 추천' if c['recommended'] and not spec.get('selectedCandidate') else '')}</h2><p>{len(indices)}장 · {sum(ds)/1000:g}초</p><p>{escape(c['description'])}</p><div class="viewport"><canvas data-id="{c['id']}"></canvas></div><p data-state="{c['id']}">불러오는 중…</p><input data-step="{c['id']}" type="range" min="0" max="{len(indices)-1}" value="0"><p class="note">{escape(c['tradeoff'])}</p><p><a href="{c['id']}/loop.webp">루프 파일</a> · <a href="{c['id']}/manifest.json">프레임 대응</a></p><details><summary>마지막3장 → 처음3장</summary><img src="{c['id']}/seam.png"></details></section>''')
    manifest={**spec,'candidates':candidates,'previewCrop':list(box),'sourceFrameHashes':hashes,'choice':spec.get('selectedCandidate')};(out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');shutil.copy2(SPEC,out/'candidate-spec.json')
    js='''const rows=DATA;const loaded=new Map();let playing=true,rate=1,seams=false,epoch=performance.now(),pausedAt=0;const states=[];
function load(src){if(!loaded.has(src))loaded.set(src,new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=src}));return loaded.get(src)}
function seq(s){return seams?s.seamPlaybackIndices:s.indices.map((_,i)=>i)}
function draw(s,key){s.key=key;const c=document.querySelector('[data-id="'+s.id+'"]'),im=s.images[key];if(c.width!==im.width||c.height!==im.height){c.width=im.width;c.height=im.height}const ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.drawImage(im,0,0);document.querySelector('[data-step="'+s.id+'"]').value=key;document.querySelector('[data-state="'+s.id+'"]').textContent=(key>=s.frameCount-3?'마지막 · ':key<3?'시작 · ':'')+'선정 '+(key+1)+' / '+s.frameCount+' · 원본 '+(s.indices[key]+1)+'번'}
function tick(t){if(playing){const time=(t-epoch)*rate;for(const s of states){const order=seq(s);const cycle=order.reduce((v,k)=>v+s.durations[k],0);let rem=time%cycle,key=order[0];for(const k of order){key=k;if(rem<s.durations[k])break;rem-=s.durations[k]}if(key!==s.key)draw(s,key)}}requestAnimationFrame(tick)}
function restart(){epoch=performance.now();pausedAt=epoch;states.forEach(s=>draw(s,seq(s)[0]))}
Promise.all(rows.map(async s=>{s.images=await Promise.all(s.previewPaths.map(load));s.key=-1;states.push(s);draw(s,0)})).then(()=>{restart();requestAnimationFrame(tick)});
document.querySelector('#play').onclick=()=>{if(playing){playing=false;pausedAt=performance.now()}else{epoch+=performance.now()-pausedAt;playing=true}document.querySelector('#play').textContent=playing?'전체 정지':'전체 재생'};
document.querySelector('#restart').onclick=restart;document.querySelector('#rate').onchange=e=>{rate=+e.target.value;restart()};document.querySelector('#mode').onchange=e=>{seams=e.target.value==='seam';restart()};
document.querySelectorAll('[data-step]').forEach(el=>el.oninput=()=>{if(playing)pausedAt=performance.now();playing=false;document.querySelector('#play').textContent='전체 재생';const s=states.find(s=>s.id===el.dataset.step);if(s)draw(s,+el.value)});'''.replace('DATA',json.dumps(candidates))
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>등짐분사수 루프 접합 A~D</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1250px;margin:auto;padding:24px}.controls{position:sticky;top:0;background:#eeeef2f5;padding:12px;z-index:1}button,select{padding:8px 12px;margin:3px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:22px}section{background:white;padding:20px;min-width:0}h2{font-size:21px}.viewport{max-width:390px;margin:auto}canvas{width:100%;height:auto;image-rendering:pixelated}img{max-width:100%;image-rendering:pixelated}a{color:#815149}.note{color:#65594c}input{width:100%}@media(max-width:750px){.grid{grid-template-columns:1fr}}</style><main><h1>끝 → 시작 · A~D 루프 후보</h1><p>상체·등통·다리 자세가 함께 가까워지는 구간과 전환 시간을 달리한4가지입니다. 사용자 선택은 D입니다. 원본 픽셀과 흰 배경을 유지합니다.</p><p><a download href="k4-loop-candidates-20261031.zip">후보 PNG·루프 ZIP</a> · <a href="manifest.json">후보 정보</a></p><div class="controls"><button id="play">전체 정지</button><button id="restart">동시에 처음부터</button><select id="rate"><option value="1">1배속</option><option value="0.5">0.5배속</option></select><select id="mode"><option value="full">전체 반복</option><option value="seam">마지막3장 → 처음3장만</option></select></div><div class="grid">'''+''.join(cards)+'''</div><p>접합부만 보기에서는 여섯 장을 반복하므로 여섯 번째에서 첫 번째로 돌아가는 지점은 전체 루프의 실제 접합이 아닙니다. 각 카드의 ‘마지막→시작’ 표시가 바뀌는 중간 전환을 비교하세요. 원본 PNG에 시간 정보가 없어 모든 시간은 검토 제안입니다. 새 프레임·보간·크로스페이드·파츠 합성은 하지 않았습니다.</p><noscript>미리보기에는 JavaScript가 필요합니다. 각 카드의 루프 파일 링크로도 재생할 수 있습니다.</noscript></main><script>'''+js+'</script></html>',encoding='utf-8')
    (out/'README.md').write_text('''# 등짐분사수 접합 후보 A~D

A는 원본0기준14~33의20장, B는36~55의20장, C는19~57의39장이다. 모두50ms. D는 A와 같은20장에 처음·마지막2장30ms, 나머지55ms를 적용하여1초를 유지한다. 사용자 선택은 D다. frames-original은 원본 셀 복사, previews/frames는 모든 후보에 동일한 공통 크롭을 사용한 보기용 PNG다. 각 후보 루프는 무손실 WebP다. 원본의 색·신체를 변경하지 않고 접합 구간·시간만 비교한다.

화면은 동시 시작·정지·0.5배속·접합 여섯 장만 보기와 개별 키 선택을 지원한다. 접합 모드의 바깥 반복 경계는 실제 전체 루프의 접합과 다르므로 가운데 마지막→처음 전환을 비교한다. 기존21장과 원본은 보존하며 게임 적용·최종 승인은 별도다.
''',encoding='utf-8')
    allowed={'frames-original','previews','A','B','C','D','manifest.json','candidate-spec.json','preview.js','index.html','README.md'}
    with zipfile.ZipFile(out/'k4-loop-candidates-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.relative_to(out).parts[0] in allowed:z.write(p,p.relative_to(out).as_posix())
    print(json.dumps([{'id':r['id'],'frames':r['frameCount'],'cycleMs':r['cycleMs']} for r in candidates]))


if __name__=='__main__':build()
