"""Copy the selected native FX to Unity, keeping approved body motion sources intact."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
import numpy as np
from PIL import Image
from authoring_output import WORKBENCH, destination

SOURCE = WORKBENCH / 'MonsterReference/approved-effects'


def main():
    ROOT = destination(__doc__)
    DEST = ROOT / 'unity/Assets/Deviant/Stage1Monsters'
    (DEST / 'Data').mkdir(parents=True)
    source_bytes = (SOURCE / 'manifest.json').read_bytes()
    data = json.loads(source_bytes)
    output = dict(sourceSHA256=hashlib.sha256(source_bytes).hexdigest(), clips=[])
    for clip in data['clips']:
        entry = {k: v for k, v in clip.items() if k != 'frames'}
        entry['pivot'] = [clip['pivot'][0] / clip['width'], 1 - clip['pivot'][1] / clip['height']]
        entry['frames'] = []
        palette = {tuple(bytes.fromhex(c[1:])) for c in clip['palette']}
        colors = set()
        for i, frame in enumerate(clip['frames']):
            origin = SOURCE / frame
            pixels = np.array(Image.open(origin).convert('RGBA'))
            allowed_alpha = clip.get('opacity', [255] * clip['frameCount'])[i]
            assert set(np.unique(pixels[:, :, 3])).issubset({0, allowed_alpha}), frame
            colors |= set(map(tuple, pixels[pixels[:, :, 3] > 0][:, :3]))
            assert colors.issubset(palette), frame
            target = DEST / 'Art/Effects' / clip['id'] / f'{i:02}.png'
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(origin, target)
            entry['frames'].append(dict(path=target.relative_to(ROOT / 'unity').as_posix(),
                sha256=hashlib.sha256(origin.read_bytes()).hexdigest()))
        assert clip.get('role') == 'body' or len(colors) <= 3
        output['clips'].append(entry)
    (DEST / 'Data/effects-import.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'Exported {len(output["clips"])} clips / {sum(len(c["frames"]) for c in output["clips"])} native frames.')


if __name__ == '__main__':
    main()
