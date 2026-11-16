"""Extract the accepted B3 slam and retain a reversible, rejected support-repair trial."""
import json
import math
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_fill_holes, distance_transform_edt

from k4_d_pixel_walk import ROOT,N,sha,foreground_bounds,sample,tidy
from k4_d_pixel_cleanup import fill_holes
from k4_attack_return import durations
from b3_walk_return import white,dump
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_b3_slam_return.json'


def plate_axis(a,colors):
    mask=(a[:,:,3]>0)&np.any(np.all(a[:,:,:3,None]==np.array(colors).T[None,None],axis=2),axis=2)
    y,x=np.where(mask);points=np.stack([x,y],1);center=points.mean(0)
    _,vectors=np.linalg.eigh(np.cov(points.T));axis=vectors[:,-1]
    if axis[0]>0:axis=-axis
    return center,math.atan2(axis[1],axis[0])


def support_patch(base,box,colors):
    mask=np.zeros(base.shape[:2],bool);x,y,x1,y1=box;mask[y:y1,x:x1]=True
    mask&=np.any(np.all(base[:,:,:3,None]==np.array(colors).T[None,None],axis=2),axis=2)
    part=np.zeros_like(base);part[mask]=base[mask];return part


def repair_attempt(a,base,source_number,config):
    bc,ba=plate_axis(base,config['creamColors']);center,angle=plate_axis(a,config['creamColors'])
    delta=(angle-ba+math.pi)%(2*math.pi)-math.pi
    early=source_number<=config['switchAfterSourceOneBased']
    part=support_patch(base,config['frontPadBox'] if early else config['smallFrontSupportBox'],[[84,57,63]] if early else config['colors'])
    ys,xs=np.where(part[:,:,3]>0);points=np.stack([xs,ys],1)
    rotation=np.array([[math.cos(delta),-math.sin(delta)],[math.sin(delta),math.cos(delta)]])
    targets=np.rint((points-bc)@rotation.T+center).astype(int);layer=np.zeros_like(a)
    for (x,y),rgba in zip(targets,part[ys,xs]):
        if 0<=x<a.shape[1] and 0<=y<192:layer[y,x]=rgba
    solid=layer[:,:,3]>0;holes=binary_fill_holes(solid)&~solid
    if holes.any():
        nearest=distance_transform_edt(~solid,return_distances=False,return_indices=True);layer[holes]=layer[nearest[0][holes],nearest[1][holes]]
    added=(a[:,:,3]==0)&(layer[:,:,3]>0);result=a.copy();result[added]=layer[added]
    assert np.array_equal(result[a[:,:,3]>0],a[a[:,:,3]>0])
    assert np.array_equal(result[~added],a[~added])
    stats={'part':'front-pad' if early else 'small-front-support','angleDegrees':math.degrees(delta),'addedPixels':int(added.sum()),'addedCoordinatesXY':np.argwhere(added)[:,::-1].tolist()}
    return Image.fromarray(result),Image.fromarray(layer),stats


