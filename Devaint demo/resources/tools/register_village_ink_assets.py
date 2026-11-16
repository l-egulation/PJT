"""Index original generated village-ink PNGs without changing image pixels."""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from html.parser import HTMLParser
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = 'ui/village-ink-return-20261107'
COLORS = ('red', 'yellow', 'green', 'blue', 'purple')
STATES = ('closed', 'open', 'cap', 'empty')
UI_NAMES = ('notebook-paper', 'executioner-notebook', 'eroders-notebook', 'well-ink-notebook', 'title-banner', 'title-banner-blank', 'counter-banner-blank',
            'progress-drop-empty', 'progress-drop-filled')


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def image_record(path, base):
    raw = path.read_bytes()
    if raw.startswith(b'version https://git-lfs.github.com/spec/v1'):
        raise ValueError(f'LFS pointer instead of image: {path}')
    with Image.open(path) as im:
        if im.mode != 'RGBA':
            raise ValueError(f'Missing RGBA alpha: {path}')
        alpha = im.getchannel('A')
        # Some built-in exports use alpha 254 for solid strokes; preserve their original bytes.
        if alpha.getextrema()[0] != 0 or alpha.getextrema()[1] < 250:
            raise ValueError(f'Expected transparent exterior and opaque sprite pixels: {path}')
        bounds = alpha.point(lambda a: 255 if a >= 64 else 0).getbbox()
        if bounds is None:
            raise ValueError(f'Empty image: {path}')
        w, h = im.size
        corners = [alpha.getpixel(p) for p in ((0, 0), (w-1, 0), (0, h-1), (w-1, h-1))]
        if max(corners) > 16:
            raise ValueError(f'Background not transparent at corners: {path}')
    return {'file': path.relative_to(base).as_posix(), 'width': w, 'height': h,
            'contentBounds': list(bounds), 'contentBoundsConvention': 'left-top-right-bottom, alpha>=64',
            'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(),
            'preserveCanvas': True, 'pivot': [0.5, 0.5], 'filter': 'Point', 'wrap': 'Clamp'}


