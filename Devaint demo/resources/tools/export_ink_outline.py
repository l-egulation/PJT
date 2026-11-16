"""Bake the player's ink outline into selected monster/NPC frames as a separate generated set.

The rule matches main's PlayerOutline.shader: outline alpha = max(4-neighbour alpha) * (1 - alpha),
colour #221C18, one native texel at PPU32. Source bytes are read through main's deployment contract
and never rewritten; outputs mirror each source path under authoring/ink-outline-1px/runtime/.
"""
from pathlib import Path
import argparse, hashlib, io, json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = 'authoring/ink-outline-1px'; ASSET = 'authoring.ink-outline-1px'


def read(p): return json.loads(p.read_text(encoding='utf-8'))
def write(p, v):
    # Keep each index file's committed line endings; new files are LF.
    eol = '\r\n' if p.exists() and b'\r\n' in p.read_bytes()[:4096] else '\n'
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes((json.dumps(v, ensure_ascii=False, indent=2) + '\n').replace('\n', eol).encode('utf-8'))
def sha(b): return hashlib.sha256(b).hexdigest()


def outline(rgba, ink):
    im = rgba.astype(np.float64) / 255; a = im[..., 3]; p = np.pad(a, 1)
    o = np.maximum.reduce([p[1:-1, 2:], p[1:-1, :-2], p[2:, 1:-1], p[:-2, 1:-1]]) * (1 - a); alpha = a + o
    rgb = (im[..., :3] * a[..., None] + np.asarray(ink) / 255 * o[..., None]) / np.maximum(alpha, 1e-6)[..., None]
    out = np.dstack([np.where(alpha[..., None] > 0, rgb, 0), alpha])
    return (out * 255 + .5).clip(0, 255).astype(np.uint8)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, required=True, help="main docs/assets/manifests/unity-deployment.json")
    parser.add_argument('--preview', type=Path, nargs='*', default=[], help='optional review images copied to preview/')
    args = parser.parse_args()
    dest = ROOT / GROUP; selection = read(dest / 'source/selection.json'); ink = selection['rule']['inkRgb']
    picks = [f for f in read(args.manifest)['files'] if f['target'].endswith('.png')
             and any(f['target'] == t or (t.endswith('/') and f['target'].startswith(t)) for t in selection['includeTargets'])]
    mapping = []; clipped = []
    for f in sorted(picks, key=lambda x: x['target']):
        src = ROOT / f['source']['path']; raw = src.read_bytes()
        if sha(raw) != f['sha256']: raise SystemExit('source differs from main contract: ' + f['target'])
        rgba = np.asarray(Image.open(io.BytesIO(raw)).convert('RGBA'))
        a = rgba[..., 3] > 0
        if a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any(): clipped.append(f['target'])
        rel = 'runtime/' + f['source']['path']; out = dest / rel; out.parent.mkdir(parents=True, exist_ok=True)
        buf = io.BytesIO(); Image.fromarray(outline(rgba, ink)).save(buf, 'PNG', optimize=True); data = buf.getvalue()
        out.write_bytes(data)
        mapping.append({'target': f['target'], 'sourceAsset': f['source']['asset'], 'sourcePath': f['source']['path'],
                        'sourceSha256': f['sha256'], 'path': GROUP + '/' + rel, 'bytes': len(data), 'sha256': sha(data)})
    for p in args.preview:
        t = dest / 'preview' / p.name; t.parent.mkdir(parents=True, exist_ok=True); t.write_bytes(p.read_bytes())
    write(dest / 'source/outline-map.json', {'schema': 1, 'rule': selection['rule'], 'canvasEdgeClipped': clipped, 'files': mapping})
    files = sorted(p.relative_to(dest).as_posix() for p in dest.rglob('*') if p.is_file() and p.name != 'asset.json')
    write(dest / 'asset.json', {'format': 'deviant-resource-asset-v1', 'id': ASSET, 'path': GROUP, 'status': 'user-approved-rule',
                                'approval': selection['rule']['approval'], 'gameIntegration': 'Unity integration is verified independently',
                                'sources': ['Existing monster/NPC runtime frames selected by main unity-deployment.json; source bytes preserved'],
                                'files': files})
    catalog = read(ROOT / 'catalog.json')
    catalog['assets'] = [a for a in catalog['assets'] if a['id'] != ASSET] + [{'id': ASSET, 'path': GROUP, 'fileCount': len(files)}]
    write(ROOT / 'catalog.json', catalog)
    generated = read(ROOT / 'generated-assets.json')
    generated['files'] = [f for f in generated['files'] if not f['path'].startswith(GROUP + '/')]
    for name in files:
        data = (dest / name).read_bytes(); generated['files'].append({'path': GROUP + '/' + name, 'bytes': len(data), 'sha256': sha(data)})
    write(ROOT / 'generated-assets.json', generated)
    print(json.dumps({'asset': ASSET, 'frames': len(mapping), 'files': len(files), 'canvasEdgeClipped': len(clipped)}))


if __name__ == '__main__':
    main()
