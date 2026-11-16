"""Export the user-approved ExpandingFan timing (B4) from the cleaned review frames into runtime/fan-final.

Frames are byte copies of preview/fan-candidates/clean/*.png (source cells and the six synthesised in-betweens)
in playback order; nothing is re-rendered.
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from clean_stage2_homing_d import components  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
REVIEW = GROUP / 'preview/fan-candidates'
OUT = GROUP / 'runtime/fan-final'
SELECTED = 'B4'
APPROVAL = {'userSelected': SELECTED, 'userRequest': 'B4로 확정', 'date': '2026-11-07',
            'scope': 'fan-aether-03 (prompt v4) source, matte/edge/foot cleanup, edge polish, hand x0.7 with wrist bridge on 25-31, '
                     'B2 storm re-sequencing, six synthesised clench in-betweens, approved crossbow, timing B4'}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    if (OUT / 'motion.json').exists() and json.loads((OUT / 'motion.json').read_text(encoding='utf-8')).get('approval'):
        raise SystemExit('fan-final is user-approved. Author further edits as a new version.')
    cands = json.loads((REVIEW / 'candidates.json').read_text(encoding='utf-8'))
    clean = json.loads((REVIEW / 'clean-all.json').read_text(encoding='utf-8'))
    tweens = json.loads((REVIEW / 'tweens.json').read_text(encoding='utf-8'))
    c = next(x for x in cands['candidates'] if x['id'] == SELECTED)
    by_file = {r['file']: r for r in clean['frames']}
    by_file.update({r['file']: r for r in tweens['frames']})
    (OUT / 'frames').mkdir(parents=True, exist_ok=True)
    records, images = [], []
    for n, (source, url, ms, role) in enumerate(zip(c['sourceFrames'], c['frameUrls'], c['durationsMs'], c['roles'])):
        src = REVIEW / url
        rec = by_file[url]
        assert sha(src) == rec['sha256'], url
        dst = OUT / f'frames/{n:02}.png'
        shutil.copy2(src, dst)
        im = Image.open(dst).convert('RGBA'); images.append(im)
        px = np.array(im)
        assert set(np.unique(px[:, :, 3])) <= {0, 255}
        records.append({'file': f'frames/{n:02}.png', 'sourceFrame': source, 'from': url, 'synthesised': source != int(source),
                        'role': role, 'durationMs': ms, 'bounds': list(im.getbbox()), 'sha256': sha(dst),
                        'crossbowPosition': rec['crossbowPosition']})
    cols = 8
    rows = (len(images) + cols - 1) // cols
    sheet = Image.new('RGBA', (384 * cols, 256 * rows))
    for n, im in enumerate(images):
        sheet.paste(im, (n % cols * 384, n // cols * 256))
    sheet.save(OUT / 'sheet.png')
    a = np.array(Image.open(GROUP / 'source/approved-fused-idle-original.png').convert('RGBA'))
    lab, sizes = components(a[:, :, 3] > 0)
    a[lab == sizes.argmax()] = 0; a[a[:, :, 3] == 0] = 0
    bow = Image.fromarray(a); assert list(bow.getbbox()) == list(clean['crossbow']['cropBounds'])
    bow.crop(bow.getbbox()).save(OUT / 'approved-crossbow.png')
    starts = [sum(c['durationsMs'][:i]) for i in range(len(c['durationsMs']))]
    manifest = {
        'format': 'stage2-fan-final-v1', 'status': 'user-approved-fan-B4-not-unity-integrated', 'pattern': 'ExpandingFan',
        'canvas': [384, 256], 'pivot': [192, 232], 'repeat': False, 'holdLastFrame': True,
        'totalMs': c['totalMs'], 'frameCount': len(records), 'sourceFrames': c['sourceFrames'], 'durationsMs': c['durationsMs'],
        'cues': {'spreadMs': starts[c['sourceFrames'].index(6)], 'clenchStartMs': starts[c['sourceFrames'].index(19)],
                 'releaseMs': starts[c['sourceFrames'].index(25)],
                 'game': 'FanTell 0.5 s field spawn; 2.5-3.0 s six arrows form; fire 3.0/3.5/3.6/4.1/4.2/4.3 s; the body holds the last frame until the pattern ends'},
        'timing': c['note'], 'frameIdNote': 'x.5 = synthesised in-between of source frames x and x+1',
        'sourceSheet': '../../source/fan-aether-03/aether-original.png', 'sourceSheetSha256': clean['sourceSheetSha256'],
        'prompt': '../../source/fan-aether/prompt.txt (v4)', 'cleanup': '../../preview/fan-candidates/clean-all.json',
        'tweens': '../../preview/fan-candidates/tweens.json', 'timingCandidates': '../../preview/fan-candidates/candidates.json',
        'inGamePreview': '../../preview/fan-sim/index.html',
        'transform': clean['transform'], 'matteProcessing': clean['matteProcessing'], 'face': clean['face'],
        'handShrink': clean['handShrink'], 'edgePolish': clean['edgePolish'], 'footCleanup': clean['footCleanup'],
        'crossbow': {**clean['crossbow'], 'file': 'approved-crossbow.png', 'approvedSource': '../../source/approved-fused-idle-original.png'},
        'identity': {'newLimbs': False, 'maskRedrawn': False, 'generativeRedraw': False,
                     'synthesisedFrames': '6 clench in-betweens from neighbouring cleaned frames only (no new drawing)'},
        'frames': records, 'approval': APPROVAL, 'unityIntegrated': False,
    }
    (OUT / 'motion.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(len(records), c['totalMs'], manifest['cues'])


if __name__ == '__main__':
    main()