def make_preview(base, layout):
    colors = {'red': '붉은 잉크', 'yellow': '금빛 잉크', 'green': '녹색 잉크',
              'blue': '푸른 잉크', 'purple': '보랏빛 잉크'}
    out = ['<!doctype html><html lang="ko"><meta charset="utf-8">',
           '<meta name="viewport" content="width=device-width,initial-scale=1">',
           '<title>마을 잉크 에셋</title><style>',
           'body{margin:0;background:#202126;color:#eee;font:14px "Malgun Gothic",sans-serif}',
           'main{max-width:1320px;margin:auto;padding:24px}h1{font-size:24px;margin:0 0 12px}',
           'p{color:#bdbdc6}button,a{font:inherit}button{padding:8px 14px;margin:0 6px 8px 0}',
           '.grid{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:12px}',
           '.ink{min-width:0}.scene{position:relative;aspect-ratio:1;background:var(--surface,#45454b);overflow:hidden}',
           '.scene img{position:absolute;inset:0;width:100%;height:100%;object-fit:contain;image-rendering:pixelated}',
           '.cap{transition:transform .4s}.open .cap,.empty .cap{transform:translateY(-15%)}',
           '.body-empty{display:none}.empty .body-full{display:none}.empty .body-empty{display:block}',
           '.tiles{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:4px;margin-top:12px}',
           '.tile{background:var(--surface,#45454b);padding:5px}.tile img{width:100%;aspect-ratio:1;object-fit:contain;image-rendering:pixelated}',
           '.tile a{display:block;color:#dedee7;text-decoration:none;font-size:12px}',
           '.drop{height:70px;display:flex;align-items:end;gap:12px;background:var(--surface,#45454b);padding:10px;margin-top:10px}',
           '.drop img{image-rendering:pixelated;object-fit:contain}.ui{display:flex;gap:20px;flex-wrap:wrap}',
           '.ui img{width:320px;max-width:100%;height:120px;object-fit:contain;background:var(--surface,#45454b)}',
           'h2{margin-top:26px;font-size:18px}h3{font-size:14px}',
           '@media(max-width:850px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}',
           '</style><main><h1>마을 잉크 에셋</h1>',
           '<p>닫힌 병 원본과 분리된 열린 병, 빈 병, 뚜껑, 드롭 이미지. 조립 미리보기는 같은 원본 캔버스를 겹칩니다.</p>',
           '<div><button data-state="closed">뚜껑 닫기</button><button data-state="open">뚜껑 열기</button>',
           '<button data-state="empty">잉크 비우기</button><button id="surface">배경 바꾸기</button></div>',
           '<div class="grid">']
    for color in COLORS:
        out += [f'<section class="ink"><h3>{colors[color]}</h3><div class="scene">',
                f'<img class="body-full" src="../runtime/acquisition/{color}-open.png" alt="열린 병">',
                f'<img class="body-empty" src="../runtime/acquisition/{color}-empty.png" alt="빈 병">',
                f'<img class="cap" src="../runtime/acquisition/{color}-cap.png" alt="분리 뚜껑">',
                '</div><div class="tiles">']
        for state, name in (('closed', '닫힌 병'), ('open', '열린 병'), ('cap', '뚜껑'), ('empty', '빈 병')):
            file = f'../runtime/acquisition/{color}-{state}.png'
            out += [f'<div class="tile"><a href="{file}">{name}<img src="{file}" alt="{name}"></a></div>']
        drop = layout['inks'][color]['drop']
        bx0, by0, bx1, by1 = drop['contentBounds']
        ratio = 32 / (by1 - by0)
        # Preserve the source bytes; CSS only controls the preview size.
        out += ['</div><div class="drop">',
                f'<a href="../runtime/drops/{color}.png"><img src="../runtime/drops/{color}.png" ',
                f'style="width:{drop["width"]*ratio:.2f}px;height:{drop["height"]*ratio:.2f}px" alt="드롭 32px 예시"></a>',
                '<span>드롭<br>높이 32px 예시</span></div></section>']
    out += ['</div><h2>획득 UI 장식판</h2><div class="ui">']
    for name in UI_NAMES:
        out += [f'<a href="../runtime/ui/{name}.png"><img src="../runtime/ui/{name}.png" alt="{name}"></a>']
    out += ['</div><p><a href="../runtime/layout.json">원본 캔버스와 정렬 좌표</a></p></main><script>',
            'for(const b of document.querySelectorAll("[data-state]")){b.onclick=()=>{',
            'for(const s of document.querySelectorAll(".scene")){s.classList.remove("open","empty");',
            'if(b.dataset.state!=="closed")s.classList.add(b.dataset.state)}}}',
            'let light=false;document.getElementById("surface").onclick=()=>{light=!light;',
            'document.body.style.setProperty("--surface",light?"#f0eade":"#45454b")};',
            '</script></html>']
    (base / 'preview/index.html').write_text('\n'.join(line.rstrip() for line in '\n'.join(out).splitlines()) + '\n', encoding='utf-8')


def validate_preview(base):
    class Preview(HTMLParser):
        def __init__(self):
            super().__init__()
            self.paths = []
            self.scripts = []
            self.in_script = False

        def handle_starttag(self, tag, attrs):
            if tag == 'script':
                self.in_script = True
            for key, value in attrs:
                if key in ('src', 'href'):
                    self.paths.append(value)

        def handle_endtag(self, tag):
            if tag == 'script':
                self.in_script = False

        def handle_data(self, data):
            if self.in_script:
                self.scripts.append(data)

    parser = Preview()
    page = base / 'preview/index.html'
    parser.feed(page.read_text(encoding='utf-8'))
    for path in parser.paths:
        target = (page.parent / path).resolve()
        if not target.is_relative_to(base.resolve()) or not target.exists():
            raise ValueError(f'Broken preview reference: {path}')
    check = subprocess.run(['node', '--check'], input='\n'.join(parser.scripts),
                           text=True, encoding='utf-8', capture_output=True)
    if check.returncode:
        raise ValueError(check.stderr)
    return len(parser.paths)


