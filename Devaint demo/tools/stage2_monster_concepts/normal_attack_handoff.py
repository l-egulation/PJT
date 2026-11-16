"""Prepare the unchanged selected base and a reviewable single-monster attack prompt."""
import argparse
import hashlib
import json
import shutil
import zipfile
from html import escape
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SPEC = ROOT/'data/presentation/stage2_normal_attack_handoff.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(spec_path=SPEC):
    spec = json.loads(spec_path.read_text(encoding='utf-8'))
    out = ROOT/spec['output']
    refs = out/'references'
    refs.mkdir(parents=True, exist_ok=True)
    source = ROOT/spec['source']
    palette_path = ROOT/spec['palette']
    combat_path = ROOT/spec['combatReference']
    combat = json.loads(combat_path.read_text(encoding='utf-8'))
    pattern = next(row for row in combat['monsters'] if row['id'] == spec['activeId'])
    combat_phases = pattern['patterns'][0]['phases']
    selected_phases = [combat_phases[i] for i in spec.get('combatPhaseIndices',range(len(combat_phases)))]
    assert [p['seconds'] for p in selected_phases] == [p['seconds'] for p in spec['phaseProposal']]
    for src, name in [(source,'base-frame.png'),(palette_path,'palette.json'),(spec_path,'presentation-spec.json')]:
        shutil.copy2(src,refs/name)
        assert sha(src) == sha(refs/name)
    (refs/'combat-pattern-reference.json').write_text(json.dumps({'source':spec['combatReference'],'sourceSha256':sha(combat_path),'monster':pattern},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    im = Image.open(source).convert('RGBA')
    assert im.size == tuple(spec['canvas'])
    a = np.array(im)
    assert set(np.unique(a[:,:,3])) == {0,255}
    colors = set(map(tuple,np.unique(a[a[:,:,3]>0,:3],axis=0).tolist()))
    palette_info = json.loads(palette_path.read_text(encoding='utf-8'))
    declared = palette_info['colors']
    assert colors.issubset({tuple(bytes.fromhex(c.lstrip('#'))) for c in declared})
    assert len(colors) == palette_info.get('opaqueColorsUsed',len(colors))
    white = Image.new('RGBA',im.size,spec['background'])
    white.alpha_composite(im)
    white = white.convert('RGB')
    b = np.array(white)
    assert np.array_equal(b[a[:,:,3]>0],a[a[:,:,3]>0,:3])
    assert np.all(b[a[:,:,3]==0] == 255)
    ident = spec['activeId']
    native = out/f'{ident}-attack-white-native.png'
    full = out/f'{ident}-attack-white-input-4x.png'
    preview = out/f'{ident}-attack-white-preview-4x.png'
    white.save(native)
    scale = spec['scale']
    large = white.resize((white.width*scale,white.height*scale),Image.Resampling.NEAREST)
    large.save(full)
    assert Image.open(full).tobytes() == large.tobytes()
    bb = im.getbbox()
    box = (bb[0]-12,bb[1]-12,bb[2]+12,bb[3]+12)
    cropped = white.crop(box)
    cropped.resize((cropped.width*scale,cropped.height*scale),Image.Resampling.NEAREST).save(preview)
    (out/'prompt-en.txt').write_text(spec['prompt']+'\n',encoding='utf-8')
    phases = '\n'.join(f'- {p["name"]} 약{p["seconds"]:g}초: {p["action"]}' for p in spec['phaseProposal'])
    effect_note = spec.get('effectNoteKo','본체·노즐 동작을 먼저 제작하고 잉크 줄기 이펙트는 별도로 준비합니다.')
    motion_note = spec.get('motionNoteKo','가까이 접근한 뒤 멈춘 상태에서 시작합니다. 발을 버티고 노즐을 왼쪽 허리 높이로 든 뒤, 작은 분사 반동을 보이고 다시 내립니다.')
    total_seconds = sum(p['seconds'] for p in spec['phaseProposal'])
    order = next(i+1 for i,q in enumerate(spec['queue']) if q['id']==ident)
    archive_name = f'{ident}-attack-input.zip'
    (out/'attack-intro-ko.txt').write_text(f'{spec["name"]}: {spec["oneLine"]}\n\n{phases}\n\n{motion_note}\n\n시간은 기존 기획의 검토값이며 수정할 수 있습니다. {effect_note}\n',encoding='utf-8')
    info = {**spec,'sourceSha256':sha(source),'combatReferenceSha256':sha(combat_path),'visibleBounds':list(bb),'visibleSize':[bb[2]-bb[0],bb[3]-bb[1]],'inputSize':list(large.size),'opaqueColors':len(colors),'actualPaletteRgb':[list(c) for c in sorted(colors)],'foregroundUnchanged':True,'previewIsCropped':True,'generatedAttackFrames':0,'runtimeIntegration':'not-performed'}
    (out/'manifest.json').write_text(json.dumps(info,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    items = ''.join(f'<li><strong>{escape(p["name"])} · 약{p["seconds"]:g}초</strong> — {escape(p["action"])}</li>' for p in spec['phaseProposal'])
    queue = ''.join(f'<li>{escape(q["name"])} — {escape(q["oneLine"])}</li>' for q in spec['queue'])
    pose_guide = ''
    if spec.get('poseGuide'):
        guide = ROOT/spec['poseGuide']
        shutil.copy2(guide,refs/'maximum-lift-guide.png')
        assert sha(guide)==sha(refs/'maximum-lift-guide.png')
        pose_guide = '<h2>최대 들기 자세 참고 시안</h2><img src="references/maximum-lift-guide.png" alt="앞몸을 크게 세운 목표 자세 참고"><p class="note">기본 이미지의 정체성과 재질을 참고해 기본 이미지 생성 도구로 만든 자세 가이드입니다. 최종 논리 픽셀 프레임/정확한 팔레트/크기 적용본은 아닙니다. 생성 시작 이미지는 위 원래 기본자세를 사용하고 이 그림은 들기 목표 참고로 사용하세요.</p>'
    page = f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{escape(spec['name'])} · 공격 입력</title>
<style>body{{margin:0;background:#f4f2ee;color:#272935;font:16px/1.7 system-ui,sans-serif}}main{{max-width:1100px;margin:auto;padding:32px}}h1{{margin:0;font-size:30px}}h2{{font-size:22px;margin-top:32px}}.grid{{display:grid;grid-template-columns:440px 1fr;gap:24px}}img{{max-width:100%;image-rendering:pixelated}}.image{{background:white;border:1px solid #ddd;text-align:center;padding:20px}}a{{color:#334f8d}}textarea{{box-sizing:border-box;width:100%;height:460px;font:14px/1.6 monospace;padding:18px;border:1px solid #ccc;background:white}}.links{{display:flex;gap:18px;flex-wrap:wrap;margin:20px 0}}.note{{color:#596171;font-size:14px}}@media(max-width:800px){{.grid{{grid-template-columns:1fr}}}}</style>
<main><h1>{order:02} · {escape(spec['name'])} 공격</h1><p>{escape(spec['oneLine'])}</p><p class="note">공격 제작용 입력과 프롬프트 제안입니다. 아직 공격 모션을 생성하거나 채택한 결과가 아닙니다.</p><div class="grid"><div class="image"><img src="{preview.name}" alt="기존 픽셀 기본본 흰 배경 확대"><p class="note">내용 확인용 확대 · 원래 캐릭터 {bb[2]-bb[0]}×{bb[3]-bb[1]}px</p></div><div><h2>동작 흐름</h2><ol>{items}</ol><p>{escape(motion_note)}</p><p>{escape(effect_note)}</p><p class="note">총 약{total_seconds:g}초는 기존 기획의 검토값입니다. 읽어보고 공격 성격이나 자세를 바꿀 수 있습니다.</p></div></div>
<div class="links"><a href="{full.name}" download>생성기에 넣을 전체 이미지{large.width}×{large.height}</a><a href="prompt-en.txt" download>영문 프롬프트TXT</a><a href="attack-intro-ko.txt">한글 소개·단계</a><a href="{archive_name}" download>이미지+프롬프트ZIP</a></div>
<p class="note">위 확대는 보기용입니다. 생성기에는 여백을 유지한 전체 이미지{large.width}×{large.height}을 넣으세요. 논리{im.width}×{im.height}의 정확한{scale}배이며 기존{len(colors)}색과 불투명 픽셀은 변경하지 않았습니다.</p>{pose_guide}<h2>복사할 프롬프트</h2><textarea readonly onclick="this.select()">{escape(spec['prompt'])}</textarea><h2>진행 순서와 공격 소개</h2><ol>{queue}</ol><p class="note">한 마리씩 입력을 전달하고 반환 모션을 검토합니다. 이동 채택본과 전투 수치·Unity 연결은 변경하지 않습니다.</p></main></html>'''
    (out/'index.html').write_text(page,encoding='utf-8')
    with zipfile.ZipFile(out/archive_name,'w',zipfile.ZIP_DEFLATED) as z:
        for p in [native,full,preview,out/'prompt-en.txt',out/'attack-intro-ko.txt',out/'manifest.json',*sorted(refs.glob('*'))]:
            z.write(p,p.relative_to(out).as_posix())
    print(json.dumps({'output':str(out),'inputSize':list(large.size),'visibleSize':info['visibleSize'],'paletteColors':len(colors),'foregroundUnchanged':True,'fullCanvas4xExact':True,'sourceReferencesByteExact':True,'attackFramesGenerated':0},ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--spec', type=Path, default=SPEC)
    args = parser.parse_args()
    build(args.spec if args.spec.is_absolute() else ROOT/args.spec)
