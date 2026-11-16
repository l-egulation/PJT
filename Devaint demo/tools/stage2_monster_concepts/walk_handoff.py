"""Package existing normal-monster pixel bases and self-contained walk prompts."""
from pathlib import Path
import hashlib
from html import escape
import json
import shutil
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SPEC = ROOT / 'data/presentation/stage2_normal_walk_handoff.json'


def build():
    spec = json.loads(SPEC.read_text(encoding='utf-8'))
    base, out = ROOT / spec['base'], ROOT / spec['output']
    out.mkdir(parents=True, exist_ok=True)
    rows, cards, texts = [], [], []
    for asset in spec['assets']:
        ident = asset['id']
        folder = out / ident
        folder.mkdir(exist_ok=True)
        source = base / ident / 'native/base-frame.png'
        native = folder / 'reference-native.png'
        shutil.copy2(source, native)
        shutil.copy2(base / ident / 'palette.json', folder / 'palette.json')
        im = Image.open(native).convert('RGBA')
        assert im.size == tuple(spec['canvas'])
        scale = spec['scale']
        enlarged = im.resize((im.width * scale, im.height * scale), Image.Resampling.NEAREST)
        enlarged.save(folder / 'input-4x.png')
        im.crop(im.getbbox()).resize(tuple(v * scale for v in im.crop(im.getbbox()).size), Image.Resampling.NEAREST).save(folder / 'preview-cropped-4x.png')
        prompt = spec['commonPrompt'] + '\n\n' + asset['prompt']
        (folder / 'prompt.txt').write_text(prompt + '\n', encoding='utf-8')
        row = {**asset, 'canvas': spec['canvas'], 'inputCanvas': list(enlarged.size),
               'source': source.relative_to(ROOT).as_posix(),
               'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
               'visibleSize': list(im.crop(im.getbbox()).size), 'referencePoseOnly': True}
        (folder / 'metadata.json').write_text(json.dumps(row, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        rows.append(row)
        texts.append(f'# {ident} {asset["name"]}\n\n입력: {ident}/input-4x.png\n\n{prompt}\n')
        cards.append(f'''<section id="{ident}"><h2>{escape(asset['name'])}</h2><p>{escape(asset['motion'])} · 실제 그림 {row['visibleSize'][0]}×{row['visibleSize'][1]}px</p><div class="scroll"><img src="{ident}/preview-cropped-4x.png" alt="{escape(asset['name'])} 기존 기본본 확대"></div><p><a download href="{ident}/input-4x.png">에테르 입력 PNG · 4배 고정 캔버스</a> · <a download href="{ident}/reference-native.png">원래 크기 PNG</a> · <a href="{ident}/prompt.txt">프롬프트 TXT</a></p><textarea readonly onclick="this.select()" aria-label="{escape(asset['name'])} 걷기 프롬프트">{escape(prompt)}</textarea></section>''')
    (out / 'prompts.md').write_text('\n\n'.join(texts), encoding='utf-8')
    (out / 'manifest.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (out / 'motion-spec.json').write_bytes(SPEC.read_bytes())
    (out / 'README.md').write_text('''# 잡몹 5종 걷기 입력과 프롬프트

기존 정지 픽셀 기본본을 복사한 입력 묶음이다. 이동 모션은 아직 생성하지 않았다. 개체별 input-4x.png와 prompt.txt를 함께 사용한다. input-4x.png는 1536×896 투명 고정 캔버스이며 논리 크기는384×224다. preview-cropped-4x.png는 감상용 크롭이므로 프레임 정렬용 입력은 input-4x.png를 우선한다.

모두 왼쪽을 향한 제자리 루프를 요청한다. 지상4종은 보행, 수의는 부유 이동이다. 여덟 프레임과 주기는 제작 제안이며 게임 이동속도 확정값이 아니다. 에테르가 캔버스나 색·픽셀 격자를 바꾸면 반환 원본을 그대로 보존한 뒤 기준본과 비교하여 보정한다. 반환 시 프레임 번호와 동일 캔버스 크기·여백·발 정렬을 유지한다. 실제 크기로 환원할 때는 정확한4분의1 최근접 축소를 사용한다. 원래 기본본과 팔레트를 함께 보존한다.
''', encoding='utf-8')
    (out / 'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>잡몹5종 걷기 입력·프롬프트</title><style>body{margin:0;background:#18232c;color:#ede5d7;font:16px/1.7 system-ui}main{max-width:1080px;margin:auto;padding:24px}a{color:#e6bc80}section{padding:24px;background:#26323c;margin:24px 0;border-radius:12px}.scroll{overflow:auto}img{image-rendering:pixelated}textarea{box-sizing:border-box;width:100%;height:280px;padding:14px;background:#17212a;color:#f2eadc;border:1px solid #71828b;font:14px/1.6 monospace}</style><main><h1>잡몹5종 · 걷기 입력과 프롬프트</h1><p>기존 픽셀 기본본과 개체별 이동 지시입니다. 지상4종은 보행, 늘어진 수의는 부유 이동입니다. 아래 그림은 정지 참고본입니다.</p><p>각 개체의 <b>에테르 입력 PNG</b>와 바로 아래 프롬프트를 함께 사용하세요. 모두 왼쪽을 향한 제자리8프레임 루프이며 주기는 시험 제안입니다. 입력은1536×896 투명 PNG, 논리 크기는384×224입니다. 프롬프트 칸을 누르면 전체 선택됩니다.</p><p><a download href="normal-walk-handoff-20261031.zip">5종 이미지·프롬프트 ZIP</a> · <a href="prompts.md">전체 프롬프트</a></p>''' + ''.join(cards) + '</main></html>', encoding='utf-8')
    with zipfile.ZipFile(out / 'normal-walk-handoff-20261031.zip', 'w', zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix != '.zip':
                z.write(p, p.relative_to(out).as_posix())
    print(json.dumps([{'id': r['id'], 'visibleSize': r['visibleSize'], 'inputCanvas': r['inputCanvas']} for r in rows]))


if __name__ == '__main__':
    build()
