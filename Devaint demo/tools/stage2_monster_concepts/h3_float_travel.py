"""Make a legless float travel study using reversible integer copies of H3 pixels."""
import json
import shutil
import zipfile
from html import escape

import numpy as np
from PIL import Image
from k4_d_pixel_walk import ROOT,N,sha
from b3_walk_return import white,dump
from m3_walk_candidates import durations
from thrower_walk_candidates import board
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_h3_float_travel.json'


def float_pose(source,dy,tail,origin,spec):
    a=np.array(source);out=np.zeros_like(a);top=origin[1]
    ys=np.arange(a.shape[0]);weight=np.clip((ys-top-spec['clothLagStartsAt'])/(spec['clothLagFullAt']-spec['clothLagStartsAt']),0,1);dx=np.rint(weight*tail).astype(int)
    for y,shift in enumerate(dx):
        target=y+dy
        if not 0<=target<a.shape[0]:
            assert not np.any(a[y,:,3]);continue
        assert shift>=0
        out[target,shift:]=a[y,:a.shape[1]-shift] if shift else a[y]
    assert (out[:,:,3]>0).sum()==(a[:,:,3]>0).sum()
    assert {tuple(v) for v in out[out[:,:,3]>0]}=={tuple(v) for v in a[a[:,:,3]>0]}
    # The mask/hood region is a rigid vertical translation of the same original pixels.
    x,y,x1,y1=source.getbbox();cut=top+spec['headRigidHeight']
    assert np.array_equal(out[top+dy:cut+dy,x:x1],a[top:cut,x:x1])
    return Image.fromarray(out),dx.tolist()


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];base=ROOT/spec['base'];prior=ROOT/spec['prior'];local=ROOT/spec['localSources']
    for folder in ('references','previews'):(out/folder).mkdir(parents=True,exist_ok=True)
    for name,src in [('base-frame.png',base/'native/base-frame.png'),('palette.json',base/'palette.json'),('prior-metadata.json',prior/'metadata.json')]:shutil.copy2(src,out/'references'/name)
    basic=Image.open(base/'native/base-frame.png').convert('RGBA');origin=basic.getbbox()[:2];a=np.array(basic);palette=np.unique(a[a[:,:,3]>0,:3],axis=0);assert len(palette)==8
    sources=[]
    for key in range(4):
        src=local/f'idle-{key:02}.png';shutil.copy2(src,out/'references'/f'local-{key:02}.png');sources.append(Image.open(src).convert('RGBA'))
    moving=[];mapping=[]
    for i,(key,dy,tail) in enumerate(zip(spec['sourceKeys'],spec['bodyDy'],spec['tailDx'])):
        frame,rows=float_pose(sources[key],dy,tail,origin,spec);moving.append(frame);mapping.append({'key':i+1,'localSourceKey':key,'sourceSha256':sha(local/f'idle-{key:02}.png'),'bodyDy':dy,'tailDx':tail,'rowDx':rows,'opaquePixelCount':int((np.array(frame)[:,:,3]>0).sum())})
    assert moving[0].tobytes()==basic.tobytes()
    oldmeta=json.loads((prior/'metadata.json').read_text(encoding='utf-8'));old=[Image.open(prior/'frames'/f'idle-{i:02}.png').convert('RGBA') for i in range(8)]
    union=Image.new('RGBA',(384,224))
    for f in old+moving:union.alpha_composite(f)
    bb=union.getbbox();box=[bb[0]-8,bb[1]-8,bb[2]+8,bb[3]+8];pal=np.concatenate([np.zeros((1,3),np.uint8),palette]);rows=[]
    # Map previews preserve the original background and player; sprites are placed on the same scale.
    refs=ROOT/'outputs/waterway/pixel-base-step1-20261031/references';shutil.copy2(refs/'C-game.png',out/'references/C-game.png');shutil.copy2(refs/'assets/approved-player.png',out/'references/approved-player.png');background=Image.open(refs/'C-game.png').convert('RGBA')
    for c in spec['candidates']:
        folder=out/c['id']
        for sub in ('native','native-indexed','white-native','enlarged-4x','previews'):(folder/sub).mkdir(parents=True,exist_ok=True)
        frames=old if c.get('reusePrior') else moving;ds=oldmeta['durations'] if c.get('reusePrior') else durations(len(frames),c['cycleMs']);assert sum(ds)==c['cycleMs']
        for i,f in enumerate(frames):
            name=f'float-{i+1:02}.png'
            if c.get('reusePrior'):shutil.copy2(prior/'frames'/f'idle-{i:02}.png',folder/'native'/name)
            else:f.save(folder/'native'/name)
            white(f).save(folder/'white-native'/name);f.resize((1536,896),N).save(folder/'enlarged-4x'/name)
            a=np.array(f);index=np.zeros(a.shape[:2],np.uint8)
            for k,rgb in enumerate(palette,1):index[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=k
            indexed=Image.fromarray(index);indexed.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));indexed.info['transparency']=0;indexed.save(folder/'native-indexed'/name);assert indexed.convert('RGBA').tobytes()==f.tobytes()
        animate(frames,ds,folder/'previews/native.webp');animate(frames,ds,folder/'previews/native.apng')
        crops=[white(f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N)).convert('RGBA') for f in frames];animate(crops,ds,folder/'previews/white-4x.webp')
        source_indices=list(range(len(frames))) if c.get('reusePrior') else spec['sourceKeys']
        board(frames,source_indices,list(range(len(frames))),box,4,2,folder/'previews/keys.png')
        scenes=[]
        for f in frames:
            scene=background.copy();scene.alpha_composite(f,(238,94));scenes.append(scene)
        animate(scenes,ds,folder/'previews/map-float.webp')
        row={**c,'durationsMs':ds,'paths':[f'{c["id"]}/native/float-{i+1:02}.png' for i in range(len(frames))],'sourceMapping':oldmeta if c.get('reusePrior') else mapping};rows.append(row);dump(folder/'manifest.json',row)
    info={**spec,'candidates':rows,'paletteRgb':palette.tolist(),'baseSha256':sha(base/'native/base-frame.png'),'previewCrop':box,'selectedCandidate':spec.get('selectedCandidate'),'runtimeIntegration':'not-performed'};dump(out/'manifest.json',info);shutil.copy2(SPEC,out/'motion-spec.json');page(out,info)
    (out/'README.md').write_text('''# 늘어진수의 · 기존 픽셀 부유 이동 후보

A는 기존8키·2.4초 부유를 무변형 복사한 기준이다. B는 기존 국소4키 픽셀을 사용해 전체0~-3px 부유와 아래 수의0~2px 오른쪽 후행을 결합한16키·1.6초다. C는 B와 픽셀·순서가 같은16키·1.2초다. 발 없는 디자인과 가면·머리 형태·기존8색·384×224 캔버스·몸 크기를 유지한다.

행별 정수 이동은 픽셀 내용을 보간하거나 늘이지 않고 같은 행 전체를 평행 이동한다. 소스별 불투명 픽셀 수와 머리/가면의 강체 이동을 검사한다. 기본본·국소4키·기존 부유·팔레트·해시·행별 변위를 보존한다. 후보별 native/indexed·흰 비교·정수4배·무손실 루프·키판·기존 맵 합성을 제공한다. 맵은 원본 배경과 플레이어를 수정하지 않은 합성 미리보기이며 실제 게임 캡처가 아니다. 이동속도·공격·Unity 통합과 다른4종 채택본은 변경하지 않는다. 새 부유 후보는 사용자 검토 단계다.
''',encoding='utf-8');package(out)


