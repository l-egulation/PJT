"""Retouch returned logical pixels: per-character colors, edge specks, fixed handedness."""
from pathlib import Path
import hashlib
from html import escape
import json
import shutil
import zipfile

import numpy as np
from PIL import Image
from scipy.ndimage import label, binary_dilation, binary_erosion

from walk_review import animate, review_board

ROOT=Path(__file__).resolve().parents[2]
SPEC=ROOT/'data/presentation/stage2_walk_retouch.json'
N=Image.Resampling.NEAREST


def dump(p,d):
    p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def luma(rgb):
    return np.asarray(rgb).astype(float) @ np.array([.2126,.7152,.0722])


def clean(frame,base,config,sampled=None):
    a=np.array(frame).copy();b=np.array(base);opaque=a[:,:,3]>0
    light=(luma(a[:,:,:3])>config['brightLumaThreshold'])&opaque
    protection=binary_dilation((luma(b[:,:,:3])>config['brightLumaThreshold'])&(b[:,:,3]>0),iterations=config['baseHighlightProtectionRadius'])
    boundary=opaque&~binary_erosion(opaque)
    if sampled is not None:
        # Reject source pixels explained by a white matte over a darker palette color.
        # Genuine near-palette highlights remain opaque, even on silhouette edges.
        palette=np.unique(b[b[:,:,3]>0,:3],axis=0).astype(float)
        y,x=np.where(boundary);q=sampled[y,x,:3].astype(float)
        opaque_error=np.sqrt(((q[:,None]-palette[None])**2).mean(2)).min(1)
        v=palette-255;weights=np.clip(((q[:,None]-255)*v[None]).sum(2)/(v*v).sum(1)[None],0,1)
        fit=255+weights[:,:,None]*v[None]
        residual=np.sqrt(((q[:,None]-fit)**2).mean(2))
        candidate=(weights<config['whiteMatteAlphaMax'])&(weights>.05)
        residual[~candidate]=10000
        bad=(opaque_error>config['whiteMatteOpaqueErrorMin'])&(residual.min(1)<config['whiteMatteResidualMax'])
        a[y[bad],x[bad]]=0
        opaque=a[:,:,3]>0
        light&=opaque
        boundary=opaque&~binary_erosion(opaque)
    clusters,n=label(light)
    for index in range(1,n+1):
        mask=clusters==index
        if mask.sum()<=config['smallBrightAreaMax'] and np.any(mask&boundary) and not np.any(mask&protection):
            a[mask]=0
    parts,n=label(a[:,:,3]>0)
    for index in range(1,n+1):
        mask=parts==index
        if mask.sum()<=config['isolatedAlphaAreaMax'] and not np.any(mask&protection):a[mask]=0
    return Image.fromarray(a)


def raw_samples(prior,ident,index,row):
    raw=np.array(Image.open(prior/ident/'raw-frames'/f'walk-{index:03}.png').convert('RGBA'))
    scale=row['scale'];ox,oy=row['offset']
    x=np.clip(np.floor((np.arange(384)+.5-ox)/scale).astype(int),0,raw.shape[1]-1)
    y=np.clip(np.floor((np.arange(224)+.5-oy)/scale).astype(int),0,raw.shape[0]-1)
    return raw[y[:,None],x[None,:]]


def materials(frame,base_id,spec):
    a=np.array(frame).copy()
    for zone in spec.get('materialZones',[]):
        if zone['baseId']!=base_id:continue
        x,y,x2,y2=zone['box'];part=a[y:y2,x:x2]
        mask=(part[:,:,3]>0)&(luma(part[:,:,:3])>zone['lumaAbove'])
        part[mask,:3]=tuple(bytes.fromhex(zone['color'][1:]))
    return Image.fromarray(a)


def shifts(frames):
    points=[]
    for frame in frames:
        a=np.array(frame);mask=(a[:,:,3]>0)&(np.indices(a.shape[:2])[0]<120)&(luma(a[:,:,:3])>160)
        y,x=np.where(mask);points.append([np.median(x),y.min()])
    points=np.array(points);center=np.median(points[1:],axis=0)
    delta=points-center
    # Cyclic five-key smoothing excludes the different initial reference image.
    active=delta[1:]
    smooth=sum(np.roll(active,i,axis=0)*w for i,w in [(-2,1),(-1,2),(0,3),(1,2),(2,1)])/9
    return [[0,0]]+np.rint(np.clip(smooth,-2,2)).astype(int).tolist()


