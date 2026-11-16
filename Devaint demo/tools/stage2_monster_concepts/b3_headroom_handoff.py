"""Add upper white padding without changing any existing character pixels."""
import json
import shutil
import zipfile
from html import escape

import numpy as np
from PIL import Image

from k4_d_pixel_walk import ROOT, N, sha
from b3_walk_return import dump

SPEC=ROOT/'data/presentation/stage2_b3_headroom_handoff.json'


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];refs=out/'references';refs.mkdir(parents=True,exist_ok=True)
    source=ROOT/spec['sourceInput'];old=Image.open(source).convert('RGB');assert old.size==tuple(spec['sourceSize'])
    for src,name in [(source,'previous-full-input.png'),(ROOT/spec['base'],'approved-base-frame.png'),(ROOT/spec['palette'],'approved-palette.json'),(ROOT/spec['sourcePrompt'],'previous-prompt.txt'),(SPEC,'presentation-spec.json')]:
        shutil.copy2(src,refs/name);assert sha(src)==sha(refs/name)
    base=Image.open(ROOT/spec['base']).convert('RGBA');assert base.size==tuple(spec['sourceLogicalCanvas'])
    logical=Image.new('RGBA',tuple(spec['workingLogicalCanvas']),(0,0,0,0));logical.alpha_composite(base,tuple(spec['logicalPasteOffset']))
    white=Image.new('RGBA',logical.size,'white');white.alpha_composite(logical);white=white.convert('RGB')
    full=Image.new('RGB',tuple(spec['outputSize']),'white');full.paste(old,tuple(spec['pasteOffset']))
    assert full.tobytes()==white.resize(tuple(spec['outputSize']),N).tobytes()
    x,y=spec['pasteOffset'];assert full.crop((x,y,x+old.width,y+old.height)).tobytes()==old.tobytes()
    assert np.all(np.asarray(full)[:y]==255)
    before=np.asarray(base);after=np.asarray(logical)
    assert int((before[:,:,3]>0).sum())==int((after[:,:,3]>0).sum())
    assert np.array_equal(before[before[:,:,3]>0],after[after[:,:,3]>0])
    colors=set(map(tuple,np.unique(after[after[:,:,3]>0,:3],axis=0)));assert len(colors)==10
    full.save(out/'B3-white-headroom-input-4x.png');white.save(out/'B3-white-headroom-logical.png');logical.save(out/'B3-headroom-logical-reference.png')
    prompt=(ROOT/spec['sourcePrompt']).read_text(encoding='utf-8')
    old_rule='Keep the original1536x896 canvas, exactly4x the384x224 logical grid.'
    assert old_rule in prompt
    prompt=prompt.replace(old_rule,'Keep the ENTIRE1536x1536 square canvas, exactly4x the384x384 working logical grid. Only the upper margin was expanded; the character size is unchanged.')
    prompt=prompt.replace('Use the existing empty headroom for the big lift.','Use the much larger empty area ABOVE the low-placed character for the full rear-up. Do not crop away the top white space or turn this back into a wide canvas.')
    prompt='INPUT FRAMING: use the new square1536x1536 image in full. The unchanged neutral creature is720x400 image pixels (180x100 logical pixels), with its ground line at y1408 (logical y352). There are1008 image pixels of empty white headroom above its resting silhouette. Maintain this low rear-foot ground anchor. Do not recenter, zoom in, shrink the creature or auto-fit its raised pose. The large upper white region is deliberate motion space.\n\n'+prompt
    assert '1536x896' not in prompt and '384x224' not in prompt
    (out/'prompt-en.txt').write_text(prompt,encoding='utf-8')
    bb=logical.getbbox();oldbb=base.getbbox();info={**spec,'sourceSha256':sha(source),'baseSha256':sha(ROOT/spec['base']),'visibleBoundsLogical':list(bb),'visibleSizeLogical':[bb[2]-bb[0],bb[3]-bb[1]],'bodySizeExport':[720,400],'headroomBeforePx':oldbb[1]*4,'headroomAfterPx':bb[1]*4,'groundLinePx':bb[3]*4,'bottomMarginPx':full.height-bb[3]*4,'paletteColors':10,'existingPixelsUnchanged':True,'oldCanvasRegionByteIdentical':True,'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info)
    (out/'index.html').write_text(f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>쐐기보행기 · 위 여백 확장 입력</title><style>body{{margin:0;background:#f2f0ec;color:#252935;font:16px/1.7 system-ui}}main{{max-width:1000px;margin:auto;padding:28px}}img{{display:block;width:100%;height:auto;image-rendering:pixelated;background:white}}.links{{display:flex;flex-wrap:wrap;gap:20px;margin:20px 0}}a{{color:#34548d}}textarea{{box-sizing:border-box;width:100%;height:360px;padding:14px;font:14px/1.6 monospace}}p{{color:#596171}}</style><main><h1>위쪽 공간을 넓힌 새 입력 이미지</h1><p>1536×896 → <strong>1536×1536</strong>. 위에만640px 흰 여백을 추가했습니다.<br>몸 크기720×400px·기존10색·가로 위치·아래 여백128px는 그대로입니다. 몸 위 공간은368px에서1008px로 늘었습니다.</p><div class="links"><a href="B3-white-headroom-input-4x.png" download>이 전체 이미지로 생성하기</a><a href="prompt-en.txt" download>새 캔버스에 맞춘 프롬프트</a><a href="B3-headroom-input.zip" download>이미지+프롬프트ZIP</a><a href="manifest.json">크기·이동량 정보</a></div><p>아래처럼 위쪽 흰 공간까지 포함해서 넣어주세요. 캐릭터 주변만 잘라 넣으면 추가 여백이 사라집니다. 큰 들기→버팀→찍기→회복 동작은 유지합니다.</p><img src="B3-white-headroom-input-4x.png" alt="위쪽 공간을 크게 확장한 쐐기보행기 흰 배경 전체 입력"><h2>복사할 프롬프트</h2><textarea readonly onclick="this.select()">{escape(prompt)}</textarea><p>새 그림을 생성하지 않고 기존 픽셀을 그대로 옮긴 입력입니다. 이 정사각형은 모션 제작용 작업 캔버스이며 게임에서 정한 몬스터 크기는 그대로입니다. 이전 입력·원본·팔레트·프롬프트는 references에 보존합니다.</p></main></html>''',encoding='utf-8')
    with zipfile.ZipFile(out/'B3-headroom-input.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in [out/'B3-white-headroom-input-4x.png',out/'B3-white-headroom-logical.png',out/'B3-headroom-logical-reference.png',out/'prompt-en.txt',out/'manifest.json',out/'index.html',*sorted(refs.glob('*'))]:z.write(p,p.relative_to(out).as_posix())
    print(json.dumps({k:info[k] for k in ('outputSize','bodySizeExport','headroomBeforePx','headroomAfterPx','groundLinePx','bottomMarginPx','paletteColors','existingPixelsUnchanged','oldCanvasRegionByteIdentical')}))


if __name__=='__main__':build()
