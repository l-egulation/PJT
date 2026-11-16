"""Validate individual item PNGs, build the review gallery and refresh this asset's indices.

Reads pixels for alpha validation only; never edits or re-encodes artwork.
"""
from pathlib import Path
import hashlib
import html
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
REL = 'ui/inventory-symbols-20261106'
GROUP = ROOT / REL


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    newline = '\r\n' if path.exists() and b'\r\n' in path.read_bytes() else '\n'
    text = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').replace('\n', newline)
    path.write_bytes(text.encode('utf-8'))


def main():
    jobs = read(GROUP / 'source/production-prompts.json')
    if len(jobs) != 71 or len({j['id'] for j in jobs}) != 71:
        raise ValueError('Expected 71 unique production jobs')
    items = []
    for job in jobs:
        path = GROUP / 'runtime/items' / (job['id'] + '.png')
        with Image.open(path) as image:
            image.load()
            if 'A' not in image.getbands():
                raise ValueError('Missing alpha: ' + str(path))
            alpha = image.getchannel('A')
            if alpha.getextrema() != (0, 255):
                raise ValueError('Missing transparent/opaque pixels: ' + str(path))
            # Ignore almost-transparent antialias noise when reporting visible content bounds.
            bounds = alpha.point(lambda a: 255 if a >= 16 else 0).getbbox()
            width, height = image.size
            if not bounds or width < 256 or height < 256:
                raise ValueError('Empty or undersized item: ' + str(path))
            # Blank backings have no emblem-shaped alpha holes. Such holes disappear on
            # a transparent viewer's dark background but read as white ghosts on paper.
            if job['id'].startswith('backing-ink-'):
                interior = alpha.crop((int(width * .4), int(height * .4),
                                       int(width * .6), int(height * .8)))
                if interior.getextrema()[0] < 240:
                    raise ValueError('Translucent erased emblem inside blank backing: ' + str(path))
        data = path.read_bytes()
        items.append(dict(id=job['id'], name=job['name'], kind=job['kind'],
                          file=path.relative_to(GROUP).as_posix(), bytes=len(data),
                          sha256=hashlib.sha256(data).hexdigest(), width=width, height=height,
                          contentBounds=bounds))
    write(GROUP / 'runtime/items.json', dict(schema=1, count=71,
          gameIntegration='shared-inventory-maintenance-ui', items=items))
    page = ['''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>DEVIANT · 개별 아이템 71개</title><style>
:root{--size:96px;--back:#f4ead7}body{font:15px/1.6 system-ui;background:#e8e1d6;color:#24211c;margin:0}
main{max-width:1250px;margin:auto;padding:28px}nav{position:sticky;top:0;background:#e8e1d6;padding:10px;display:flex;gap:18px;flex-wrap:wrap}
.grid{display:flex;flex-wrap:wrap;gap:12px}.item{width:145px;padding:12px;background:var(--back);border-radius:8px;text-align:center}
.item img{width:var(--size);height:var(--size);object-fit:contain}.item a{display:block;color:inherit}small{font-size:10px;overflow-wrap:anywhere}
label,button{cursor:pointer}h2{margin-top:36px}</style><main><h1>개별 아이템 · 71개</h1>
<p>잉크 54 · 형태 스탬프 7 · 스킬 스탬프 3 · 빈 바탕 7. 각각 새로 생성한 투명 PNG 원본이다. 등급·개수는 별도 UI 요소다.</p>
<nav><label>크기 <select onchange="document.documentElement.style.setProperty('--size',this.value+'px')"><option>48</option><option>64</option><option selected>96</option><option>128</option></select></label>
<button onclick="document.documentElement.style.setProperty('--back','#f4ead7');document.querySelectorAll('.item').forEach(x=>x.style.color='#24211c')">밝은 배경</button>
<button onclick="document.documentElement.style.setProperty('--back','#30302d');document.querySelectorAll('.item').forEach(x=>x.style.color='#fff5e5')">어두운 배경</button>
<a href="stamp-backgrounds-v2.html">채택 바탕</a><a href="index-v2.html">채택 문양</a><a href="../runtime/items.json">ID·해시 목록</a></nav>''']
    for key, title in [('red','빨강 10종'), ('yellow','노랑 12종'), ('green','초록 10종'),
                       ('blue','파랑 11종'), ('purple','보라 11종'),
                       ('weapon-stamp','형태 스탬프 7종'), ('skill-stamp','스킬 스탬프 3종'), ('backing','빈 바탕 7종')]:
        selected = [i for i in items if i['id'].startswith('ink3_' + key + '_') or i['kind'] == key]
        page.append('<h2>' + title + '</h2><div class="grid">')
        for item in selected:
            src = '../' + item['file']
            page.append(f'<div class="item"><a href="{src}"><img loading="lazy" src="{src}" alt="{html.escape(item["name"])}"></a>'
                        f'<div>{html.escape(item["name"])}</div><small>{item["id"]}</small></div>')
        page.append('</div>')
    page.append('</main></html>')
    (GROUP / 'preview/production-items.html').write_text(''.join(page), encoding='utf-8')
    asset = read(GROUP / 'asset.json')
    asset.update(status='individual-production-sprites', approval='v2 glyphs, existing teardrop and D stamp direction approved',
                 gameIntegration='shared-inventory-maintenance-ui',
                 backgroundApproval=dict(status='approved-direction', variant='d'))
    asset['files'] = sorted(p.relative_to(GROUP).as_posix() for p in GROUP.rglob('*') if p.is_file() and p.name != 'asset.json')
    asset['sources'] = list(dict.fromkeys(asset['sources'] + ['source/production-prompts.json', 'source/production-generation-files.json']))
    write(GROUP / 'asset.json', asset)
    catalog = read(ROOT / 'catalog.json')
    for item in catalog['assets']:
        if item['id'] == asset['id']:
            item['fileCount'] = len(asset['files'])
    write(ROOT / 'catalog.json', catalog)
    generated = read(ROOT / 'generated-assets.json')
    records = []
    for path in [GROUP / 'asset.json', *(GROUP / f for f in asset['files'])]:
        data = path.read_bytes()
        records.append(dict(path=path.relative_to(ROOT).as_posix(), bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
    updated = {r['path']: r for r in records}
    generated['files'] = [updated.pop(r['path'], r) for r in generated['files']]
    generated['files'].extend(updated.values())
    write(ROOT / 'generated-assets.json', generated)
    print('Validated 71 individual RGBA sprites; gallery and resource indices refreshed.')


if __name__ == '__main__':
    main()
