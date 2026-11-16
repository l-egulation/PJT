"""Reuse the square input while correcting the animation to anchored foreleg motion."""
import json
import shutil
import zipfile
from html import escape

from PIL import Image

from k4_d_pixel_walk import ROOT, sha
from b3_walk_return import dump

SPEC=ROOT/'data/presentation/stage2_b3_foreleg_slam_handoff.json'


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];refs=out/'references';refs.mkdir(parents=True,exist_ok=True)
    image=out/'B3-foreleg-slam-white-input-4x.png';source=ROOT/spec['sourceInput']
    shutil.copy2(source,image);assert sha(source)==sha(image)
    assert Image.open(image).size==tuple(spec['inputSize'])
    for src,name in [(ROOT/spec['sourceManifest'],'input-canvas-manifest.json'),(ROOT/spec['previousPrompt'],'previous-prompt.txt'),(ROOT/spec['base'],'approved-base-frame.png'),(ROOT/spec['palette'],'approved-palette.json'),(SPEC,'presentation-spec.json')]:
        shutil.copy2(src,refs/name);assert sha(src)==sha(refs/name)
    canvas=json.loads((ROOT/spec['sourceManifest']).read_text(encoding='utf-8'))
    assert canvas['paletteColors']==10 and canvas['visibleSizeLogical']==spec['bodySizeLogical'] and canvas['groundLinePx']==1408
    assert sum(spec['phaseDurationsMsProposal'])==1500
    (out/'prompt-en.txt').write_text(spec['prompt']+'\n',encoding='utf-8')
    (out/'attack-intro-ko.txt').write_text(spec['oneLine']+'\n\n'+'\n'.join('- '+r for r in spec['motionRules'])+'\n',encoding='utf-8')
    info={**spec,'inputSha256':sha(image),'existingInputByteIdentical':True,'paletteColors':10,'attackFramesGenerated':0,'runtimeIntegration':'not-performed'};dump(out/'manifest.json',info)
    rules=''.join('<li>'+escape(r)+'</li>' for r in spec['motionRules'])
    (out/'index.html').write_text(f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>쐐기보행기 · 제자리 앞발 들기와 찍기</title><style>body{{margin:0;background:#f2f0ec;color:#252935;font:16px/1.7 system-ui}}main{{max-width:1000px;margin:auto;padding:28px}}a{{color:#34548d}}.links{{display:flex;flex-wrap:wrap;gap:18px;margin:20px 0}}img{{width:100%;height:auto;image-rendering:pixelated}}textarea{{box-sizing:border-box;width:100%;height:420px;padding:16px;font:14px/1.6 monospace}}p{{color:#596171}}</style><main><h1>제자리 앞발 들기 → 잠깐 버팀 → 쾅 찍기</h1><p>{escape(spec['oneLine'])}</p><ol>{rules}</ol><p>이번에는 뒷발 접점이 모든 프레임에서 같은 좌표를 지키는 것을 최우선으로 요청합니다. 기존 위 여백 확장 이미지는 그대로 사용합니다. 앞발 들기·찍기·회복만 요청하는 제작용1.5초 제안이며 전체 돌진 후 찍기 공격 기획은 유지합니다.</p><div class="links"><a href="{image.name}" download>같은 정사각형 기본 이미지</a><a href="prompt-en.txt" download>수정 프롬프트TXT</a><a href="B3-foreleg-slam-input.zip" download>이미지+프롬프트ZIP</a><a href="manifest.json">입력·동작 규칙</a></div><h2>복사할 새 프롬프트</h2><textarea readonly onclick="this.select()">{escape(spec['prompt'])}</textarea><h2>생성에 사용할 전체 기본 이미지</h2><img src="{image.name}" alt="위 여백이 넓은 기존 쐐기보행기 기본 이미지"><p>이미지는 이전 정사각형 입력과 바이트까지 동일합니다. 새 모션을 보정한 결과가 아니라 수정한 생성 요청이며 반환 프레임은 아직 없습니다. 기존 원본·팔레트·이전 프롬프트를 보존하고 전투·Unity는 변경하지 않습니다.</p></main></html>''',encoding='utf-8')
    with zipfile.ZipFile(out/'B3-foreleg-slam-input.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in [image,out/'prompt-en.txt',out/'attack-intro-ko.txt',out/'manifest.json',out/'index.html',*sorted(refs.glob('*'))]:z.write(p,p.relative_to(out).as_posix())
    print(json.dumps({'inputUnchanged':True,'size':list(Image.open(image).size),'paletteColors':10,'phaseMsProposal':spec['phaseDurationsMsProposal'],'motionFramesGenerated':0}))


if __name__=='__main__':build()
