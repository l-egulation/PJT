"""Make a large, tightly framed Aether input without changing sprite pixels."""
from pathlib import Path
import hashlib
import json
import subprocess

from PIL import Image


def dump(path,value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def main():
    root=Path(__file__).resolve().parents[1]
    repo=root.parent
    if subprocess.check_output(['git','-C',str(repo),'branch','--show-current'],text=True).strip()!='feat/story-unity-essentials':
        raise SystemExit('Branch changed; stopped.')
    asset_root=root/'characters/ink/weapons/hammer'
    current='user-hammer-normal-reused-20261031'
    preview=asset_root/'preview'/current
    source=asset_root/'source'/current
    input_path=preview/'aether-start-native.png'
    original_bytes=input_path.read_bytes()
    sprite=Image.open(input_path).convert('RGBA')
    crop=(48,64,176,160)
    factor=32
    small=sprite.crop(crop)
    bounds=sprite.getbbox()
    assert crop[0]<=bounds[0] and crop[1]<=bounds[1] and crop[2]>=bounds[2] and crop[3]>=bounds[3]
    large=small.resize((small.width*factor,small.height*factor),Image.Resampling.NEAREST)
    transparent=preview/'aether-large-32x-transparent.png'
    white=preview/'aether-large-32x-white.png'
    mapping_path=source/'aether-large-input.json'
    for path in [transparent,white,mapping_path]:
        if path.exists():
            raise SystemExit('Output already exists; refusing to replace '+str(path))
    large.save(transparent)
    background=Image.new('RGBA',large.size,'white')
    background.alpha_composite(large)
    background.convert('RGB').save(white)
    mapping={'purpose':'Aether input only; game sprite and metadata unchanged',
        'input':'preview/'+current+'/aether-start-native.png',
        'originalCanvas':[224,192],'originalPivot':[96,151],
        'cropLTRB':list(crop),'referenceNativeCanvas':list(small.size),
        'integerScale':factor,'outputCanvas':list(large.size),
        'referencePivot':[1536,2784],
        'visibleSpritePixels':[bounds[2]-bounds[0],bounds[3]-bounds[1]],
        'visibleSpriteOutputPixels':[(bounds[2]-bounds[0])*factor,(bounds[3]-bounds[1])*factor],
        'restoreToGame':'Nearest-neighbor downscale by 32, then place at (48,64) on the original 224x192 canvas',
        'startAndEnd':'Use the same large reference image for both',
        'resampling':'nearest-neighbor; no generated or interpolated pixels'}
    dump(mapping_path,mapping)
    prompt_path=source/'aether-prompts.txt'
    generated_path=root/'generated-assets.json'
    generated=json.loads(generated_path.read_text(encoding='utf-8'))
    prompt_record=next(x for x in generated['files'] if x['path']==prompt_path.relative_to(root).as_posix())
    prompt_bytes=prompt_path.read_bytes()
    assert hashlib.sha256(prompt_bytes).hexdigest()==prompt_record['sha256'],'Prompt modified externally'
    backup=repo/'outputs/hammer-reuse-20261031/aether-prompts-before-large-input.txt'
    backup.write_bytes(prompt_bytes)
    text=prompt_bytes.decode('utf-8')
    heading='''망치 모션 제작 입력 — 크게 채운 32배 참조

사용 이미지: preview/user-hammer-normal-reused-20261031/aether-large-32x-white.png
투명본: preview/user-hammer-normal-reused-20261031/aether-large-32x-transparent.png
시작·끝에 같은 큰 이미지를 사용한다. 아래 동작 프롬프트는 그대로 사용한다.
입력 캔버스 4096×3072, 발/root=(1536,2784). 카메라·배율·위치를 유지한다.
캐릭터와 망치의 실제 점유 크기는 2400×2080픽셀이다.
원본 픽셀은 그대로다. 원본 224×192의 (48,64)~(176,160)을 자른 뒤 최근접 32배 확대했다.
게임 복원: 최근접 1/32 축소 후 기존 224×192 캔버스의 (48,64)에 배치한다.
게임용 이미지·피벗·PPU·전투 수치는 변경하지 않는다.
이전 8배 참조와 새 큰 참조를 한 작업의 시작·끝에 섞지 않는다.

'''
    marker='평타 2타 — 아래 영어 문단을 한 번에 입력'
    assert marker in text
    text=heading+text[text.index(marker):]
    prompt_path.write_text(text,encoding='utf-8')
    asset=json.loads((asset_root/'asset.json').read_text(encoding='utf-8'))
    catalog=json.loads((root/'catalog.json').read_text(encoding='utf-8'))
    for path in [transparent,white,mapping_path,prompt_path]:
        rel=path.relative_to(asset_root).as_posix()
        if rel not in asset['files']:asset['files'].append(rel)
        name=path.relative_to(root).as_posix()
        data=path.read_bytes()
        record={'path':name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()}
        old=next((x for x in generated['files'] if x['path']==name),None)
        if old is None:generated['files'].append(record)
        else:old.update(record)
    asset['generatedSets'][current]['largeAetherReference']={'mapping':mapping_path.relative_to(asset_root).as_posix(),
        'white':white.relative_to(asset_root).as_posix(),'transparent':transparent.relative_to(asset_root).as_posix(),
        'gameIntegration':'reference-only; no runtime replacement'}
    next(x for x in catalog['assets'] if x['id']==asset['id'])['fileCount']=len(asset['files'])
    for path,value in [(asset_root/'asset.json',asset),(root/'catalog.json',catalog),(generated_path,generated)]:
        dump(path,value)
    assert input_path.read_bytes()==original_bytes
    assert large.resize(small.size,Image.Resampling.NEAREST).tobytes()==small.tobytes()
    assert large.tobytes()==small.resize(large.size,Image.Resampling.NEAREST).tobytes()
    print(json.dumps({'white':str(white),'transparent':str(transparent),**mapping},ensure_ascii=True))


if __name__=='__main__':
    main()