def build():
    base = ROOT / GROUP
    generation = json.loads((base / 'source/generation.json').read_text(encoding='utf-8'))
    records = {r['runtime']: r for r in generation['records']}
    expected = [f'runtime/acquisition/{c}-{s}.png' for c in COLORS for s in STATES]
    expected += [f'runtime/drops/{c}.png' for c in COLORS]
    expected += [f'runtime/ui/{n}.png' for n in UI_NAMES]
    if set(records) != set(expected):
        raise ValueError(f'Missing={set(expected)-set(records)} extra={set(records)-set(expected)}')
    items = {}
    for name in expected:
        item = image_record(base / name, base)
        generated = base / 'source/generated' / (records[name]['id'] + '.png')
        if generated.read_bytes() != (base / name).read_bytes():
            raise ValueError(f'Generated original was not copied byte-for-byte: {name}')
        items[name] = item
    layout = {'schema': 1, 'assetId': 'ui.village-ink-return-20261107',
              'gameIntegration': 'Unity VillageInk bindings; first-red tutorial and later well-F returns',
              'coordinates': 'top-left pixel coordinates in the untrimmed original canvas',
              'imageProcessing': 'none; original imagegen PNG bytes preserved', 'inks': {}, 'ui': {}}
    for color in COLORS:
        states = {state: items[f'runtime/acquisition/{color}-{state}.png'] for state in STATES}
        dimensions = {(r['width'], r['height']) for r in states.values()}
        if len(dimensions) != 1:
            raise ValueError(f'Animation states have different canvas sizes: {color}: {dimensions}')
        closed, opened, cap, empty = (states[s] for s in STATES)
        cb, ob, eb, capb = (r['contentBounds'] for r in (closed, opened, empty, cap))
        # Imagegen may shift an antialiased contour by a few source pixels.
        if max(abs(ob[i]-eb[i]) for i in (0, 2, 3)) > 12:
            raise ValueError(f'Empty-body contour lost registration: {color}, {ob} vs {eb}')
        if max(abs(ob[i]-cb[i]) for i in (0, 2, 3)) > 12:
            raise ValueError(f'Open-body contour lost registration: {color}')
        w, h = closed['width'], closed['height']
        neck_x = (capb[0]+capb[2])/2
        layout['inks'][color] = {**states, 'drop': items[f'runtime/drops/{color}.png'],
            'frame': {'width': w, 'height': h, 'preserveCanvasForAllStates': True,
                      'capDockPixel': [neck_x, capb[3]],
                      'capDockNormalized': [round(neck_x/w, 6), round(capb[3]/h, 6)],
                      'pourOriginPixel': [neck_x, ob[1]+12],
                      'recommendedDropContentHeight': 32,
                      'openEmptyBoundsDeviation': [eb[i]-ob[i] for i in range(4)]}}
    for name in UI_NAMES:
        layout['ui'][name] = items[f'runtime/ui/{name}.png']
    write_json(base / 'runtime/layout.json', layout)
    make_preview(base, layout)
    preview_links = validate_preview(base)
    readme = '''# 마을 잉크 귀환 연출 에셋

사용자가 선택한 5종 잉크의 제작본이다. 획득 화면용은 촘촘한 픽셀, 드롭용은 별도 단순화 디자인을 쓴다.

- `runtime/acquisition/`: 색마다 `closed`, `open`, `cap`, `empty` 투명 PNG.
- `runtime/drops/`: 개별 드롭 투명 PNG 5개.
- `runtime/ui/`: 제목판, 빈 제목판, 빈 숫자판, 빈 방울, 색을 적용하는 흰 방울, 집행자가 마을을 부수는 수첩 삽화와 별도 수첩 종이.
- `runtime/layout.json`: 원본 크기, 내용 경계, 같은 캔버스의 뚜껑 기준점과 잉크 배출 시작점.
- `preview/index.html`: 실제 PNG 조립과 뚜껑 이동, 빈 상태 전환 미리보기.
- `source/generated/`: 생성 결과의 원본 바이트. `runtime/` 사본도 재인코딩하지 않았다.
- `source/references/`: 사용자가 선택한 디자인과 컷씬 예시. `source/generation.json`에 전체 프롬프트를 보존한다.

## 정렬과 사용

획득용 4개 상태는 색별로 원본 캔버스 크기가 같다. 투명 여백을 자동으로 자르지 말고 같은 사각형과 중심 피벗에 놓는다. `open`과 `cap`을 겹친 다음 뚜껑을 위로 옮겨 열고, 잉크 배출 뒤 병을 `empty`로 바꾼다. `closed`는 독립 완성 이미지이며 분리 레이어와의 미세한 생성 차이가 있을 수 있다.

드롭은 전체 캔버스 대신 `contentBounds`를 사용해 표시 크기를 정한다. 원본을 보존한 채 Unity Sprite rect 또는 UI UV/별도 배치 메타데이터로 여백을 처리한다. Point 필터, Clamp, 압축 없음이 기본 권장이다. 작은 드롭의 화면 높이는 32px를 검토용 시작값으로 제시하며 본 게임 수치는 확정하지 않는다.

숫자는 장식판에 구워 넣지 않았다. 게임 글꼴로 `0 / 5`부터 `5 / 5`까지 표시한다. 메인 Unity 바인딩은 첫 붉은 잉크 장인 설명과 후속 우물 F 사용을 구분한다. 작은 병은 5.2초 동안 뚜껑을 열고 수직으로 잉크를 흘리며 2.6초에 색 번짐을 시작한다. 이후 닫힌 병 획득 UI를 F로 확인한다. 사용법은 메인 `docs/assets/village-ink-integration.md`를 따른다.

원본 투명 PNG와 소스 캔버스를 보존하며 적용본도 원본 바이트 그대로다. 리소스 커밋을 메인의 gitlink와 배치 계약으로 선택한다. 내장 이미지 생성 도구를 사용했고 외부 이미지 서버는 사용하지 않았다.
'''
    (base / 'README.md').write_text(readme, encoding='utf-8')
    names = sorted(p.relative_to(base).as_posix() for p in base.rglob('*')
                   if p.is_file() and p.name != 'asset.json')
    write_json(base / 'asset.json', {'format': 'deviant-resource-asset-v1',
        'id': 'ui.village-ink-return-20261107', 'path': GROUP,
        'name': '마을 잉크 5종과 분리 뚜껑, 귀환 획득 UI',
        'approval': 'user-approved village ink sprites, reward UI and notebook direction',
        'gameIntegration': 'Unity VillageInk bindings; first-red tutorial and later well-F returns', 'sourceTool': 'built-in image_gen',
        'sources': ['source/generation.json', 'source/references/'], 'files': names})
    catalog_path = ROOT / 'catalog.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    catalog['assets'] = [a for a in catalog['assets'] if a['id'] != 'ui.village-ink-return-20261107']
    catalog['assets'].append({'id': 'ui.village-ink-return-20261107', 'path': GROUP, 'fileCount': len(names)})
    write_json(catalog_path, catalog)
    manifest_path = ROOT / 'generated-assets.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['files'] = [r for r in manifest['files'] if not r['path'].startswith(GROUP+'/')]
    for name in names:
        raw = (base / name).read_bytes()
        manifest['files'].append({'path': GROUP+'/'+name, 'bytes': len(raw),
                                  'sha256': hashlib.sha256(raw).hexdigest()})
    write_json(manifest_path, manifest)
    return {'runtimePngs': len(expected), 'inks': len(COLORS), 'indexedFiles': len(names),
            'previewLinksChecked': preview_links, 'previewJavaScript': 'syntax-passed',
            'registeredCanvases': {c: layout['inks'][c]['frame'] for c in COLORS},
            'preview': str(base/'preview/index.html')}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    print(json.dumps(build(), ensure_ascii=False, indent=2))
