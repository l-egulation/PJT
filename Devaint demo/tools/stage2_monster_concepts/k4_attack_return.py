"""Extract K4's returned attack; hold one whole pose while the separate spray emits."""
import json
import shutil
import zipfile
from pathlib import Path
from html import escape

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from k4_d_pixel_walk import ROOT, N, sha, foreground_bounds, sample, tidy, map_preview
from k4_d_pixel_cleanup import fill_holes
from b3_walk_return import dump, white
from walk_review import animate

SPEC = ROOT/'data/presentation/stage2_k4_attack_return.json'


def durations(count, total):
    points = [round(total*i/count) for i in range(count+1)]
    return [b-a for a,b in zip(points,points[1:])]


def board(frames, rows, crop, selected, path):
    zoom=3; cols=6; w=(crop[2]-crop[0])*zoom; h=(crop[3]-crop[1])*zoom
    result=Image.new('RGB',(w*cols,(h+52)*((len(selected)+cols-1)//cols)),'white')
    draw=ImageDraw.Draw(result); font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',14)
    for pos,key in enumerate(selected):
        x,y=(pos%cols)*w,(pos//cols)*(h+52);row=rows[key]
        result.paste(white(frames[key].crop(crop).resize((w,h),N)),(x,y+52))
        draw.text((x+8,y+4),f'키{key+1:02} · 원본{row["sourceOneBased"]:02}',font=font,fill='#25252d')
        draw.text((x+8,y+26),f'{row["phaseName"]} / {row["durationMs"]}ms',font=font,fill='#815149')
    result.save(path)


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];base=ROOT/spec['base'];handoff=ROOT/spec['inputHandoff']
    for sub in ('source','all-frames','selected-source','native','native-indexed','white-native','enlarged-4x','previews','references','masks','phases'):
        (out/sub).mkdir(parents=True,exist_ok=True)
    src=out/'source'/Path(spec['sourceFile']).name
    if not src.exists():shutil.copy2(spec['sourceFile'],src)
    assert sha(src)==sha(Path(spec['sourceFile']))
    sheet=Image.open(src).convert('RGBA');cw,ch=spec['cellSize'];cols,rows=spec['grid'];assert sheet.size==(cw*cols,ch*rows)
    source_hashes=[]
    for i in range(cols*rows):
        x,y=(i%cols)*cw,(i//cols)*ch
        cell=sheet.crop((x,y,x+cw,y+ch));p=out/'all-frames'/f'frame-{i:03}.png';cell.save(p)
        assert Image.open(p).tobytes()==cell.tobytes();source_hashes.append(sha(p))
    for src_ref,name in [(base/'native/base-frame.png','base-frame.png'),(base/'palette.json','palette.json'),(handoff/'prompt-en.txt','input-prompt.txt'),(handoff/'manifest.json','input-manifest.json'),(SPEC,'attack-spec.json')]:
        shutil.copy2(src_ref,out/'references'/name)
    basic=Image.open(base/'native/base-frame.png').convert('RGBA');a=np.asarray(basic)
    palette=np.unique(a[a[:,:,3]>0,:3],axis=0);assert len(palette)==11
    boxes=np.array([foreground_bounds(Image.open(out/'all-frames'/f'frame-{i:03}.png')) for i in spec['scaleReferenceFramesZeroBased']])
    scale=float(spec['targetReferenceHeight']/np.median(boxes[:,3]-boxes[:,1]));anchor=[float(np.median((boxes[:,0]+boxes[:,2])/2)),float(np.median(boxes[:,3]))]
    offset=[192-anchor[0]*scale,spec['baseline']-anchor[1]*scale]
    pal=np.concatenate([np.zeros((1,3),np.uint8),palette]);frames=[];mapping=[];ds=[];phases=[]
    for phase in spec['phases']:
        indices=phase['sourceIndicesZeroBased'];times=durations(len(indices),phase['durationMs']);phase_rows=[]
        (out/'phases'/phase['id']).mkdir(exist_ok=True)
        for source_index,time in zip(indices,times):
            key=len(frames)+1;original_path=out/'all-frames'/f'frame-{source_index:03}.png';original=Image.open(original_path).convert('RGBA')
            shutil.copy2(original_path,out/'selected-source'/f'attack-{key:02}.png')
            raw=sample(original,palette,scale,offset,spec);clean=tidy(raw,basic)
            arr,holes=fill_holes(np.array(clean),spec['pinholeCleanup']);frame=Image.fromarray(arr);name=f'attack-{key:02}.png'
            frame.save(out/'native'/name);shutil.copy2(out/'native'/name,out/'phases'/phase['id']/name)
            white(frame).save(out/'white-native'/name);frame.resize((1536,896),N).save(out/'enlarged-4x'/name)
            index=np.zeros(arr.shape[:2],np.uint8)
            for c,rgb in enumerate(palette,1):index[np.all(arr[:,:,:3]==rgb,axis=2)&(arr[:,:,3]>0)]=c
            ind=Image.fromarray(index);ind.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));ind.info['transparency']=0;ind.save(out/'native-indexed'/name)
            assert ind.convert('RGBA').tobytes()==frame.tobytes()
            assert Image.open(out/'enlarged-4x'/name).convert('RGBA').tobytes()==frame.resize((1536,896),N).tobytes()
            assert set(np.unique(arr[:,:,3])).issubset({0,255})
            assert set(map(tuple,np.unique(arr[arr[:,:,3]>0,:3],axis=0))).issubset(set(map(tuple,palette)))
            changed=np.any(np.array(raw)!=arr,axis=2);mask=np.zeros_like(arr);mask[changed]=[230,40,130,255];Image.fromarray(mask).save(out/'masks'/name)
            row={'key':key,'phase':phase['id'],'phaseName':phase['name'],'sourceIndexZeroBased':source_index,'sourceOneBased':source_index+1,'sourceSha256':source_hashes[source_index],'png':f'native/{name}','sha256':sha(out/'native'/name),'durationMs':time,'bbox':list(frame.getbbox()),'cleanupPixels':int(changed.sum()),'filledHoles':holes}
            mapping.append(row);phase_rows.append(key);frames.append(frame);ds.append(time)
        phases.append({**phase,'keysOneBased':phase_rows,'startMs':sum(ds)-phase['durationMs'],'endMs':sum(ds)})
    assert len(frames)==24 and sum(ds)==1900
    hold=next(p for p in phases if p['id']=='spray-hold');assert hold['keysOneBased']==[12] and hold['startMs']==550 and hold['endMs']==1200
    hold_frame=frames[11];hold_frame.save(out/'phases/spray-hold/hold.png')
    # The complete immutable frame (including feet and nozzle) persists for the whole emission.
    for time in range(550,1200):
        cumulative=0
        for k,duration in enumerate(ds):
            cumulative+=duration
            if time<cumulative:break
        assert k==11 and frames[k].tobytes()==hold_frame.tobytes()
    nozzle=spec['effectAnchorProposal']['nozzleTip'];assert hold_frame.getpixel(tuple(nozzle))[3]==255
    union=Image.new('RGBA',(384,224))
    for f in frames:union.alpha_composite(f)
    bb=union.getbbox();crop=[bb[0]-10,bb[1]-10,bb[2]+10,bb[3]+10]
    assert crop[0]>=0 and crop[1]>=0 and crop[2]<=384 and crop[3]<=224
    animate(frames,ds,out/'previews/attack-native.webp');animate(frames,ds,out/'previews/attack-native.apng')
    crops=[white(f.crop(crop).resize(((crop[2]-crop[0])*4,(crop[3]-crop[1])*4),N)).convert('RGBA') for f in frames]
    animate(crops,ds,out/'previews/attack-white-4x.webp')
    animate(crops+[crops[-1]],ds+[spec['previewGapMs']],out/'previews/review-white-4x.webp')
    # Decoder verification, independent of browser playback.
    for path,expected in [('attack-native.webp',frames),('attack-native.apng',frames),('attack-white-4x.webp',crops)]:
        im=Image.open(out/'previews'/path);assert im.n_frames==len(expected)
        for k,(want,time) in enumerate(zip(expected,ds)):
            im.seek(k);im.load();assert im.convert('RGBA').tobytes()==want.tobytes();assert round(im.info['duration'])==time
    white(hold_frame.crop(crop).resize(((crop[2]-crop[0])*4,(crop[3]-crop[1])*4),N)).save(out/'previews/spray-hold-4x.png')
    board(frames,mapping,crop,[0,2,4,6,8,10,11,12,15,18,21,23],out/'previews/selected-12-poses.png')
    board(frames,mapping,crop,list(range(24)),out/'previews/all-24-poses.png')
    map_preview(out,frames,ds)
    info={**spec,'sourceSha256':sha(src),'frameCount':24,'cycleMs':1900,'durationsMs':ds,'mapping':mapping,'phases':phases,'paletteRgb':palette.tolist(),'scale':scale,'offset':offset,'sourceAnchor':anchor,'previewCrop':crop,'holdKeyOneBased':12,'holdSourceOneBased':13,'holdFrameSha256':sha(out/'native/attack-12.png'),'holdPixelsIdenticalForEntirePhase':True,'baseSha256':sha(base/'native/base-frame.png'),'runtimeIntegration':'not-performed','inkEffectGenerated':False}
    dump(out/'manifest.json',info);dump(out/'attack-spec.json',spec);page(out,info)
    (out/'README.md').write_text('''# 등짐분사수 공격 · 분사 자세 유지

업로드7680×4320은6×6,36개1280×720 셀로 무변형 분리한다. 원본1은 초기 참고 크기가 달라 제외하고 원본2~12를 전조11장/550ms, 원본13을 분사 유지1장/650ms, 원본14~25를 회복12장/700ms로 사용한다. 총24개 PNG/1900ms다. 화면의 원본 번호는1부터 시작한다.

분사 구간은 단일 전체 프레임으로 발·몸통·노즐을 고정한다. 새로운 포즈나 미세 반동을 합성하지 않는다. 준비/회복의 원본 움직임은 남기고, 원본2~5에서 정한 배율·오프셋 하나와 기존11색·384×224·이진 알파를 사용한다. 작은 잡점과4px 이하 둘러싸인 구멍만 정리한다. 기존 호스·사지 사이 빈 공간은 유지한다. 원본 생성 그림의 형태 변화는 남아 있으며 픽셀 변환/유지 타이밍은 검토 대상이다.

native/indexed/흰 배경/4배·단계별 PNG·원본 셀·보정 마스크·기본본·팔레트·입력 프롬프트·해시·원본 대응·장별 시간을 제공한다. 분사 실제 효과는 아직 없으며 효과 연결 기준점은 논리[124,136],왼쪽 방향의 아트 제안이다. 게임 연결과 판정 시점은 구현하지 않는다.

index.html은 분사 유지 시간을 바꿔 비교하고 전조/분사/회복으로 이동할 수 있다. 기본 공격1.9초 뒤500ms 정지 구간은 확인용 재생 간격이며 공격 데이터에 포함하지 않는다. 단일 재생은1.9초에서 멈춘다. review-white-4x.webp는 이 확인 간격을 포함한다. attack-native.webp/apng는 정확한24키/1.9초이고 분사 유지키는650ms다.

맵은 원본 배경·플레이어를 바꾸지 않은 합성 미리보기이며 실제 게임 캡처가 아니다. 기존 걷기 채택본·전투 수치·Unity는 변경하지 않는다.
''',encoding='utf-8')
    included={'source','all-frames','selected-source','native','native-indexed','white-native','enlarged-4x','phases','references','masks','previews','index.html','preview.js','manifest.json','attack-spec.json','README.md'}
    with zipfile.ZipFile(out/'K4-attack-hold-20261101.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            rel=p.relative_to(out)
            if p.is_file() and rel.parts[0] in included:z.write(p,rel.as_posix())
    print(json.dumps({'frames':24,'extracted':36,'attackMs':sum(ds),'holdKey':12,'holdSource':13,'holdMs':650,'paletteColors':11,'holdExact':True,'sourceSha256':sha(src),'decoderPixelsAndTiming':'pass'},ensure_ascii=False))


def page(out, info):
    script='''const data=DATA;let elapsed=0,running=true,repeat=true,last=null,hold=650;const images=[];let bg;
const $=s=>document.querySelector(s),c=$('#sprite'),ctx=c.getContext('2d'),mc=$('#map'),mx=mc.getContext('2d');
const load=p=>new Promise((ok,no)=>{let im=new Image;im.onload=()=>ok(im);im.onerror=()=>no(Error(p));im.src=p});
function times(){let ds=[...data.durationsMs];ds[data.holdKeyOneBased-1]=hold;return ds}
function state(){let ds=times(),total=ds.reduce((a,b)=>a+b,0),t=Math.min(elapsed,total-1),k=0;while(k<ds.length-1&&t>=ds[k]){t-=ds[k];k++}return {ds,total,k,gap:elapsed>=total}}
function draw(){if(images.length!==data.frameCount||!bg)return;let {ds,total,k,gap}=state(),b=data.previewCrop,w=b[2]-b[0],h=b[3]-b[1],z=+$('#zoom').value;c.width=w;c.height=h;c.style.width=w*z+'px';c.style.height=h*z+'px';ctx.imageSmoothingEnabled=false;ctx.fillStyle=$('#bg').value;ctx.fillRect(0,0,w,h);ctx.drawImage(images[k],b[0],b[1],w,h,0,0,w,h);mx.imageSmoothingEnabled=false;mx.clearRect(0,0,960,540);mx.drawImage(bg,0,0);mx.drawImage(images[k],238,94);$('#key').value=k;let row=data.mapping[k];$('#phase').textContent=gap?'확인용 재생 간격':row.phaseName;$('#label').textContent=`키${k+1}/24 · 원본${row.sourceOneBased} · ${ds[k]}ms · ${(elapsed/1000).toFixed(2)} / ${(total/1000).toFixed(2)}초`;$('#holdvalue').textContent=hold+'ms';$('#bar').style.width=Math.min(elapsed,total)/total*100+'%'}
function tick(t){if(last===null)last=t;if(running){elapsed+=t-last;let total=times().reduce((a,b)=>a+b,0);if(repeat){elapsed%=total+data.previewGapMs}else if(elapsed>=total){elapsed=total;running=false;$('#play').textContent='재생'}}last=t;draw();requestAnimationFrame(tick)}
function seekKey(k){running=false;elapsed=times().slice(0,k).reduce((a,b)=>a+b,0);$('#play').textContent='재생';draw()}
Promise.all([Promise.all(data.mapping.map(r=>load(r.png))),load('references/C-game.png')]).then(([ims,scene])=>{images.push(...ims);bg=scene;$('#status').textContent='24키 로드 완료 · 분사 중에는 전체 프레임 고정';requestAnimationFrame(tick)}).catch(e=>$('#status').textContent='파일 로드 실패: '+e.message);
$('#play').onclick=()=>{running=!running;last=performance.now();$('#play').textContent=running?'일시정지':'재생'};$('#restart').onclick=()=>{elapsed=0;running=true;last=performance.now();$('#play').textContent='일시정지'};$('#once').onclick=()=>{repeat=false;$('#repeat').checked=false;elapsed=0;running=true;last=performance.now();$('#play').textContent='일시정지'};$('#repeat').onchange=e=>{repeat=e.target.checked;elapsed=0};$('#key').oninput=e=>seekKey(+e.target.value);document.querySelectorAll('[data-phase]').forEach(b=>b.onclick=()=>seekKey(data.phases.find(p=>p.id===b.dataset.phase).keysOneBased[0]-1));['zoom','bg'].forEach(id=>$('#'+id).onchange=draw);$('#hold').oninput=e=>{let prior=hold,s=state();hold=+e.target.value;if(s.k>11)elapsed+=hold-prior;else if(s.k===11)elapsed=550;draw()};document.addEventListener('visibilitychange',()=>last=performance.now());
$('#save').onclick=()=>{const blob=new Blob([JSON.stringify({id:'K4',status:'local-art-timing-selection-not-runtime',mapping:data.mapping.map((r,i)=>({...r,durationMs:times()[i]})),durationsMs:times(),holdMs:hold,attackMs:times().reduce((a,b)=>a+b,0),previewGapExcluded:true},null,2)],{type:'application/json'});let u=URL.createObjectURL(blob),a=document.createElement('a');a.href=u;a.download='K4-attack-timing.json';a.click();setTimeout(()=>URL.revokeObjectURL(u),1000)};'''.replace('DATA',json.dumps(info,ensure_ascii=False))
    (out/'preview.js').write_text(script,encoding='utf-8')
    links=''.join(f'<a href="{r["png"]}">키{r["key"]:02} · {escape(r["phaseName"])} · 원본{r["sourceOneBased"]}</a>' for r in info['mapping'])
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>등짐분사수 공격 · 분사 자세 유지</title><style>body{margin:0;background:#f2f0ec;color:#272a34;font:16px/1.7 system-ui}main{max-width:1250px;padding:28px;margin:auto}h1{margin:0}h2{font-size:22px;margin:30px 0 14px}p{color:#596171}button,select{padding:8px 12px;font:inherit;background:white;border:1px solid #ccc;border-radius:7px}button{cursor:pointer}a{color:#34548d}.controls{display:flex;flex-wrap:wrap;gap:12px;align-items:center;margin:16px 0}.viewport{overflow:auto;background:white;border:1px solid #ddd;border-radius:10px}canvas,img{image-rendering:pixelated}.viewport canvas{display:block}img{max-width:100%;height:auto}.loop{display:block;margin:auto}.phase{font-weight:700;font-size:20px}.track{height:6px;background:#d9d6d0;margin:14px 0}.track div{height:100%;background:#b85f55}.links{display:flex;gap:14px;flex-wrap:wrap}.note{font-size:14px}</style><main><h1>등짐분사수 · 분사 자세를 유지하는 공격</h1><p>받은36셀에서 전조11장 → 분사 자세1장 유지 → 회복12장을 골랐습니다. 기존11색·몸 높이112px 기준으로 픽셀화했습니다.<br>분사 중에는 발·몸·노즐을 같은 한 장으로 고정합니다. 잉크 줄기 이펙트와 Unity 연결은 아직 없습니다.</p><div class="links"><a href="K4-attack-hold-20261101.zip" download>24키·원본36셀·참조·시간 ZIP</a><a href="manifest.json">단계·순서·시간·효과 기준점</a><a href="previews/attack-native.apng">정확한1.9초 APNG</a></div>
<h2>공격 확인 · 분사 유지 시간 조절</h2><div class="controls"><button id="play">일시정지</button><button id="restart">처음부터</button><button id="once">한 번만 재생</button><label><input id="repeat" type="checkbox" checked>확인용 반복</label><select id="zoom"><option value="1">실제1배</option><option value="4" selected>4배</option><option value="8">8배</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 벽색</option><option value="#708b92">푸른 원경색</option><option value="#111316">검정 근경색</option></select></div><div class="controls"><label>분사 자세 유지 <input id="hold" type="range" min="400" max="1200" step="50" value="650"></label><span id="holdvalue">650ms</span><button id="save">비교한 시간표 저장</button></div><div class="controls"><button data-phase="windup">전조 첫 키</button><button data-phase="spray-hold">분사 고정 키</button><button data-phase="recovery">회복 첫 키</button><span class="phase" id="phase"></span></div><div class="track"><div id="bar"></div></div><div class="viewport"><canvas id="sprite"></canvas></div><div class="controls"><input id="key" type="range" min="0" max="23" value="0"><span id="label"></span></div><p class="note" id="status">프레임 준비 중…</p><p class="note">기본 전조0.55초 / 분사 고정0.65초 / 회복0.7초 = 공격1.9초. 반복 사이0.5초는 확인용 간격이며 공격 시간에 포함하지 않습니다. 슬라이더는 이 페이지에서만 바뀌며 게임 수치를 변경하지 않습니다.</p>
<h2>원본 맵 위 같은 크기</h2><div class="viewport"><canvas id="map" width="960" height="540"></canvas></div><p class="note">원본 배경과 승인 플레이어를 바꾸지 않은 합성 미리보기입니다. 실제 게임 캡처가 아닙니다.</p><h2>선정12자세 · 둘째 줄 첫 칸이650ms 유지 키</h2><img src="previews/selected-12-poses.png"><details><summary>선정24개 PNG 전체 보기</summary><img src="previews/all-24-poses.png"><div class="links">'''+links+'''</div></details><details><summary>분사 고정 자세 확대와 효과 연결 위치</summary><img src="previews/spray-hold-4x.png"><p>분사 유지: 선정키12 / 원본13. 논리 노즐 끝[125,136], 효과 시작 제안[124,136], 왼쪽[-1,0]. 이 좌표는 아트 참고이며 런타임 바인딩은 아닙니다.</p></details><p class="note">원본1은 초기 참고 크기가 달라 제외했고 뒤쪽 중복 대기는 걷어냈습니다. 단일 공통 배율/위치를 사용하며 프레임별 몸 크기나 하체 합성을 하지 않았습니다. 원본·36셀·선정셀·보정 마스크·기본본·팔레트·프롬프트를 보존합니다. 받은 동작은 사용자가 선택했고 픽셀 변환과 분사 유지 연결은 이번 검토 대상입니다. 기존 걷기 채택본은 유지합니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')


if __name__=='__main__':build()