def package(out):
    with zipfile.ZipFile(out/'h3-float-travel-20261101.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip':archive.write(p,p.relative_to(out).as_posix())


def page(out,info):
    js='''const rows=DATA,box=BOX;let playing=true,elapsed=0,previous=null,scale=3;const states=[];
function draw(s,k){s.key=k;const c=document.querySelector('[data-id="'+s.id+'"]'),ctx=c.getContext('2d'),w=box[2]-box[0],h=box[3]-box[1];c.width=w;c.height=h;c.style.width=w*scale+'px';c.style.height=h*scale+'px';ctx.imageSmoothingEnabled=false;ctx.drawImage(s.images[k],box[0],box[1],w,h,0,0,w,h);document.querySelector('[data-label="'+s.id+'"]').textContent=(k+1)+' / '+s.frameCount;document.querySelector('[data-key="'+s.id+'"]').value=k}
Promise.all(rows.map(async s=>{s.images=await Promise.all(s.paths.map(src=>new Promise((resolve,reject)=>{let im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=src})));states.push(s);draw(s,0)})).then(()=>requestAnimationFrame(tick));
function tick(t){if(previous===null)previous=t;if(playing){elapsed+=t-previous;for(const s of states){let rem=elapsed%s.cycleMs,k=0;while(k<s.frameCount-1&&rem>=s.durationsMs[k]){rem-=s.durationsMs[k];k++}if(k!==s.key)draw(s,k)}}previous=t;requestAnimationFrame(tick)}document.querySelector('#play').onclick=()=>{playing=!playing;previous=performance.now();document.querySelector('#play').textContent=playing?'전체 정지':'전체 재생'};document.querySelector('#restart').onclick=()=>{elapsed=0;previous=performance.now();states.forEach(s=>draw(s,0))};document.querySelector('#scale').onchange=e=>{scale=+e.target.value;states.forEach(s=>draw(s,s.key))};document.querySelector('#bg').onchange=e=>document.querySelectorAll('.viewport').forEach(v=>v.style.background=e.target.value);document.querySelectorAll('[data-key]').forEach(input=>input.oninput=()=>{playing=false;document.querySelector('#play').textContent='전체 재생';let s=states.find(s=>s.id===input.dataset.key);draw(s,+input.value)});'''.replace('DATA',json.dumps(info['candidates'])).replace('BOX',json.dumps(info['previewCrop']))
    (out/'preview.js').write_text(js,encoding='utf-8');cards=[]
    for c in info['candidates']:
        ident=c['id'];cards.append(f'''<section><h2>{ident} · {escape(c['name'])}{' · 추천' if c['recommended'] else ''}</h2><p>{c['frameCount']}키 · {c['cycleMs']/1000:g}초</p><p>{escape(c['description'])}</p><div class="viewport"><canvas data-id="{ident}"></canvas></div><input data-key="{ident}" type="range" min="0" max="{c['frameCount']-1}" value="0"><span data-label="{ident}"></span><p><a href="{ident}/previews/white-4x.webp">흰 배경 루프</a> · <a href="{ident}/previews/keys.png">키판</a> · <a href="{ident}/manifest.json">시간·변위</a></p><details><summary>원래 맵 위 실제1배 합성</summary><img src="{ident}/previews/map-float.webp" width="960" height="540"></details></section>''')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>늘어진수의 · 픽셀 부유 이동</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1450px;margin:auto;padding:24px}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}section{padding:16px;background:white;min-width:0}.viewport{overflow:auto;background:white}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}button,select{padding:8px;margin:4px}a{color:#815149}.controls{position:sticky;top:0;background:#eeeeeef5;padding:8px}h2{font-size:21px}input{max-width:100%}@media(max-width:900px){.grid{grid-template-columns:1fr}}</style><main><h1>늘어진수의 · 기존 픽셀로 만든 부유 이동</h1><p>발 없는 수의를 새로 생성하지 않고 기존 픽셀의 정수 이동으로 만들었습니다. 몸은3px 범위로 부유하고, B/C의 아래 수의는 오른쪽으로 최대2px 뒤따릅니다. 가면과 머리 모양·기존8색·논리384×224·크기를 유지합니다. B가 기본 이동 추천이고 C는 같은 움직임의 빠른 재생입니다.</p><p><a download href="h3-float-travel-20261101.zip">부유 PNG·4배·참조·변위 ZIP</a> · <a href="manifest.json">공통 규격</a></p><div class="controls"><button id="play">전체 정지</button><button id="restart">같이 처음부터</button><select id="scale"><option value="1">실제1배</option><option value="3" selected>3배</option><option value="4">4배</option><option value="8">8배</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 배경</option><option value="#708b92">푸른 배경</option></select></div><div class="grid">'''+''.join(cards)+'''</div><p>새 색·보간·전체 크기 변화·발이나 공격 동작을 추가하지 않았습니다. 제자리 이동 루프의 검토 시안이며 실제 게임 이동속도·전투·Unity 연결은 별도입니다. 맵은 합성 미리보기이며 실제 게임 캡처가 아닙니다. 다른4종의 채택된 이동 프레임은 유지합니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')


if __name__=='__main__':build()
