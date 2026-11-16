"""Register the user-approved Rose/Blue boss body motions (v8) and idle breaths in the main-owned deployment contract.

Sources (resources):
  monsters/stage-02/boss-motions-v8-20261108/runtime/<id>/sheet.png + motion.json  (8-column grid, cells 384x256)
  monsters/stage-02/boss-idle-breath-20261107/runtime/<id>/sheet.png + motion.json (one row of 6 cells)
Every cell is 384x256 with the feet pivot at (192,232) and is drawn at 32 px per unit (original size).
Writes unity/Assets/Chapter2/Resources/RoseBlueMotions/<id>.png entries (+ deterministic .meta) into
docs/assets/manifests/unity-deployment.json and the binding unity/Assets/Chapter2/Resources/RoseBlueMotions.json.
Run tools/assets/update_assets.py afterwards to copy the textures.
"""
from pathlib import Path
import hashlib
import json
import uuid

ROOT = Path(__file__).resolve().parents[2]
V8 = ('monsters.stage-02.boss-motions-v8-20261108', 'monsters/stage-02/boss-motions-v8-20261108')
BREATH = ('monsters.stage-02.boss-idle-breath-20261107', 'monsters/stage-02/boss-idle-breath-20261107')
PREFIX = 'unity/Assets/Chapter2/Resources/RoseBlueMotions/'
BINDING = 'unity/Assets/Chapter2/Resources/RoseBlueMotions.json'
CELL, PIVOT = (384, 256), (192, 232)
# Motions used by the encounter's combat phases.
USED = ['rose-triple-shot', 'blue-ink-burst',
        'rose-stun-shot', 'blue-ink-throw', 'rose-sky-volley', 'blue-ink-whirl', 'rose-stun', 'blue-stun',
        'p3-homing', 'p3-bounce', 'p3-rain', 'p3-fan', 'p3-hover', 'p3-stun', 'p3-defeat']
# Cutscene motions (data/combat/chapter2_rose_blue_intro.json, chapter2_rose_blue_phase_shift.json rosePlan/bluePlan): binding id -> v8 runtime folder.
CUTSCENE = {'R1': 'R1-rose-smug-laugh', 'R2': 'R2-rose-boast-aim', 'R3': 'R3-rose-aim-release', 'R6': 'R6-rose-panic-reload',
            'B3': 'B3-blue-scene-cast', 'R4': 'R4-rose-surprise-point', 'R7': 'R7-rose-battle-ready'}
BREATHS = {'rose-breath': 'rose-idle-breath', 'blue-breath': 'blue-idle-breath', 'fused-breath': 'fused-idle-breath'}
# Phase1 LayerShots fires at 0.7/1.7/2.7 s; the triple shot clip is stretched to that span (no frame-exact keys yet).
EXTRA_KEYS = {'rose-triple-shot': [[0, 0], [3.15, 2.52]]}
# Phase2 stun arrow + ink fireball: the tell is StunArrowTell (1 s). The first pose holds while Rose's aim tracks,
# then the approved 0.42 s draw plays at its own speed so the release still lands on the spawn cue.
# These override the source gameCueKeys (made for the earlier 0.5 s tell).
OVERRIDE_KEYS = {mid: [[0, 0], [0.58, 0], [1, 0.42], [3.1, 2.52]] for mid in ('rose-stun-shot', 'blue-ink-throw')}


def guid(target):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'deviant-roseblue-motions/' + target).hex


def texture_meta(target):
    return f'''fileFormatVersion: 2
guid: {guid(target)}
TextureImporter:
  serializedVersion: 13
  externalObjects: {{}}
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    wrapU: 1
    wrapV: 1
  maxTextureSize: 4096
  textureCompression: 0
  nPOTScale: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    overridden: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    overridden: 1
  spriteMode: 1
  spriteExtrude: 0
  spriteMeshType: 1
  spritePixelsToUnits: 32
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  userData:
  assetBundleName:
  assetBundleVariant:
'''


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8', newline='\n')