def save_variant(folder,frames,times,palette,crop,gap):
    for name in ('native','native-indexed','white-native','enlarged-4x','previews'):(folder/name).mkdir(parents=True,exist_ok=True)
    pal=np.concatenate([np.zeros((1,3),np.uint8),palette]);hashes=[]
    for i,frame in enumerate(frames,1):
        name=f'attack-{i:02}.png';frame.save(folder/'native'/name);white(frame).save(folder/'white-native'/name);frame.resize((1536,896),N).save(folder/'enlarged-4x'/name)
        a=np.array(frame);index=np.zeros(a.shape[:2],np.uint8)
        for c,rgb in enumerate(palette,1):index[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=c
        ind=Image.fromarray(index);ind.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));ind.info['transparency']=0;ind.save(folder/'native-indexed'/name)
        assert ind.convert('RGBA').tobytes()==frame.tobytes()
        assert set(np.unique(a[:,:,3])).issubset({0,255})
        assert set(map(tuple,np.unique(a[a[:,:,3]>0,:3],axis=0))).issubset(set(map(tuple,palette)))
        assert Image.open(folder/'enlarged-4x'/name).convert('RGBA').tobytes()==frame.resize((1536,896),N).tobytes()
        hashes.append(sha(folder/'native'/name))
    animate(frames,times,folder/'previews/attack-native.webp');animate(frames,times,folder/'previews/attack-native.apng')
    crops=[white(f.crop(crop).resize(((crop[2]-crop[0])*4,(crop[3]-crop[1])*4),N)).convert('RGBA') for f in frames]
    animate(crops,times,folder/'previews/attack-white-4x.webp');animate(crops+[crops[-1]],times+[gap],folder/'previews/review-white-4x.webp')
    for filename,expected in [('attack-native.webp',frames),('attack-native.apng',frames),('attack-white-4x.webp',crops)]:
        im=Image.open(folder/'previews'/filename);assert im.n_frames==len(frames)
        for i,(frame,time) in enumerate(zip(expected,times)):
            im.seek(i);im.load();assert im.convert('RGBA').tobytes()==frame.tobytes() and round(im.info['duration'])==time
    return hashes


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];base_path=ROOT/spec['base']
    for sub in ('source','all-frames','selected-source','references','masks','repair-layers','previews'):(out/sub).mkdir(parents=True,exist_ok=True)
    source=out/'source'/Path(spec['sourceFile']).name
    if not source.exists():shutil.copy2(spec['sourceFile'],source)
    assert sha(source)==sha(Path(spec['sourceFile']))
    sheet=Image.open(source).convert('RGBA');cw,ch=spec['cellSize'];cols,rows=spec['grid'];assert sheet.size==(cw*cols,ch*rows)
    cell_hashes=[]
    for i in range(cols*rows):
        x,y=(i%cols)*cw,(i//cols)*ch;cell=sheet.crop((x,y,x+cw,y+ch));p=out/'all-frames'/f'frame-{i:03}.png';cell.save(p)
        assert Image.open(p).tobytes()==cell.tobytes();cell_hashes.append(sha(p))
    for src,name in [(base_path/'native/base-frame.png','base-frame.png'),(base_path/'palette.json','palette.json'),(ROOT/spec['inputHandoff']/'prompt-en.txt','input-prompt.txt'),(ROOT/spec['inputHandoff']/'manifest.json','input-manifest.json'),(SPEC,'presentation-spec.json')]:shutil.copy2(src,out/'references'/name)
    basic=Image.open(base_path/'native/base-frame.png').convert('RGBA');base=np.array(basic);palette=np.unique(base[base[:,:,3]>0,:3],axis=0);assert len(palette)==10
    boxes=np.array([foreground_bounds(Image.open(out/'all-frames'/f'frame-{i:03}.png')) for i in spec['scaleReferenceFramesZeroBased']])
    scale=float(spec['targetReferenceHeight']/np.median(boxes[:,3]-boxes[:,1]));anchor=[float(np.median((boxes[:,0]+boxes[:,2])/2)),float(np.median(boxes[:,3]))]
    offset=[192-anchor[0]*scale,spec['baseline']-anchor[1]*scale]
    frames=[];trials=[];mapping=[];times=[];phases=[]
    for phase in spec['phases']:
        numbers=phase['sourceOneBased'];count=len(numbers)
        if 'lastKeyHoldMs' in phase:ds=durations(count-1,phase['durationMs']-phase['lastKeyHoldMs'])+[phase['lastKeyHoldMs']]
        elif 'firstKeyHoldMs' in phase:ds=[phase['firstKeyHoldMs']]+durations(count-1,phase['durationMs']-phase['firstKeyHoldMs'])
        else:ds=durations(count,phase['durationMs'])
        start=len(frames)
        for num,time in zip(numbers,ds):
            key=len(frames)+1;src=out/'all-frames'/f'frame-{num-1:03}.png';original=Image.open(src).convert('RGBA');shutil.copy2(src,out/'selected-source'/f'attack-{key:02}.png')
            raw=sample(original,palette,scale,offset,spec);clean=tidy(raw,basic);a,holes=fill_holes(np.array(clean),spec['pinholeCleanup']);frame=Image.fromarray(a)
            trial,layer,repair=repair_attempt(a,base,num,spec['repairTrial']);trials.append(trial);layer.save(out/'repair-layers'/f'attack-{key:02}.png')
            changed=np.any(np.array(raw)!=a,axis=2);mask=np.zeros_like(a);mask[changed]=[230,40,130,255];Image.fromarray(mask).save(out/'masks'/f'attack-{key:02}.png')
            bb=frame.getbbox();assert bb[0]>0 and bb[1]>0 and bb[2]<384 and bb[3]<224
            frames.append(frame);times.append(time);mapping.append({'key':key,'phase':phase['id'],'phaseName':phase['name'],'sourceOneBased':num,'sourceIndexZeroBased':num-1,'sourceSha256':cell_hashes[num-1],'durationMs':time,'bbox':list(bb),'cleanupPixels':int(changed.sum()),'holesFilled':holes,'repairTrial':repair})
        phases.append({**phase,'startMs':sum(times)-phase['durationMs'],'endMs':sum(times),'keysOneBased':list(range(start+1,len(frames)+1))})
    assert len(frames)==19 and sum(times)==1500
    union=Image.new('RGBA',(384,224))
    for f in frames+trials:union.alpha_composite(f)
    bb=union.getbbox();crop=[max(0,bb[0]-8),max(0,bb[1]-8),min(384,bb[2]+8),min(224,bb[3]+8)]
    raw_hashes=save_variant(out/'original',frames,times,palette,crop,spec['previewGapMs']);trial_hashes=save_variant(out/'repair-trial',trials,times,palette,crop,spec['previewGapMs'])
    map_source=ROOT/'outputs/waterway/pixel-base-step1-20261031/references/C-game.png';shutil.copy2(map_source,out/'references/C-game.png');background=Image.open(map_source).convert('RGBA')
    for name,poses in [('original',frames),('repair-trial',trials)]:
        scenes=[]
        for pose in poses:
            scene=background.copy();scene.alpha_composite(pose,(238,94));scenes.append(scene)
        animate(scenes,times,out/name/'previews/map.webp')
    info={**spec,'sourceSha256':sha(source),'frameCount':19,'attackMsProposal':1500,'durationsMs':times,'mapping':mapping,'phases':phases,'scale':scale,'offset':offset,'sourceAnchor':anchor,'paletteRgb':palette.tolist(),'previewCrop':crop,'originalNativeHashes':raw_hashes,'repairTrialNativeHashes':trial_hashes,'defaultVariant':'original','runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info);page(out,info);boards(out,frames,trials,mapping,crop)
    (out/'README.md').write_text('''# 쐐기보행기 · 선정19장 찍기

업로드36셀을1280×720로 무변형 분리하고19장을 선택한다. 첫1번은 작은 참고 이미지라 제외하며 실제중립2~4번 높이에 기본100px 기준공통배율/위치하나를 적용한다. 들기·버팀8장450ms(정점120ms), 찍기3장150ms, 회복8장900ms(첫180ms), 총19장1500ms는 기존제작제안이다. 논리384×224에 들린전체가 들어오며 기본몸크기와10색을 유지한다. 반환원본의 몸/판/발위치변화와바닥잔해는 남긴다. 발을프레임별로이동하거나 전체를재조정하지않는다.

original은다리무보충 픽셀변환본이다. repair-trial은기존기본본의 짧은앞발/앞지지대픽셀을판의중심과주축에맞춰 정수배치해 뒤에만더한시도다. 기존불투명픽셀은건드리지않는다. 큰들기에서발끝이쐐기위로튀어나와기본적용에서제외했다. 사용자지시에따라원본형태를기본으로사용하며시도본을비교보존한다. 다리복원이완료됐다고표현하지않는다.

원본시트/36셀·선정셀·기본본/팔레트·프롬프트·보정마스크·추가레이어·좌표/시간/해시·두버전19PNG/indexed/흰배경/정수4배와무손실WebP/APNG를보존한다. 페이지는같은시간축비교/단계탐색/키탐색/확대/배경/단일재생과원본맵합성을제공한다. 반복사이600ms는확인용간격이며공격시간에서제외된다. 전투/효과/Unity통합은없고실제게임캡처가아닌합성미리보기다. 돌진은이찍기클립에포함하지않는다.
''',encoding='utf-8')
    if spec.get('approval')=='approved-by-user':
        p=out/'README.md';p.write_text(p.read_text(encoding='utf-8')+'\n사용자가 original 다리 무보충19장의 픽셀·연결을 채택했다. 보충 시도는 미적용으로 보존한다.\n',encoding='utf-8')
    allowed={'source','all-frames','selected-source','original','repair-trial','references','masks','repair-layers','previews','manifest.json','index.html','preview.js','README.md'}
    with zipfile.ZipFile(out/'B3-slam-selected19-20261101.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            rel=p.relative_to(out)
            if p.is_file() and rel.parts[0] in allowed:z.write(p,rel.as_posix())
    print(json.dumps({'extractedCells':36,'selected':19,'phaseCounts':[len(p['sourceOneBased']) for p in phases],'phaseMs':[p['durationMs'] for p in phases],'attackMs':1500,'paletteColors':10,'default':'original','repair':'attempted-not-applied','decoderPixelsAndTiming':'pass'}))


def boards(out,frames,trials,mapping,crop):
    cols=5;z=2;w=(crop[2]-crop[0])*z;h=(crop[3]-crop[1])*z;font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',15)
    board=Image.new('RGB',(w*cols,(h+50)*4),'white');draw=ImageDraw.Draw(board)
    for i,frame in enumerate(frames):
        x,y=(i%cols)*w,(i//cols)*(h+50);board.paste(white(frame.crop(crop).resize((w,h),N)),(x,y+50));row=mapping[i]
        draw.text((x+8,y+2),f'키{i+1:02} · 원본{row["sourceOneBased"]}',font=font,fill='#25252d');draw.text((x+8,y+25),f'{row["phaseName"]} / {row["durationMs"]}ms',font=font,fill='#874e4d')
    board.save(out/'previews/selected-19.png')
    chosen=[0,4,6,10,14,18];comparison=Image.new('RGB',(w*4,(h+50)*3),'white');draw=ImageDraw.Draw(comparison)
    for pos,k in enumerate(chosen):
        for side,poses in enumerate([frames,trials]):
            x=((pos%2)*2+side)*w;y=(pos//2)*(h+50);comparison.paste(white(poses[k].crop(crop).resize((w,h),N)),(x,y+50));draw.text((x+8,y+4),f'키{k+1} · '+('무보충' if not side else '보충 시도·미적용'),font=font,fill='#25252d')
    comparison.save(out/'previews/repair-comparison.png')


def page(out,info):
    script='''const data=DATA;let elapsed=0,running=true,repeat=true,last=null;let originals=[],trials=[],scene;const $=s=>document.querySelector(s);const load=p=>new Promise((ok,no)=>{const i=new Image;i.onload=()=>ok(i);i.onerror=()=>no(Error(p));i.src=p});function key(){let t=Math.min(elapsed,1499),k=0;while(k<18&&t>=data.durationsMs[k]){t-=data.durationsMs[k];k++}return k}
function paint(c,frame){let b=data.previewCrop,w=b[2]-b[0],h=b[3]-b[1];if(c.width!==w||c.height!==h){c.width=w;c.height=h}const z=+$('#zoom').value;c.style.width=w*z+'px';c.style.height=h*z+'px';let x=c.getContext('2d');x.imageSmoothingEnabled=false;x.fillStyle=$('#bg').value;x.fillRect(0,0,w,h);x.drawImage(frame,b[0],b[1],w,h,0,0,w,h)}
function draw(){if(originals.length!==19||trials.length!==19||!scene)return;let k=key();paint($('#original'),originals[k]);paint($('#trial'),trials[k]);$('#key').value=k;let row=data.mapping[k];$('#label').textContent=` 키${k+1}/19 · 원본${row.sourceOneBased} · ${row.durationMs}ms · ${elapsed>=1500?'확인용 간격':row.phaseName}`;let x=$('#map').getContext('2d');x.imageSmoothingEnabled=false;x.clearRect(0,0,960,540);x.drawImage(scene,0,0);x.drawImage(($('#mapvariant').value==='original'?originals:trials)[k],238,94)}
function tick(t){if(last===null)last=t;if(running){elapsed+=t-last;if(repeat)elapsed%=2100;else if(elapsed>=1500){elapsed=1500;running=false;$('#play').textContent='재생'}}last=t;draw();requestAnimationFrame(tick)}
Promise.all([Promise.all(data.mapping.map(r=>load('original/native/attack-'+String(r.key).padStart(2,'0')+'.png'))),Promise.all(data.mapping.map(r=>load('repair-trial/native/attack-'+String(r.key).padStart(2,'0')+'.png'))),load('references/C-game.png')]).then(([a,b,bg])=>{originals=a;trials=b;scene=bg;$('#status').textContent='선정19장과 보충 시도본 준비 완료';requestAnimationFrame(tick)}).catch(e=>$('#status').textContent=e.message);
$('#play').onclick=()=>{running=!running;last=performance.now();$('#play').textContent=running?'정지':'재생'};$('#restart').onclick=()=>{elapsed=0;running=true;last=performance.now();$('#play').textContent='정지'};$('#once').onclick=()=>{elapsed=0;running=true;repeat=false;$('#repeat').checked=false;last=performance.now();$('#play').textContent='정지'};$('#repeat').onchange=e=>{repeat=e.target.checked;elapsed=0};$('#key').oninput=e=>{running=false;$('#play').textContent='재생';elapsed=data.durationsMs.slice(0,+e.target.value).reduce((a,b)=>a+b,0);draw()};document.querySelectorAll('[data-phase]').forEach(b=>b.onclick=()=>{running=false;$('#play').textContent='재생';elapsed=data.phases.find(p=>p.id===b.dataset.phase).startMs;draw()});['zoom','bg','mapvariant'].forEach(id=>$('#'+id).onchange=draw);document.addEventListener('visibilitychange',()=>last=performance.now());'''.replace('DATA',json.dumps(info,ensure_ascii=False))
    (out/'preview.js').write_text(script,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>쐐기보행기 · 선정19장 찍기</title><style>body{margin:0;background:#f2f0ec;color:#252935;font:16px/1.65 system-ui}main{max-width:1450px;padding:28px;margin:auto}h1{margin:0;font-size:30px}h2{font-size:22px}p{color:#596171}.controls{display:flex;gap:12px;flex-wrap:wrap;align-items:center;margin:18px 0}button,select{font:inherit;padding:8px 12px;background:white;border:1px solid #ccc;border-radius:7px}a{color:#34548d}.view{background:white;overflow:auto;border:1px solid #ddd}.view canvas{display:block}canvas,img{image-rendering:pixelated}img{max-width:100%;height:auto}details{margin:24px 0;padding:16px;background:#fff}.note{font-size:14px}</style><main><h1>쐐기보행기 · 선정19장 들기/찍기</h1><p>받은 동작을 기준으로 들기·버팀8장 → 찍기3장 → 회복8장을 골랐습니다. 기존10색·384×224 논리 픽셀·몸 높이100px 기준 공통 배율입니다.<br>다리 보충은 큰 들기에서 발끝이 쐐기 위로 튀어나와 기본 적용에서 제외했습니다. 아래 기본 재생은 다리 무보충본이며 시도본은 펼쳐 비교할 수 있습니다.</p><div class="controls"><a href="B3-slam-selected19-20261101.zip" download>19장 PNG·4배·원본36셀·시도본 ZIP</a><a href="original/previews/attack-native.apng">무보충본 정확한1.5초 APNG</a><a href="manifest.json">순서·시간·보충 시도 좌표</a></div><div class="controls"><button id="play">정지</button><button id="restart">처음부터</button><button id="once">한 번 재생</button><label><input id="repeat" type="checkbox" checked>확인용 반복</label><select id="zoom"><option value="1">실제1배</option><option value="2" selected>2배</option><option value="4">4배</option><option value="8">8배</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 벽색</option><option value="#708b92">푸른 원경색</option><option value="#111316">검정 근경색</option></select></div><div class="controls"><button data-phase="lift-hold">들기 처음</button><button data-phase="slam">찍기 처음</button><button data-phase="recovery">회복 처음</button><input id="key" type="range" min="0" max="18" value="0"><span id="label"></span></div><h2>기본 · 원본 형태 유지</h2><div class="view"><canvas id="original"></canvas></div><p id="status">프레임 준비 중…</p><p class="note">들기·버팀450ms / 찍기150ms / 회복900ms = 제작 제안1.5초. 반복 사이600ms는 확인용 간격입니다. 공격은 단일 동작이며 끝과 처음이 이어지는 걷기 루프가 아닙니다.</p><details><summary>다리 보충 시도본 비교 · 기본 적용 제외</summary><p>기존 기본본의 앞발/앞지지대 픽셀을 쐐기 각도에 따라 뒤에만 더했습니다. 낮은 자세에는 도움이 되지만 정점에서 실루엣이 어색해 기본 사용에서 제외했습니다. 원래 불투명 픽셀은 수정하지 않았습니다.</p><div class="view"><canvas id="trial"></canvas></div><img src="previews/repair-comparison.png"><a href="repair-trial/previews/review-white-4x.webp">시도본 개별 재생</a></details><h2>선정19장</h2><img src="previews/selected-19.png"><h2>원본 맵 위 실제 크기</h2><div class="controls"><select id="mapvariant"><option value="original">기본 · 무보충</option><option value="repair-trial">보충 시도 · 미적용</option></select></div><div class="view"><canvas id="map" width="960" height="540"></canvas></div><p class="note">원본960×540 배경과 승인 플레이어를 유지한 합성 미리보기이며 실제 게임 캡처가 아닙니다. 원본 생성 동작의 몸/판/발 변화와 바닥 잔해는 남아 있습니다. 전투·Unity는 연결하지 않았습니다. 원본 시트·36셀·선정셀·기본본·팔레트·프롬프트·보정 마스크와 시도 레이어를 보존합니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')

    if info.get('approval')=='approved-by-user':
        p=out/'index.html';html=p.read_text(encoding='utf-8').replace('<h1>쐐기보행기 · 선정19장 들기/찍기</h1>','<h1>쐐기보행기 · 무보충19장 채택</h1>');p.write_text(html,encoding='utf-8')


if __name__=='__main__':build()
