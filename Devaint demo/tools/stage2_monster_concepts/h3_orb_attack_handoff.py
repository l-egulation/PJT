"""Prepare an isolated generated ink orb; preserve H3's approved float pixels."""
import json
import shutil
import zipfile
from html import escape
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import binary_fill_holes, label

from b3_walk_return import dump, white
from k4_d_pixel_walk import ROOT, N, sha

SPEC=ROOT/'data/presentation/stage2_h3_orb_attack_handoff.json'


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];floating=ROOT/s['approvedFloat']
    for folder in ('generated','orb/native','orb/native-indexed','orb/white-native','orb/enlarged-8x','references/body-float-B/native','previews'):
        (out/folder).mkdir(parents=True,exist_ok=True)
    raw=out/'generated/original.png';shutil.copy2(s['generatedSource'],raw)
    assert sha(raw)==sha(Path(s['generatedSource']))
    original=Image.open(raw).convert('RGBA');a=np.array(original)
    # Source alpha owns occupancy; palette uncertainty must never cut holes into ink.
    occupied=a[:,:,3]>=128;yy,xx=np.where(occupied)
    source_box=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
    height=s['orbVisibleHeight'];width=round((source_box[2]-source_box[0])*height/(source_box[3]-source_box[1]))
    small=original.crop(source_box).resize((width,height),Image.Resampling.BOX)
    q=np.array(small);palette=np.array(s['orbPaletteRgb'],np.uint8);opaque=q[:,:,3]>=128
    nearest=((q[:,:,:3,None].astype(int)-palette.T[None,None].astype(int))**2).sum(2).argmin(2)
    p=np.zeros_like(q);p[:,:,:3]=palette[nearest];p[:,:,3]=opaque.astype(np.uint8)*255;p[~opaque]=0
    # Keep a single solid ink mass, including the attached sag, with no transparent pinholes.
    parts,_=label(p[:,:,3]>0);sizes=np.bincount(parts.ravel());sizes[0]=0
    keep=parts==sizes.argmax();p[~keep]=0
    holes=binary_fill_holes(keep)&~keep
    # Use an existing plum material for any enclosed subpixel alpha defect.
    p[holes]=[128,88,110,255]
    orb=Image.new('RGBA',tuple(s['orbCanvas']));position=((orb.width-width)//2,(orb.height-height)//2)
    orb.alpha_composite(Image.fromarray(p),position)
    orb.save(out/'orb/native/ink-orb.png');white(orb).save(out/'orb/white-native/ink-orb.png')
    full=white(orb).resize((512,512),N);full.save(out/'orb/enlarged-8x/ink-orb-white-input.png')
    orb.resize((512,512),N).save(out/'orb/enlarged-8x/ink-orb-transparent.png')
    arr=np.array(orb);indices=np.zeros(arr.shape[:2],np.uint8)
    for c,rgb in enumerate(palette,1):indices[np.all(arr[:,:,:3]==rgb,axis=2)&(arr[:,:,3]>0)]=c
    ind=Image.fromarray(indices);colors=np.concatenate([np.zeros((1,3),np.uint8),palette]);ind.putpalette(colors.ravel().tolist()+[0]*(768-colors.size));ind.info['transparency']=0
    ind.save(out/'orb/native-indexed/ink-orb.png');assert ind.convert('RGBA').tobytes()==orb.tobytes()
    assert set(np.unique(arr[:,:,3]))=={0,255}
    assert set(map(tuple,np.unique(arr[arr[:,:,3]>0,:3],axis=0)))<=set(map(tuple,palette))
    base=Image.open(ROOT/s['bodyBase']).convert('RGBA');base_a=np.array(base)
    assert set(map(tuple,palette))<=set(map(tuple,np.unique(base_a[base_a[:,:,3]>0,:3],axis=0)))
    body_files=sorted((floating/'native').glob('*.png'));assert len(body_files)==16
    hashes=[]
    for path in body_files:
        dest=out/'references/body-float-B/native'/path.name;shutil.copy2(path,dest)
        assert sha(path)==sha(dest);hashes.append({'path':str(path.relative_to(ROOT)),'sha256':sha(path)})
    shutil.copy2(floating/'previews/white-4x.webp',out/'previews/approved-body-float.webp')
    body=Image.open(body_files[0]).convert('RGBA');assert body.size==tuple(s['bodyCanvas'])
    center=s['orbCenterInBodyCanvas'];anchor=(center[0]-orb.width//2,center[1]-orb.height//2)
    composition=body.copy();composition.alpha_composite(orb,anchor)
    white(composition).resize((1536,896),N).save(out/'previews/body-plus-orb-location-4x.png')
    white(body).resize((1536,896),N).save(out/'previews/body-only-4x.png')
    bb=composition.getbbox();crop=[bb[0]-10,bb[1]-10,bb[2]+10,bb[3]+10]
    white(composition.crop(crop)).resize(((crop[2]-crop[0])*4,(crop[3]-crop[1])*4),N).save(out/'previews/body-plus-orb-cropped-4x.png')
    scene=Image.open(ROOT/s['mapReference']).convert('RGBA');assert scene.size==(960,540)
    scene.alpha_composite(composition,tuple(s['mapOffset']));scene.save(out/'previews/map-static-composite.png')
    combat=json.loads((ROOT/s['combatReference']).read_text(encoding='utf-8'))
    pattern=next(m for m in combat['monsters'] if m['id']=='H3')
    assert [p['seconds'] for p in pattern['patterns'][0]['phases']]==[p['seconds'] for p in s['phaseProposal']]
    assert round(sum(p['seconds'] for p in s['phaseProposal'])*1000)==1950
    for source,name in [(ROOT/s['bodyBase'],'body-base.png'),(SPEC,'presentation-spec.json'),
                        (ROOT/s['combatReference'],'combat-patterns.json')]:shutil.copy2(source,out/'references'/name)
    dump(out/'orb/palette.json',{'colors':s['orbPaletteRgb'],'actualColorsUsed':len(np.unique(arr[arr[:,:,3]>0,:3],axis=0)),'transparentIndex':0})
    (out/'prompt-orb-en.txt').write_text(s['prompt']+'\n',encoding='utf-8')
    info={**s,'generationTool':'built-in image_gen','rawSha256':sha(raw),'rawSize':list(original.size),
          'generationPromptSha256':sha(ROOT/s['generationPrompt']),'rawOpaqueBounds':source_box,
          'orbVisibleBounds':list(orb.getbbox()),'orbVisibleSize':[width,height],
          'orbNativeSha256':sha(out/'orb/native/ink-orb.png'),'orbInputSize':[512,512],
          'bodyFloatHashes':hashes,'bodyFloatFramesReused':16,'bodyPixelsChanged':0,
          'orbPositionInBodyCanvas':list(anchor),'formationAnchorProposal':center,'attackMotionFramesGenerated':0,
          'orbStaticDesignGenerated':True,'combatReferenceSha256':sha(ROOT/s['combatReference']),
          'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info);page(out,info)
    (out/'README.md').write_text('''# 늘어진 수의 · 본체 부유 유지 / 잉크 구 별도 입력

사용자 지정은 손발을 추가하지 않고 본체 앞에서 별도 잉크 구를 생성·발사하는 방식이다. 본체는 승인 B 부유16장의 파일을 그대로 복사하며 새 공격 자세를 만들지 않는다. 본체+구는 정지 위치/크기 합성 참고이며 구 동작 생성기 입력은 구 단독512×512 흰 배경을 사용한다.

기본 image_gen 도구로 생성한 구 원본을 generated/original.png에 무변형 복사하고 생성 프롬프트/도구/원본경로/해시를 보존한다. 알파 점유를 기준으로40px 높이에 축소하고 H3 실제8색 중4색으로 환원한다. 팔레트 오차로 알파를 제거하지 않으며 작은 고립점/내부 구멍을 정리한다.64×64 native/indexed/흰배경·정수8배 입력과 투명 확대를 제공한다. 생성 원본은 색/알파가 다양한 디자인 참고이고 적용 후보는 이진 알파/정확한4색 픽셀이다. 구 디자인/크기/생성 모션은 사용자 승인 전이다.

몸 앞 구 중심[134,99]는384×224에서의 제안이며 본체/맵 크기를 변경하지 않는다. 공 생성850ms·분리/발사200ms·회복900ms=1950ms는 기존 제작 검토값이다. 구 단독 프롬프트는 빈 화면에서 고정 중심[32,32]에 공 생성, 국소8px 왼쪽 발사 시작, 빈 회복을 요청한다. 실제 이후 비행은 별도 투사체이며 바닥 장판/착탄은 후속이다. 실제 모션 프레임은 생성하지 않았다.

기획 원본 H3의 손/팔 동작 표현을 본체 부유와 몸 앞 공 생성·분리로 바꾸고 생성 문서/검토 HTML을 재생성한다. 다른 몬스터/보스·숫자·장판 규칙은 유지한다. 기존 맵 위 그림은 합성 미리보기이며 실제 게임 캡처가 아니다. 전투 피해/탄도/Unity 연결은 하지 않는다.
''',encoding='utf-8')
    dump(out/'validation.json',{'result':'pass','orbCanvas':[64,64],'orbInput':[512,512],
         'actualOrbColors':len(np.unique(arr[arr[:,:,3]>0,:3],axis=0)),'binaryAlpha':True,
         'indexedRgbaExact':True,'inputNearest8xExact':Image.open(out/'orb/enlarged-8x/ink-orb-white-input.png').tobytes()==full.tobytes(),
         'body16FramesByteIdentical':True,'bodyPixelsChanged':0,'phaseMs':[850,200,900],
         'orbMotionGenerated':False,'browserQA':'not-run','Unity':'not-run'})
    with zipfile.ZipFile(out/'H3-separate-orb-input.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and p.name!='commit-message.txt':z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'H3-separate-orb-input.zip') as z:assert z.testzip() is None
    print(json.dumps({'orbSize':[width,height],'orbCanvas':[64,64],'bodyFramesUnchanged':16,'motionGenerated':False}))


def page(out,info):
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>늘어진 수의 · 잉크 구 분리 입력</title><style>body{margin:0;background:#f2f0ed;color:#28252c;font:16px/1.7 system-ui}main{max-width:1100px;padding:28px;margin:auto}section{background:white;padding:20px;margin:20px 0}h1{font-size:28px}h2{font-size:22px}img{max-width:100%;height:auto;image-rendering:pixelated}textarea{box-sizing:border-box;width:100%;height:420px;padding:16px;font:14px/1.6 monospace}a{color:#814d69}.pair{display:flex;gap:20px;flex-wrap:wrap}.orb{max-width:320px}</style><main><h1>늘어진 수의 · 본체는 부유 / 구만 생성·발사</h1><p>손발을 추가하지 않고 몸 앞쪽 공중에서 잉크 구를 따로 만들고 내보냅니다. 본체는 승인된 B 부유16장을 그대로 사용합니다.</p><p><a download href="H3-separate-orb-input.zip">이미지·프롬프트·원본·참조 ZIP</a> · <a href="manifest.json">규격·원본·해시</a></p><section><h2>몸 앞 위치 참고 · 동작 생성기 입력 아님</h2><img src="previews/body-plus-orb-cropped-4x.png" alt="변경하지 않은 본체와 독립 구체의 위치 참고"><p>구는 최대40px 높이 제안이고 본체 논리384×224에서 중심[134,99]입니다. 이 그림은 정지 합성 참고이며 완성 공격 모션이 아닙니다. 생성기에는 아래 구 단독 입력을 넣으세요.</p></section><section><h2>생성기에 넣을 구 단독 입력 · 512×512</h2><img class="orb" src="orb/enlarged-8x/ink-orb-white-input.png" alt="잉크 구 단독 순백8배 입력"><p><a download href="orb/enlarged-8x/ink-orb-white-input.png">구 단독 흰 배경 PNG</a> · <a download href="prompt-orb-en.txt">구 동작 영문 프롬프트</a> · <a download href="orb/native/ink-orb.png">64×64 투명 원배율</a></p><p>원배율64×64·정수8배·기존 H3 팔레트의4색입니다. 입력 그림은 최대 완성형 참고이며 실제 생성 모션은 빈 화면에서 시작하도록 요청합니다. 손·팔·발·본체를 새로 그리지 않습니다.</p><ol><li>0.85초: 한 위치에서 작은 잉크 덩어리들이 최대 공으로 응축.</li><li>0.2초: 살짝 압축되며 왼쪽으로8px 움직이는 발사 시작만 표현.</li><li>0.9초: 구 생성 위치를 비워 두고 다음 공은 만들지 않기.</li></ol><p>이후 완만한 비행은 별도 투사체로 처리합니다. 착탄·지속 피해 장판은 후속입니다. 실제 구 모션 프레임은 아직 생성하지 않았습니다.</p><textarea readonly onclick="this.select()">'''+escape(info['prompt'])+'''</textarea></section><section><h2>그대로 재사용하는 본체 B 부유</h2><img src="previews/approved-body-float.webp" alt="기존 승인된 부유 루프"><p>16장·픽셀·크기·타이밍을 변경하지 않았습니다. 기존 수의의 주름을 손발로 확장하지 않습니다.</p></section><section><h2>기존 맵 정지 합성 · 960×540</h2><img src="previews/map-static-composite.png" alt="기존 맵 위 본체와 정지 구 합성"><p>합성 미리보기이며 실제 게임 캡처가 아닙니다. 배경/플레이어는 원본 그대로이고 구 디자인·위치·크기는 검토 제안입니다.</p></section><p>구 원본은 기본 image_gen 도구로 만들고 생성 원본·프롬프트를 보존했습니다. 구 픽셀화 후보만4색/이진 알파로 환원했습니다. 디자인과 모션은 아직 채택 전이며 전투·탄도·Unity 적용은 하지 않았습니다.</p></main></html>''',encoding='utf-8')


if __name__=='__main__':build()
