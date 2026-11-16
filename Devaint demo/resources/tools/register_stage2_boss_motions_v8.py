"""2스테이지 보스 동작 v8 묶음과 그 Aether 입력·원본 그룹들을 색인에 등록하고 검증한다(다른 그룹 항목은 순서까지 그대로).

    python -I tools/register_stage2_boss_motions_v8.py
"""
import hashlib
import io
import json
from html.parser import HTMLParser
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
S2 = 'monsters/stage-02/'
# 경로 -> (이름, 상태). 이미 등록된 그룹(boss-phase3-attacks-20261106)은 기존 이름·상태·진입점을 지키고 파일만 다시 센다.
GROUPS = {
    'boss-motions-v8-20261108': ('2스테이지 보스 동작 확정본 v8(로즈·블루·흑화 블루 26동작)', 'user-approved-not-unity-integrated'),
    'boss-phase1-attacks-20261107': ('2스테이지 1페이즈 공격 동작 Aether 입력·원본', 'aether-source-for-boss-motions-v8'),
    'boss-phase2-attacks-20261107': ('2스테이지 2페이즈 공격·기절 동작 Aether 입력·원본', 'aether-source-for-boss-motions-v8'),
    'boss-cutscene-motions-20261107': ('2스테이지 보스 컷씬 동작(R1~R8·B2·B3) Aether 입력·원본', 'aether-source-for-boss-motions-v8'),
    'boss-phase3-redesign-20261107': ('흑화 블루 재디자인 3페이즈 동작 Aether 입력·원본', 'aether-source-for-boss-motions-v8'),
    'boss-idle-breath-20261107': ('로즈·블루·흑화 블루 들썩 대기', 'user-approved-not-unity-integrated'),
    'boss-phase3-attacks-20261106': None,
}


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def place(items, entries, key, mine):
    """Replace this group's entries without reordering anyone else's (same rule as register_stage2_phase3_handoff).
    An already registered group is replaced where its block was, so the diff stays inside the group."""
    import bisect
    at = next((i for i, x in enumerate(items) if mine(x)), None)
    if at is not None:
        kept = [x for x in items[:at] if not mine(x)]
        return kept + sorted(entries, key=lambda x: x[key]) + [x for x in items[at:] if not mine(x)]
    kept = [x for x in items if not mine(x)]
    keys = [x[key] for x in kept]
    if keys == sorted(keys):
        for e in sorted(entries, key=lambda x: x[key]):
            kept.insert(bisect.bisect_left([x[key] for x in kept], e[key]), e)
        return kept
    return kept + sorted(entries, key=lambda x: x[key])


class Refs(HTMLParser):
    def __init__(self):
        super().__init__()
        self.paths = []

    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key in ('src', 'href') and value and not value.startswith(('http:', 'https:', '#', 'data:', 'mailto:')):
                self.paths.append(value.split('?')[0].split('#')[0])


def main():
    catalog, manifest = read(ROOT / 'catalog.json'), read(ROOT / 'generated-assets.json')
    total = 0
    for name, meta in GROUPS.items():
        group = S2 + name
        P = ROOT / group
        asset_id = group.replace('/', '.')
        files = sorted(x.relative_to(P).as_posix() for x in P.rglob('*') if x.is_file() and x.name != 'asset.json')
        if meta is None:
            asset = read(P / 'asset.json')
            asset['files'] = files
        else:
            entry = next((e for e in ('preview/index.html', 'README.md', 'motions.json') if (P / e).is_file()), files[0])
            asset = {'format': 'deviant-resource-asset-v1', 'id': asset_id, 'path': group, 'name': meta[0], 'status': meta[1], 'entry': entry, 'files': files}
        write(P / 'asset.json', asset)
        catalog['assets'] = place(catalog['assets'], [{'id': asset_id, 'path': group, 'fileCount': len(files)}], 'id', lambda x, a=asset_id: x['id'] == a)
        entries = []
        for f in files:
            blob = (P / f).read_bytes()
            if f.endswith('.png'):
                Image.open(io.BytesIO(blob)).verify()
            entries.append({'path': group + '/' + f, 'bytes': len(blob), 'sha256': hashlib.sha256(blob).hexdigest()})
        manifest['files'] = place(manifest['files'], entries, 'path', lambda x, g=group: x['path'].startswith(g + '/'))
        # 이 그룹 HTML 의 상대 링크가 저장소 안 실제 파일을 가리키는지
        for html in P.rglob('*.html'):
            refs = Refs()
            refs.feed(html.read_text(encoding='utf-8'))
            for r in refs.paths:
                target = (html.parent / r).resolve()
                target = target / 'index.html' if target.is_dir() else target
                assert target.is_relative_to(ROOT.resolve()) and target.is_file(), (html, r)
        total += len(files)
        print(f'{group}: {len(files)} files')
    write(ROOT / 'catalog.json', catalog)
    write(ROOT / 'generated-assets.json', manifest)
    # 다시 읽어 해시·소유권 확인
    manifest = read(ROOT / 'generated-assets.json')
    import sys
    sys.path.insert(0, str(ROOT / "tools"))
    from assets import Store
    store = Store(ROOT)
    for name in GROUPS:
        group = S2 + name
        for item in manifest['files']:
            if item['path'].startswith(group + '/'):
                blob = (ROOT / item['path']).read_bytes()
                assert len(blob) == item['bytes'] and hashlib.sha256(blob).hexdigest() == item['sha256'], item['path']
                store.read({'asset': group.replace('/', '.'), 'path': item['path']})
    print(f'PASS: {total} files in {len(GROUPS)} groups; PNG decode, byte hashes, ownership, HTML relative links.')


if __name__ == '__main__':
    main()