def repair_thrower(frame,base,offset,config):
    src=np.array(frame);b=np.array(base);h,w=src.shape[:2];yy,xx=np.indices((h,w));dx,dy=offset
    # Keep walking legs behind a faithfully restored resting upper body.
    leg_region=(yy>=config['upperBodyCutY']+dy)&(xx>=config['legMinX']+dx)&(xx<config['legMaxX']+dx)
    allowed=np.array([tuple(bytes.fromhex(c[1:])) for c in config['legColors']])
    leg_color=np.any(np.all(src[:,:,:3,None]==allowed.T[None,None,:,:],axis=2),axis=2)
    result=np.zeros_like(src);mask=leg_region&leg_color&(src[:,:,3]>0);result[mask]=src[mask]
    assembly=(b[:,:,3]>0)&((yy<config['upperBodyCutY'])|((xx>=config['heldAssemblyMinX'])&(yy<config['heldAssemblyMaxY'])))
    y,x=np.where(assembly);result[y+dy,x+dx]=b[y,x]
    return Image.fromarray(result),assembly


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));prior=ROOT/spec['prior'];out=ROOT/spec['output'];base=ROOT/spec['base']
    rows=json.loads((prior/'manifest.json').read_text(encoding='utf-8'));out.mkdir(parents=True,exist_ok=True)
    for row in rows:
        ident=row['id'];bid=row.get('baseId',ident);p=out/ident
        for sub in ('native','enlarged-4x','previews/frames','references'):(p/sub).mkdir(parents=True,exist_ok=True)
        basic=Image.open(base/bid/'native/base-frame.png').convert('RGBA');shutil.copy2(base/bid/'native/base-frame.png',p/'references/base-frame.png');shutil.copy2(base/bid/'palette.json',p/'references/palette.json')
        originals=[Image.open(prior/ident/'native'/f'walk-{i:03}.png').convert('RGBA') for i in range(row['frameCount'])]
        offsets=shifts(originals) if ident==spec['thrower']['id'] else None
        frames=[];counts=[];source_hashes=[];assembly_checks=[]
        palette={tuple(c) for c in np.unique(np.array(basic).reshape(-1,4),axis=0)}
        for i,original in enumerate(originals):
            f=clean(original,basic,spec['cleanup'],raw_samples(prior,ident,i,row)) if ident!='H3' else original.copy()
            f=materials(f,bid,spec)
            if offsets is not None:
                f,assembly=repair_thrower(f,basic,offsets[i],spec['thrower'])
                a=np.array(f);b=np.array(basic);ys,xs=np.where(assembly);dx,dy=offsets[i]
                assert np.array_equal(a[ys+dy,xs+dx],b[ys,xs])
                assembly_checks.append(True)
            pixels=np.array(f);assert {tuple(c) for c in np.unique(pixels.reshape(-1,4),axis=0)}<=palette
            assert set(np.unique(pixels[:,:,3]))=={0,255}
            counts.append(int(np.any(pixels!=np.array(original),axis=2).sum()))
            f.save(p/'native'/f'walk-{i:03}.png');f.resize((1536,896),N).save(p/'enlarged-4x'/f'walk-{i:03}.png');frames.append(f)
            source_hashes.append(sha(prior/ident/'native'/f'walk-{i:03}.png'))
        chosen=row['reviewIndices'];active=[frames[i] for i in chosen];ds=[row['durations'][i] for i in chosen]
        union=Image.new('RGBA',(384,224))
        for f in active:union.alpha_composite(f)
        bb=union.getbbox();box=(max(0,bb[0]-8),max(0,bb[1]-8),min(384,bb[2]+8),min(224,bb[3]+8))
        animate(active,ds,p/'previews/walk-native.webp')
        enlarged=[f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N) for f in active]
        animate(enlarged,ds,p/'previews/walk-4x.webp')
        gifs=[]
        for f in enlarged:
            bg=Image.new('RGBA',f.size,'#26313b');bg.alpha_composite(f);gifs.append(bg.convert('RGB'))
        gifs[0].save(p/'previews/walk-4x.gif',save_all=True,append_images=gifs[1:],duration=ds,loop=0,disposal=2,optimize=False)
        for i,f in enumerate(frames):f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N).save(p/'previews/frames'/f'walk-{i:03}.png')
        keys=[chosen[round(i*(len(chosen)-1)/7)] for i in range(8)]
        review_board(frames,keys,box,4,2,p/'previews/key-frames.png');review_board(frames,list(range(len(frames))),box,8,2,p/'previews/all-frames.png')
        if offsets is not None:
            mask=Image.fromarray((assembly*255).astype('uint8'));mask.save(p/'references/restored-assembly-mask.png')
        row.update({'changedPixels':counts,'priorNativeHashes':source_hashes,'baseHash':sha(base/bid/'native/base-frame.png'),'bodyOffsets':offsets,'heldAssemblyExactChecks':len(assembly_checks),'approvedPaletteOnly':True,'retouchBox':list(box)})
        dump(p/'metadata.json',row);print(ident,'changed',sum(counts),'assembly checks',len(assembly_checks),flush=True)
    dump(out/'manifest.json',rows);shutil.copy2(SPEC,out/'retouch-spec.json')
    page(out,rows)
    (out/'README.md').write_text('''# 잡몹 걷기 픽셀 보정 v2

이전 결과는 모든 원본·시트 분리·GIF 복호화·정리본을 그대로 보존한다. 이번 native는384×224 개별 PNG이며 enlarged-4x는 정확한4배다. 개체마다 선택된 픽셀 기본본의 실제 RGBA 팔레트만 사용한다. 원본 픽셀의 흰 배경 혼합을 대조해 가장자리 오염을 제거하고 작고 밝은 외곽 군집과 떨어진 잡점을 정리하고 기본본의 정상적인 하이라이트 영역을 보호한다. 신호투척수는 기본본 상체·든 팔·손·소품을 강체로 복원하고 반환 모션의 다리 픽셀을 뒤에 배치했다. 상체는 머리 움직임에서 구한 최대2px 변위를 따라가며 소품의 손·상대 위치·형태는 고정한다. 다리 색 제한 때문에 무릎 경계는 추가 육안 검토 대상이다. H3는 기존8키 부유를 유지한다.

이전 PNG 원본 해시, 기본본 해시, 변경 픽셀 수, 상체 변위와 강체 동일성 검사를 metadata에 기록한다. index.html은 이전/보정 재생과 보정 프레임 선택을 제공한다. 비교 화면의 이전 모션 링크는 원래 출력 폴더를 사용하므로 ZIP 밖에서 열린다. ZIP에는 이번 프레임·확대·메타데이터를 제공하며 원본 추출은 이전 폴더에서 확인한다. 아트 검토용이며 전투 구현·Unity 적용은 아니다.
''',encoding='utf-8')
    package(out,rows)


