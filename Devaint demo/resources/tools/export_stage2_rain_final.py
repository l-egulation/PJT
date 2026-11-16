"""Export the user-approved ArrowRain timing (A-x2) from the cleaned review frames into runtime/rain-a-x2.

Frames are byte copies of preview/rain-candidates/clean/NN.png in playback order (no re-render).
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
REVIEW = GROUP / 'preview/rain-candidates'
OUT = GROUP / 'runtime/rain-a-x2'
SELECTED = 'A-x2'
APPROVAL = {'userSelected': SELECTED, 'userRequest': 'A-x2. A · 앞부분 2배속 으로 가자', 'date': '2026-11-07',
            'scope': 'rain-aether-01 source, mouth removed with one canonical mask, matte/edge/foot cleanup, approved crossbow, timing A-x2'}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save_bow_crop(path, bounds):
    import numpy as np
    from clean_stage2_homing_d import components
    a = np.array(Image.open(GROUP / 'source/approved-fused-idle-original.png').convert('RGBA'))
    labels, sizes = components(a[:, :, 3] > 0)
    a[labels == sizes.argmax()] = 0
    a[a[:, :, 3] == 0] = 0
    bow = Image.fromarray(a)
    assert list(bow.getbbox()) == list(bounds)
    bow.crop(tuple(bounds)).save(path)


def main():
    if (OUT / 'motion.json').exists() and json.loads((OUT / 'motion.json').read_text(encoding='utf-8')).get('approval'):
        raise SystemExit('rain-a-x2 is user-approved. Author further edits as a new version.')
    cands = json.loads((REVIEW / 'candidates.json').read_text(encoding='utf-8'))
    clean = json.loads((REVIEW / 'clean-all.json').read_text(encoding='utf-8'))
    c = next(x for x in cands['candidates'] if x['id'] == SELECTED)
    by_source = {r['sourceFrame']: r for r in clean['frames']}
    (OUT / 'frames').mkdir(parents=True, exist_ok=True)
    records, images = [], []
    for n, (source, ms) in enumerate(zip(c['sourceFrames'], c['durationsMs'])):
        src = REVIEW / f'clean/{source:02}.png'
        assert sha(src) == by_source[source]['sha256'], source
        dst = OUT / f'frames/{n:02}.png'
        shutil.copy2(src, dst)
        assert sha(dst) == sha(src)
        im = Image.open(dst).convert('RGBA'); images.append(im)
        r = by_source[source]
        records.append({'file': f'frames/{n:02}.png', 'sourceFrame': source, 'role': c['roles'][n], 'durationMs': ms, 'bounds': list(im.getbbox()),
                        'sha256': sha(dst), 'sourceCellSha256': r['sourceCellSha256'], 'crossbowPosition': r['crossbowPosition'],
                        'face': r['face']})
    sheet = Image.new('RGBA', (384 * 6, 256 * 6))
    for n, im in enumerate(images):
        sheet.paste(im, (n % 6 * 384, n // 6 * 256))
    sheet.save(OUT / 'sheet.png')
    save_bow_crop(OUT / 'approved-crossbow.png', clean['crossbow']['cropBounds'])
    starts = [sum(c['durationsMs'][:i]) for i in range(len(c['durationsMs']))]
    manifest = {
        'format': 'stage2-rain-a-x2-v1', 'status': 'user-approved-rain-A-x2-not-unity-integrated', 'pattern': 'ArrowRain',
        'canvas': [384, 256], 'pivot': [192, 232], 'repeat': False, 'holdLastFrame': True,
        'totalMs': c['totalMs'], 'sourceFrames': c['sourceFrames'], 'durationsMs': c['durationsMs'],
        'cues': {'summonRaisedMs': starts[c['sourceFrames'].index(5)], 'detonateMs': starts[c['sourceFrames'].index(28)],
                 'game': 'RainTell 0.5 s: first arrow 0.5 s, landings 0.75-2.25 s, explosions 2.45-3.95 s; the body holds the last frame until the pattern ends'},
        'timing': c['note'],
        'sourceSheet': '../../source/rain-aether-01/aether-original.png', 'sourceSheetSha256': clean['sourceSheetSha256'],
        'prompt': '../../source/rain-aether/prompt.txt', 'cleanup': '../../preview/rain-candidates/clean-all.json',
        'timingCandidates': '../../preview/rain-candidates/candidates.json', 'inGamePreview': '../../preview/rain-sim/index.html',
        'transform': clean['transform'], 'matteProcessing': clean['matteProcessing'], 'face': clean['face'],
        'crossbow': {**clean['crossbow'], 'file': 'approved-crossbow.png', 'approvedSource': '../../source/approved-fused-idle-original.png'},
        'footCleanup': clean['footCleanup'], 'edgeRefinement': clean['edgeRefinement'],
        'identity': {'newLimbs': False, 'maskRedrawn': 'mouth row only, one canonical mask pasted at hood registration', 'generativeRedraw': False},
        'frames': records, 'approval': APPROVAL, 'unityIntegrated': False,
    }
    (OUT / 'motion.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(len(records), c['totalMs'], manifest['cues'])


if __name__ == '__main__':
    main()
