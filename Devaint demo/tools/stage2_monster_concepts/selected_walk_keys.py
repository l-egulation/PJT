"""Curate short actual frame loops; avoid presenting full clips as game-ready keys."""
from pathlib import Path
from html import escape
import hashlib,json,shutil,zipfile

import numpy as np
from PIL import Image
from scipy.ndimage import binary_dilation,convolve,label
from walk_review import animate,review_board

ROOT=Path(__file__).resolve().parents[2]
SPEC=ROOT/'data/presentation/stage2_selected_walk_keys.json'
N=Image.Resampling.NEAREST


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def tidy(im,base):
    a=np.array(im).copy();b=np.array(base)
    kernel=np.ones((3,3),int)
    original_neighbors=convolve((b[:,:,3]>0).astype(int),kernel,mode='constant')
    protect=binary_dilation((b[:,:,3]>0)&(original_neighbors<=4),iterations=3)
    # Cull unsupported one-pixel protrusions; preserve original thin needles/nozzles.
    for _ in range(2):
        solid=a[:,:,3]>0;neighbors=convolve(solid.astype(int),kernel,mode='constant')-solid
        a[solid&(neighbors<=1)&~protect]=0
    # Close only a single enclosed pinhole, leaving gaps between legs open.
    solid=a[:,:,3]>0;neighbors=convolve(solid.astype(int),kernel,mode='constant')
    holes=(~solid)&(neighbors==8)
    for y,x in zip(*np.where(holes)):
        patch=a[y-1:y+2,x-1:x+2];colors,counts=np.unique(patch[patch[:,:,3]>0],axis=0,return_counts=True)
        a[y,x]=colors[counts.argmax()]
    # Simplify isolated interior color noise, without editing silhouettes or eye gold.
    solid=a[:,:,3]>0;interior=convolve(solid.astype(int),kernel,mode='constant')==9
    source=a.copy()
    for color in np.unique(source[source[:,:,3]>0],axis=0):
        if tuple(color[:3])==(234,170,22):continue
        parts,n=label(np.all(source==color,axis=2))
        for index in range(1,n+1):
            mask=parts==index
            if mask.sum()>2 or not np.all(interior[mask]):continue
            ring=binary_dilation(mask)&solid&~mask
            colors,counts=np.unique(source[ring],axis=0,return_counts=True)
            if len(colors) and counts.max()>=5:a[mask]=colors[counts.argmax()]
    return Image.fromarray(a)


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];prior=ROOT/spec['prior'];base=ROOT/spec['base'];out.mkdir(parents=True,exist_ok=True);rows=[]
    for clip in spec['clips']:
        p=out/clip['id']
        for sub in ('native','enlarged-4x','previews','references'):(p/sub).mkdir(parents=True,exist_ok=True)
        basic=Image.open(base/clip['id']/'native/base-frame.png').convert('RGBA');shutil.copy2(base/clip['id']/'native/base-frame.png',p/'references/base-frame.png')
        palette=set(np.unique(np.array(basic).view(np.uint32)).tolist());frames=[];hashes=[];changes=[]
        for key,index in enumerate(clip['frames']):
            src=prior/clip['sourceId']/'native'/f'walk-{index:03}.png';im=Image.open(src).convert('RGBA');f=tidy(im,basic) if clip['id']!='H3' else im.copy()
            assert set(np.unique(np.array(f).view(np.uint32)).tolist())<=palette
            f.save(p/'native'/f'walk-{key:02}.png');f.resize((1536,896),N).save(p/'enlarged-4x'/f'walk-{key:02}.png');frames.append(f);hashes.append(sha(src));changes.append(int(np.any(np.array(f)!=np.array(im),axis=2).sum()))
        union=Image.new('RGBA',(384,224))
        for f in frames:union.alpha_composite(f)
        bb=union.getbbox();box=(max(0,bb[0]-12),max(0,bb[1]-12),min(384,bb[2]+12),min(224,bb[3]+12))
        ds=clip['durations'];animate(frames,ds,p/'previews/selected-native.webp');large=[f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N) for f in frames];animate(large,ds,p/'previews/selected-4x.webp');gifs=[]
        for f in large:
            bg=Image.new('RGBA',f.size,'#26313b');bg.alpha_composite(f);gifs.append(bg.convert('RGB'))
        gifs[0].save(p/'previews/selected-4x.gif',save_all=True,append_images=gifs[1:],duration=ds,loop=0,disposal=2,optimize=False)
        review_board(frames,list(range(len(frames))),box,4,3,p/'previews/selected-keys.png')
        # No new frame synthesis or world movement; speed is an art review proposal.
        row={**clip,'selectedCount':len(frames),'sourceHashes':hashes,'baseHash':sha(base/clip['id']/'native/base-frame.png'),'tidiedPixels':changes,'cycleMs':sum(ds),'reviewBox':list(box)};rows.append(row);(p/'selection.json').write_text(json.dumps(row,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (out/'manifest.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');shutil.copy2(SPEC,out/'selection-spec.json');page(out,rows)
    (out/'README.md').write_text('''# 선정한 걷기 키만 검토

분사수·보행기·필경사 각8장, 수의6장 총30장이다. 원본 프레임 번호는 selection.json에 보존한다. 선정 키만 다시 이어 재생하며 원본 긴 GIF를 재생하지 않는다. 고정384×224 PNG와 정확한4배, 키 보드·투명 WebP·GIF를 제공한다. 선택된 키에서 약한 돌출 픽셀·완전히 둘러싸인 작은 구멍·고립된 내부 색 잡음을 제한적으로 정리했다. 얇은 기본 침·노즐을 보호하며 팔레트는 각 기본본의 실제 색만 사용한다.

프레임과 시간은 검토 제안이며 사용자 승인·게임 적용과 구분한다. 투척수는 이전 합성에서 하체 접합이 훼손되어 제외하고 걷기 재제작을 권장한다. 나머지 생성된 형태 변화·분사수 상단 잘림도 남아 있으며 단순 색 변환만으로 완전한 픽셀 양산 아트가 되지는 않는다. 원래 전체 모션과 이전 보정본은 보존한다.
''',encoding='utf-8')
    allowed={r['id'] for r in rows}|{'index.html','manifest.json','selection-spec.json','README.md'}
    with zipfile.ZipFile(out/'selected-walk-keys-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.relative_to(out).parts[0] in allowed:z.write(p,p.relative_to(out).as_posix())
    print([(r['id'],r['selectedCount'],r['frames'],r['tidiedPixels']) for r in rows])


def page(out,rows):
    cards=[]
    for r in rows:
        i=r['id'];cards.append(f'''<section><h2>{escape(r['name'])} · 선정 {r['selectedCount']}장</h2><p>{escape(r['reason'])}</p><p>원본 번호: {', '.join(map(str,r['frames']))} · 제안 주기 {r['cycleMs']/1000:g}초</p><div class="scroll"><img src="{i}/previews/selected-4x.webp"></div><img class="board" src="{i}/previews/selected-keys.png"><p><a href="{i}/native/walk-00.png">선정 첫 PNG</a> · <a href="{i}/previews/selected-keys.png">선정 프레임판</a> · <a href="{i}/selection.json">선정 정보</a></p><p class="note">{escape(r['limitation'])}</p></section>''')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>선정된 걷기 키만 보기</title><style>body{margin:0;background:#17212a;color:#eee5d7;font:16px/1.7 system-ui}main{max-width:1100px;margin:auto;padding:24px}section{background:#26313b;padding:24px;margin:24px 0;border-radius:12px}a{color:#e8c48d}img{image-rendering:pixelated}.scroll{overflow:auto}.board{max-width:100%;margin-top:18px}.note{color:#c5c4bb}</style><main><h1>선정한 걷기 프레임만</h1><p>분사수·보행기·필경사8장씩, 수의 부유6장. 아래 루프는 선정한 키만 이어 붙였습니다. 숫자는 선정 순서0부터이며 원본 번호는 별도 표기합니다. 이전 전체 영상은 이 페이지에서 재생하지 않습니다.</p><p>투척수는 이전 합성의 하체 접합이 잘려 보여 이번 선정에서 보류합니다. 선택 디자인을 유지한 새 걷기를 권장합니다. 나머지도 아트 검토용이며 최종 승인 전입니다.</p><p><a download href="selected-walk-keys-20261031.zip">선정30장 PNG·4배·루프 ZIP</a></p>'''+''.join(cards)+'</main></html>',encoding='utf-8')


if __name__=='__main__':build()
