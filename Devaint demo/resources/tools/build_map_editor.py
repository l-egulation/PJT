"""Build a review-only offline HTML from an indexed bundle and consumer-owned editor sources."""
import base64
import json
from pathlib import Path
import re

from assets import safe_path
from authoring_output import prepare_output

EDITOR_BUNDLE = 'authoring/map-editor/runtime/맵 편집기.html'


def build(source_root, bundle, destination, assets=None, tile_assets=None):
    source_root = source_root.resolve()
    destination = Path(destination).absolute()
    if destination.suffix.lower() != '.html' or destination.resolve().is_relative_to(source_root):
        raise ValueError('Choose a new HTML review output outside the editor source directory')
    if assets:
        inline = {'assets/' + p.name: 'data:image/png;base64,' + base64.b64encode(p.read_bytes()).decode('ascii')
                  for p in sorted(assets.glob('*.png'))}
    else:
        match = re.search(r'window\.INLINE_ASSETS=(\{[^<]+\});</script>', bundle.decode('utf-8-sig'))
        if not match:
            raise ValueError('Indexed editor bundle has no embedded image data')
        inline = json.loads(match[1])
    if tile_assets:
        for source, name in [('ground.png', 'Terrain32.png'), ('solid-platform.png', 'SolidPlatform.png'),
                             ('drop-through.png', 'DropPlatform.png')]:
            inline['assets/' + name] = 'data:image/png;base64,' + base64.b64encode((tile_assets / source).read_bytes()).decode('ascii')
    required = {'Terrain32.png', 'SolidPlatform.png', 'DropPlatform.png', 'forest_depth.png',
                'forest_near.png', *[f'hero{i}.png' for i in range(4)]}
    if set(inline) != {'assets/' + name for name in required}:
        raise ValueError('Expected the nine approved editor images')
    for value in inline.values():
        if not isinstance(value, str) or not value.startswith('data:image/png;base64,'):
            raise ValueError('Images must be embedded PNG data URLs')
        raw = base64.b64decode(value.split(',', 1)[1], validate=True)
        if not raw.startswith(b'\x89PNG\r\n\x1a\n'):
            raise ValueError('Invalid embedded PNG image')
    for name, size in [('Terrain32.png', (256, 256)), ('SolidPlatform.png', (512, 64)),
                       ('DropPlatform.png', (512, 64))]:
        raw = base64.b64decode(inline['assets/' + name].split(',', 1)[1])
        actual = tuple(int.from_bytes(raw[i:i + 4], 'big') for i in (16, 20))
        if actual != size:
            raise ValueError(f'{name}: expected {size}, received {actual}')
    html = safe_path(source_root, 'index.html').read_text(encoding='utf-8')
    bootstrap = '<script>window.INLINE_ASSETS=' + json.dumps(inline, separators=(',', ':')) + ';</script>'
    html = html.replace('<script src="data/map.js"></script>', bootstrap + '<script src="data/map.js"></script>')

    def embed(match):
        text = safe_path(source_root, match[1]).read_text(encoding='utf-8').replace('</script', '<\\/script')
        return '<script>\n' + text + '\n</script>'

    html = re.sub(r'<script src="([^"]+)"></script>', embed, html)
    initial = base64.b64encode(safe_path(source_root, 'data/placements.json').read_bytes()).decode('ascii')
    html = html.replace('href="data/placements.json" download',
                        'href="data:application/json;base64,' + initial + '" download="initial-placements.json"')
    html = html.replace('href="README.md" target="_blank"', 'href="#usage"')
    helptext = '''<details id="usage"><summary>사용 안내</summary><p>1: 배치 편집 / 2: 지형 편집 / 3: 둘러보기. 바닥을 칠하고 지우거나 사각형으로 수정할 수 있습니다. Space+드래그로 이동하고 Ctrl+Z로 되돌립니다.</p><p>전체 맵 JSON 저장으로 지형과 배치를 저장하세요. 다른 PC에는 이 HTML과 JSON을 함께 전달하고 JSON 불러오기로 이어서 편집합니다. HTML 자체는 저장 버튼으로 변경되지 않습니다. 자동 저장은 현재 브라우저에만 남습니다. 지형과 별도 발판 배치는 다른 레이어입니다.</p><p>위쪽은 잔디, 옆면·밑면은 흙으로 연결합니다. 흙층 발판과 나무·뿌리 통과형 발판을 구분하며 부분 스케치는 상단 잎에 적용합니다. 실제 점프·전투는 제공하지 않으며 Unity 자동 적용은 하지 않습니다. 상세 조작은 함께 제공되는 안내사항.txt를 확인하세요.</p></details>'''
    html = html.replace('</footer>', helptext + '</footer>')
    # Do not overwrite a source bundle, consumer copy or an existing review directory.
    output = prepare_output(destination.parent) / destination.name
    output.write_text(html, encoding='utf-8')
    return {'html': str(output), 'embeddedImages': len(inline), 'bytes': output.stat().st_size}
