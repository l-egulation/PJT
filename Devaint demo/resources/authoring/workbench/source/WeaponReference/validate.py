"""Check the shared snapshot, local HTML links and approved frame manifests."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit, unquote
import hashlib
import json
import re

root = Path(__file__).resolve().parent
asset_manifest = root.parent / 'docs/assets/manifests/manifest.json'
if asset_manifest.exists():
    assets = json.loads(asset_manifest.read_text(encoding='utf-8'))
    unavailable = [item['path'] for item in assets['files']
                   if item['path'].startswith('WeaponReference/')
                   and not (root.parent / item['path']).is_file()]
    if unavailable:
        raise SystemExit(f'External assets not restored ({len(unavailable)} files). '
                         'See docs/assets/README.md and run tools/restore_assets.py first.')
missing = []
references = 0

def check_link(page, value):
    global references
    url = urlsplit(value)
    if url.scheme or url.netloc or not url.path:
        return
    references += 1
    target = (page.parent / unquote(url.path)).resolve()
    if not target.exists() and target != root / '검증결과.json':
        missing.append({'page': str(page.relative_to(root)), 'target': value})

class PageParser(HTMLParser):
    def handle_starttag(self, tag, attributes):
        for key, value in attributes:
            if key in ('href', 'src') and value:
                check_link(self.page, value)

# New independently versioned packages have their own validators.
def legacy(path):
    return path.relative_to(root).parts[0] not in {'LatestMotion', '확정패키지'}

pages = [p for p in root.rglob('*.html') if legacy(p)]
for page in pages:
    parser = PageParser()
    parser.page = page
    parser.feed(page.read_text(encoding='utf-8-sig'))
for page in (p for p in root.rglob('*.md') if legacy(p)):
    for value in re.findall(r'\[[^\]]+\]\(([^)]+)\)', page.read_text(encoding='utf-8-sig')):
        check_link(page, value.strip('<>'))

manifest = json.loads((root / '자료목록.json').read_text(encoding='utf-8'))
hash_errors = [m['file'] for m in manifest['files']
               if hashlib.sha256((root / m['file']).read_bytes()).hexdigest() != m['sha256']]
confirmed = {}
frame_errors = []
for name, relative in [('잉크_마스터','잉크/공통몸체/04_확정본'), ('잉크_채찍모션','잉크/무기/채찍/04_확정본')]:
    folder = root / '자료/캐릭터' / relative
    data = json.loads((folder / '확정프레임_SHA256.json').read_text(encoding='utf-8-sig'))
    records = data.items() if isinstance(data, dict) else [(m['file'], m['sha256']) for m in data]
    records = list(records)
    confirmed[name] = len(records)
    for file, expected in records:
        path = folder / file
        if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            frame_errors.append(name + '/' + file)

result = {'snapshotTime': manifest['snapshotTime'], 'copiedFiles': len(manifest['files']),
          'htmlPagesChecked': len(pages), 'localLinksChecked': references,
          'missingLinks': missing, 'copyHashErrors': hash_errors,
          'confirmedFilesChecked': confirmed, 'confirmedFileErrors': frame_errors,
          'validationScope': 'Static links and file hashes; no Unity integration performed.'}
(root / '검증결과.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False))
if missing or hash_errors or frame_errors or manifest['sourceChangedDuringCopy']:
    raise SystemExit(1)