def main():
    res = ROOT / 'resources'
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    entries, motions = [], []
    targets = []

    def add(mid, asset, group, folder, cols, cells, sequence, durations, keys, loop):
        raw = (res / group / 'runtime' / folder / 'sheet.png').read_bytes()
        target = PREFIX + mid + '.png'
        targets.append(target)
        entries.append({'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(),
                        'source': {'asset': asset, 'path': f'{group}/runtime/{folder}/sheet.png'}})
        if not (ROOT / (target + '.meta')).exists():  # Unity 가 다시 쓴 .meta(같은 guid)는 그대로 둔다
            write(ROOT / (target + '.meta'), texture_meta(target))
        m = {'id': mid, 'sheet': mid, 'columns': cols, 'cells': cells, 'sequence': sequence, 'durationsMs': durations,
             'totalMs': sum(durations), 'loop': loop, 'cueKeys': [v for pair in keys for v in pair] if keys else []}
        motions.append(m)

    for mid in USED:
        data = json.loads((res / V8[1] / 'runtime' / mid / 'motion.json').read_text(encoding='utf-8'))
        assert data['canvas'] == list(CELL) and data['pivot'] == list(PIVOT), mid
        assert 'user-approved' in data['status'], mid
        index = {f: i for i, f in enumerate(data['frames'])}
        keys = OVERRIDE_KEYS.get(mid) or data.get('gameCueKeys') or EXTRA_KEYS.get(mid)
        add(mid, *V8, mid, data['sheet']['columns'], len(data['frames']), [index[f] for f in data['sequence']], data['durationsMs'], keys, data['loop'])
    for mid, folder in CUTSCENE.items():
        data = json.loads((res / V8[1] / 'runtime' / folder / 'motion.json').read_text(encoding='utf-8'))
        assert data['canvas'] == list(CELL) and data['pivot'] == list(PIVOT), folder
        assert 'user-approved' in data['status'], folder
        index = {f: i for i, f in enumerate(data['frames'])}
        add(mid, *V8, folder, data['sheet']['columns'], len(data['frames']), [index[f] for f in data['sequence']], data['durationsMs'], None, data['loop'])
    for mid, folder in BREATHS.items():
        data = json.loads((res / BREATH[1] / 'runtime' / folder / 'motion.json').read_text(encoding='utf-8'))
        assert data['canvas'] == list(CELL) and data['pivot'] == list(PIVOT), folder
        frames = data['frames']
        add(mid, *BREATH, folder, len(frames), len(frames), list(range(len(frames))), [f['durationMs'] for f in frames], None, True)

    # Keep everyone else's order; replace only this prefix in place (or append after the phase1 art once).
    kept = [x for x in manifest['files'] if not x['target'].startswith(PREFIX)]
    at = next((i for i, x in enumerate(manifest['files']) if x['target'].startswith(PREFIX)), None)
    if at is None:
        at = max((i + 1 for i, x in enumerate(kept) if x['target'].startswith('unity/Assets/Chapter2/Resources/RoseBluePhase1/')), default=len(kept))
    manifest['files'] = kept[:at] + entries + kept[at:]
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')

    binding = {'format': 'deviant-roseblue-motions-v1', 'cellWidthPx': CELL[0], 'cellHeightPx': CELL[1], 'pivotPx': list(PIVOT),
               'pixelsPerUnit': 32, 'motions': motions}
    write(ROOT / BINDING, json.dumps(binding, ensure_ascii=False, indent=2) + '\n')
    for path, importer in ((BINDING + '.meta', 'TextScriptImporter'), (PREFIX.rstrip('/') + '.meta', None)):
        meta = ROOT / path
        if meta.exists():
            continue
        if importer:
            write(meta, f'fileFormatVersion: 2\nguid: {guid(path)}\n{importer}:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
        else:
            write(meta, f'fileFormatVersion: 2\nguid: {guid(path)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    # Drop .meta files of textures this run no longer registers.
    for meta in (ROOT / PREFIX).glob('*.png.meta'):
        if PREFIX + meta.name[:-5] not in targets:
            meta.unlink()
    print(f'Registered {len(entries)} Rose/Blue motion sheets ({len(USED)} body motions, {len(CUTSCENE)} cutscene motions, {len(BREATHS)} idle breaths)')


if __name__ == '__main__':
    main()
