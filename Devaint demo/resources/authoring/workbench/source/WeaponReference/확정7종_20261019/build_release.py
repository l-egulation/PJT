"""Export an approved viewer without resampling artwork. Requires Pillow."""
from pathlib import Path
import argparse
import base64
import hashlib
import io
import json
import re
import shutil
from PIL import Image

HERE = Path(__file__).resolve().parent


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf8')


def export(source, html, runtime, gif, out=HERE):
    out.mkdir(parents=True, exist_ok=True)
    source_bytes = source.read_bytes()
    data = json.loads(source_bytes)
    assert [w['id'] for w in data['weapons']] == ['sword', 'whip', 'rapier', 'dagger', 'hammer', 'shield', 'greatsword']
    report = {'sourceSha256': digest(source_bytes), 'weapons': [], 'pixelsResampled': False}

    def asset(uri):
        assert uri.startswith('data:image/png;base64,')
        raw = base64.b64decode(uri.split(',', 1)[1], validate=True)
        image = Image.open(io.BytesIO(raw)).convert('RGBA')
        relative = 'assets/images/' + digest(raw) + '.png'
        target = out / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
        return relative, image

    for weapon in data['weapons']:
        used = list(dict.fromkeys(step['frame'] for seq in weapon['sequences'].values() for step in seq))
        assert all(step['frame'] in used for hit in weapon['hits'] for step in hit['steps'])
        unused = len(weapon['frames']) - len(used)
        weapon['frames'] = {key: weapon['frames'][key] for key in used}
        for key in ('pivots', 'offsets'):
            weapon[key] = {f: v for f, v in weapon[key].items() if f in used}
        weapon['detail'] = 'index.html?weapon=' + weapon['id']
        width, height = data['canvas']
        columns = 8
        sheet = Image.new('RGBA', (width * columns, height * ((len(used) + columns - 1) // columns)))
        sprites = {}
        for i, key in enumerate(used):
            relative, image = asset(weapon['frames'][key])
            weapon['frames'][key] = relative
            pivot = weapon['pivots'].get(key, weapon['pivot'])
            offset = weapon['offsets'].get(key, [0, 0])
            x, y = (data['ground'][a] - pivot[a] + offset[a] for a in (0, 1))
            assert x == int(x) and y == int(y), (weapon['id'], key, 'fractional pixel')
            x, y = int(x), int(y)
            bbox = image.getbbox()
            if bbox:
                assert x + bbox[0] >= 0 and y + bbox[1] >= 0
                assert x + bbox[2] <= width and y + bbox[3] <= height, (weapon['id'], key, 'clipped sprite')
            col, row = i % columns, i // columns
            sheet.alpha_composite(image, (col * width + x, row * height + y))
            sprites[key] = {'rectTopLeft': [col * width, row * height, width, height],
                            'unityRectBottomLeft': [col * width, sheet.height - (row + 1) * height, width, height],
                            'pivotPixelsTopLeft': data['ground'],
                            'unityPivotNormalized': [data['ground'][0] / width, 1 - data['ground'][1] / height]}
        for effect in weapon['fx']:
            effect['png'], _ = asset(effect['png'])
        folder = out / 'weapons' / weapon['id']
        folder.mkdir(parents=True, exist_ok=True)
        sheet.save(folder / 'sprites.png')
        write_json(folder / 'animations.json', {
            'id': weapon['id'], 'name': weapon['name'], 'sheet': 'sprites.png',
            'filterMode': 'Point', 'compression': 'None', 'cell': data['canvas'],
            'sprites': sprites, 'sequences': weapon['sequences'], 'hits': weapon['hits'],
            'manualInput': weapon['manualInput'], 'fx': weapon['fx'],
            'effectPathBase': '../../', 'timeUnit': 'milliseconds'})
        report['weapons'].append({'id': weapon['id'], 'usedFrames': len(used), 'unusedFramesExcluded': unused,
                                  'effects': len(weapon['fx']), 'attackMs': sum(s['ms'] for s in weapon['sequences']['attack'])})

    write_json(out / 'data.json', data)
    (out / 'data.js').write_text('const D=' + json.dumps(data, ensure_ascii=False, separators=(',', ':')) + ';\n', encoding='utf8')
    page = re.sub(r'<script>[\s\S]*?</script>', '<script src="data.js"></script><script src="viewer.js"></script>', html.read_text(encoding='utf8'), count=1)
    page = re.sub(r'href="[^"]*무기7종_공격_이펙트.gif"', 'href="assets/무기7종_공격_이펙트.gif"', page)
    page = page.replace('모션 준비 중…', '모션 준비 중… 이미지가 없으면 README의 ZIP 복원을 먼저 진행하세요.')
    page = page.replace('잉크 · 클릭 반응 / 연계 보정', '잉크 · 7종 무기 확정본')
    (out / 'index.html').write_text(page, encoding='utf8')
    shutil.copyfile(runtime, out / 'viewer.js')
    shutil.copyfile(gif, out / 'assets/무기7종_공격_이펙트.gif')
    (out / 'source').mkdir(exist_ok=True)
    (out / 'source/viewer-data.json').write_bytes(source_bytes)
    write_json(out / 'export-report.json', report)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    for name in ('source', 'html', 'runtime', 'gif'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--out', type=Path, default=HERE)
    args = parser.parse_args()
    print(json.dumps(export(args.source, args.html, args.runtime, args.gif, args.out), ensure_ascii=False))