def package(out,rows):
    ids={r['id'] for r in rows}
    allowed={'index.html','manifest.json','preview.js','README.md','retouch-spec.json'}
    with zipfile.ZipFile(out/'walk-retouch-v2-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for f in sorted(out.rglob('*')):
            if f.is_file() and (f.relative_to(out).parts[0] in ids or f.name in allowed):z.write(f,f.relative_to(out).as_posix())


def page(out,rows):
    cards=[]
    for row in rows:
        i=row['id'];cards.append(f'''<section><h2>{escape(row['name'])}</h2><p>팔레트: 선택된 개체 기본본만 사용 · {row['frameCount']}장</p><div class="pair"><div>이전<div class="scroll"><img src="../walk-review-20261031/{i}/previews/walk-4x.webp"></div></div><div>보정<div class="scroll"><img data-id="{i}" src="{i}/previews/walk-4x.webp"></div></div></div><button data-play="{i}">재생</button><input data-step="{i}" type="range" min="0" max="{row['frameCount']-1}" value="{row['reviewIndices'][0]}"><span data-label="{i}">프레임 선택</span><p><a href="{i}/previews/all-frames.png">전체 프레임판</a> · <a href="{i}/previews/key-frames.png">8키 확대</a> · <a href="{i}/native/walk-001.png">개별 PNG</a> · <a href="{i}/metadata.json">보정 정보</a></p><img class="board" src="{i}/previews/key-frames.png"></section>''')
    js='''document.querySelectorAll('[data-step]').forEach(s=>s.oninput=()=>{document.querySelector('[data-id="'+s.dataset.step+'"]').src=s.dataset.step+'/previews/frames/walk-'+String(s.value).padStart(3,'0')+'.png';document.querySelector('[data-label="'+s.dataset.step+'"]').textContent='프레임 '+s.value});document.querySelectorAll('[data-play]').forEach(b=>b.onclick=()=>document.querySelector('[data-id="'+b.dataset.play+'"]').src=b.dataset.play+'/previews/walk-4x.webp');'''
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>잡몹 걷기 픽셀 보정</title><style>body{margin:0;background:#17212a;color:#eee5d7;font:16px/1.7 system-ui}main{max-width:1300px;margin:auto;padding:24px}a{color:#e8c48d}section{padding:24px;background:#26313b;margin:24px 0;border-radius:12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}.pair>div{min-width:0}.scroll{overflow:auto}img{image-rendering:pixelated}.board{max-width:100%}button{padding:8px 20px;margin:12px}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style><main><h1>잡몹 걷기 · 소품과 외곽 픽셀 보정</h1><p>개별 PNG를 추출한 뒤 그 PNG로 미리보기를 재생합니다. 이번에는 개체별 기본본 팔레트만 사용하고 외곽의 작고 밝은 잡점을 정리했습니다. 투척수는 기본본 상체·든 팔·손·소품을 복원하고 반환된 다리 걸음을 이어 붙였습니다. 상체가 조금 움직여도 소품은 같은 손·위치에 붙어 있습니다.</p><p>아트 검토용입니다. 원본의 분사수 상단 잘림과 다른 형태 변화는 남아 있습니다. <a href="../walk-review-20261031/index.html">원본 추출 보기</a> · <a download href="walk-retouch-v2-20261031.zip">이번 보정 프레임 ZIP</a></p>'''+''.join(cards)+'<script>'+js+'</script></main></html>',encoding='utf-8')


if __name__=='__main__':build()
