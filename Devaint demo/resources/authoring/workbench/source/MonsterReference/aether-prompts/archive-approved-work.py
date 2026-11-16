"""Reversibly archive completed experiments; never rewrite approved sprites."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
ARCHIVE = ROOT / 'local-assets/99_archive/2026-10-26-approved-monsters'
RECEIPT = ROOT / 'maintenance/cleanup-20261026.json'
SCRIPTS = [
    'approve-elite-hook-range.py', 'approve-elite-impact-death.py',
    'build-elite-heavy-death.py', 'build-elite-impact-death.py',
    'smooth-elite-heavy-death.py', 'extend-elite-hook-range.py',
    'finalize-heavy-walk.cjs', 'outline-approved-pixels.cjs',
    'repair-elite-idle-walk.cjs', 'extract-heavy-walk.cjs',
    'build-heavy-walk-review.cjs', 'build-heavy-walk-smooth.cjs',
    'build-heavy-walk-inputs.cjs', 'build-pixel-polish-review.cjs',
    'polish-approved-pixels.cjs',
]
FOLDERS = [
    'base-v43', 'death-v50', 'idle-v48', 'elite-detail-fix-v1',
    'elite-heavy-death-v1', 'elite-heavy-death-v2', 'elite-impact-death-v3',
    'elite-impact-death-v4', 'elite-hook-range-v1', 'heavy-walk-extract-v1',
    'heavy-walk-inputs-v1', 'heavy-walk-pixel-v3', 'heavy-walk-smooth-v2',
    'pixel-polish-v1', 'player-monster-lineup-v1',
]
paths = ['aether-prompts/' + s for s in SCRIPTS]
paths += ['local-assets/' + s for s in FOLDERS]
paths += ['output/imagegen/elite-heavy-death-v' + str(n) for n in [1, 2, 3]]
if RECEIPT.exists():
    raise SystemExit('Archive receipt already exists; no moves performed.')
plan = []
for rel in paths:
    src = (ROOT / rel).resolve()
    dst = (ARCHIVE / rel).resolve()
    assert src.is_relative_to(ROOT) and dst.is_relative_to(ARCHIVE)
    assert not src.is_relative_to(ARCHIVE)
    if not src.exists():
        continue
    assert not dst.exists(), dst
    files = [src] if src.is_file() else sorted(p for p in src.rglob('*') if p.is_file())
    inventory = [{'path': str(p.relative_to(ROOT)).replace('\\', '/'),
                  'bytes': p.stat().st_size,
                  'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]
    plan.append((src, dst, {'source': rel, 'destination': str(dst.relative_to(ROOT)).replace('\\', '/'),
                            'files': inventory}))
# All absolute targets are checked before any recursive directory move.
for src, dst, item in plan:
    dst.parent.mkdir(parents=True, exist_ok=True)
    src.rename(dst)
    for entry in item['files']:
        archived = ARCHIVE / entry['path']
        assert hashlib.sha256(archived.read_bytes()).hexdigest() == entry['sha256']
result = {'date': '2026-10-26', 'archiveRoot': str(ARCHIVE.relative_to(ROOT)).replace('\\', '/'),
          'policy': 'Local reversible archive; original paths retained. Restore tools to original locations before running. Never overwrite approved assets.',
          'preserved': ['approved/', 'editor/', 'previews/approved/', 'local-assets/pixel-outline-v2/',
                        'local-assets/approved-motion-gifs-v1/', 'local-assets/player-monster-lineup-v2/',
                        'local-assets/boss-two-fists-v3/', 'local-assets/selected-large-review-v1/',
                        'local-assets/large-attack-definitions/', 'local-assets/elite-claw-followup-input-v2/'],
          'entries': [item for _, _, item in plan]}
RECEIPT.parent.mkdir(parents=True, exist_ok=True)
inventory_bytes = (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
(ARCHIVE / 'inventory.json').write_bytes(inventory_bytes)
summary = {k: v for k, v in result.items() if k != 'entries'}
summary['inventory'] = {'path': str((ARCHIVE / 'inventory.json').relative_to(ROOT)).replace('\\', '/'),
                        'sha256': hashlib.sha256(inventory_bytes).hexdigest()}
summary['entries'] = [{'source': item['source'], 'destination': item['destination'],
                       'fileCount': len(item['files']), 'bytes': sum(f['bytes'] for f in item['files'])}
                      for _, _, item in plan]
RECEIPT.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'archivedRoots': len(plan), 'files': sum(len(i['files']) for _, _, i in plan),
                  'bytes': sum(f['bytes'] for _, _, i in plan for f in i['files']),
                  'receipt': str(RECEIPT)}, ensure_ascii=False))
