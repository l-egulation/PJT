"""Copy the clock boss ink warning FX from the HTML preview into Unity Resources."""
import hashlib
import json
import shutil
from pathlib import Path
import numpy as np
from PIL import Image
from authoring_output import ROOT as RESOURCE_ROOT, WORKBENCH, destination

SOURCE = WORKBENCH / 'MonsterReference/local-assets/hanging-clock-boss-battle-v1'
SOURCE_MANIFEST = RESOURCE_ROOT / 'monsters/stage-01/bosses/clock-executioner/source/battle-preview/asset-manifest.json'
CLIPS = ['needleInk', 'wallInk', 'clockRing']
PREVIEW_FLOOR = 464  # HTML floor line; Unity anchors every clip to the boss floor instead.


def main():
    ROOT = destination(__doc__)
    DEST = ROOT / 'unity/Assets/Deviant/Stage1Monsters/Resources/ClockExecutionerTelegraph'
    source_bytes = SOURCE_MANIFEST.read_bytes()
    manifest = json.loads(source_bytes)
    output = dict(sourceSHA256=hashlib.sha256(source_bytes).hexdigest(), ppu=32, clips=[])
    for clip_id in CLIPS:
        clip = manifest['fx'][clip_id]
        palette = {tuple(bytes.fromhex(c[1:])) for c in clip['palette']}
        target_dir = DEST / clip_id
        target_dir.mkdir(parents=True, exist_ok=True)
        frames, size = [], None
        for i, frame in enumerate(clip['frames']):
            origin = SOURCE / frame
            pixels = np.array(Image.open(origin).convert('RGBA'))
            assert set(np.unique(pixels[:, :, 3])).issubset({0, 255}), frame
            assert set(map(tuple, pixels[pixels[:, :, 3] > 0][:, :3])).issubset(palette), frame
            size = size or pixels.shape[1::-1]
            assert tuple(pixels.shape[1::-1]) == tuple(size), frame
            target = target_dir / f'{i:02}.png'
            shutil.copy2(origin, target)
            frames.append(dict(path=target.relative_to(ROOT / 'unity').as_posix(),
                               sha256=hashlib.sha256(origin.read_bytes()).hexdigest()))
        width, height = (int(v) for v in size)
        px, py = clip['pivot']
        # Ceiling clips are placed from the floor so drops land on Unity's floor line.
        if 'ceilingY' in clip:
            py = PREVIEW_FLOOR - clip['ceilingY']
        output['clips'].append(dict(id=clip_id, width=width, height=height, fps=clip['fps'],
                                    loopFrom=clip.get('loopFrom', -1), startAt=clip.get('startAt', 0),
                                    pivot=[px / width, 1 - py / height], palette=clip['palette'], frames=frames))
    (DEST / 'telegraphs.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'Exported {len(output["clips"])} clips / {sum(len(c["frames"]) for c in output["clips"])} native frames.')


if __name__ == '__main__':
    main()
