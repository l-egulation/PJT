"""Compose a review animation from deployed character bindings and original art."""
import argparse
import bisect
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--main-root', type=Path, required=True)
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    dest = args.destination.resolve()
    if dest.exists():
        raise SystemExit('Use a new review directory; existing output is preserved.')
    dest.mkdir(parents=True)
    main_root = args.main_root.resolve()
    root = main_root / 'resources'
    asset = Path(__file__).resolve().parents[1]
    manifest = json.loads((main_root / 'docs/assets/manifests/unity-deployment.json').read_text(encoding='utf-8'))
    by_target = {f['target']: f for f in manifest['files']}
    catalog = json.loads((main_root / 'unity/Assets/Deviant/Combat/Resources/CharacterV2/ApprovedWeaponCatalog.json').read_text(encoding='utf-8'))
    clips = {c['name']: c for c in next(s for s in catalog['sets'] if s['form'] == 'base_sword')['clips']}
    inputs = {}

    def load(path):
        data = path.read_bytes()
        inputs[path.relative_to(main_root).as_posix()] = hashlib.sha256(data).hexdigest()
        return Image.open(path).convert('RGBA')

    def load_clip(name):
        result = []
        for key in clips[name]['frames']:
            target = 'unity/Assets/Deviant/Combat/Resources/' + key + '.png'
            source = root / by_target[target]['source']['path']
            im = load(source)
            # Read each actual main-owned Unity foot pivot rather than the alpha bbox.
            import re
            meta = (main_root / (target + '.meta')).read_text(encoding='utf-8')
            px, py = map(float, re.search(r'spritePivot: \{x: ([\d.]+), y: ([\d.]+)\}', meta).groups())
            result.append((im, round(im.width * px), round(im.height * (1 - py))))
        return result

    walk, idle = load_clip('walk'), load_clip('idle')
    sheet = load(asset / 'runtime/footstep-electric-micro-spark.png')
    fx_meta = json.loads((asset / 'runtime/footstep-electric-micro-spark.json').read_text(encoding='utf-8'))
    fx = []
    for item in fx_meta['frames']:
        r = item['frame']
        fx.append(sheet.crop((r['x'], r['y'], r['x'] + r['w'], r['y'] + r['h'])))
    terrain = root / 'environments/forest/twilight/runtime'
    platform = load(terrain / 'platform.png')
    width, height, floor = 480, 240, 182
    bg = Image.new('RGBA', (width, height), '#151e31')
    # Reuse the game's scenery, with restrained contrast for a readable VFX review.
    for name, offset, opacity in [('far.png', 0, 0.5), ('front.png', 34, 0.25)]:
        layer = load(terrain / name)
        layer.putalpha(layer.getchannel('A').point(lambda a: int(a * opacity)))
        for x in range(-layer.width, width + layer.width, layer.width):
            bg.alpha_composite(layer, (x, offset))
    for x in range(0, width, platform.width):
        bg.alpha_composite(platform, (x, floor))
    draw = ImageDraw.Draw(bg)
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 12)
    small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 9)
    draw.rectangle((0, 0, width, 42), fill='#101725')
    draw.text((15, 8), '이동 후 바닥에 남는 전기 파티클', font=font, fill='#e7f5ff')
    draw.text((15, 27), '실제 캐릭터 아트 합성 · 장판 유지 1.2초는 미리보기용', font=small, fill='#91abc5')
    fps, count = 25, 130
    start, end, speed, x0 = 0.48, 3.84, 90, 70
    step_interval, patch_life = sum(clips['walk']['seconds']) / 2, 1.2
    patches = [(start + i * step_interval, x0 + i * step_interval * speed) for i in range(12)]
    frames = []
    peak = None
    for n in range(count):
        t = n / fps
        scene = bg.copy()
        px = round(x0 + max(0, min(t, end) - start) * speed)
        d = ImageDraw.Draw(scene)
        d.ellipse((px - 15, floor - 2, px + 15, floor + 4), fill='#151a2b')
        for j, (born, at) in enumerate(patches):
            age = t - born
            if not 0 <= age < patch_life:
                continue
            # The spark stays in world space; fresh sparks flicker within each patch.
            for k in range(3):
                cycle = (age - k * 0.07) % 0.46
                frame_index = min(7, int(cycle * 16))
                sprite = fx[frame_index]
                if (j + k) % 2:
                    sprite = ImageOps.mirror(sprite)
                sprite = sprite.resize((32, 20), Image.Resampling.NEAREST)
                fade = min(1, (patch_life - age) / 0.25)
                if fade < 1:
                    sprite.putalpha(sprite.getchannel('A').point(lambda a: int(a * fade)))
                scene.alpha_composite(sprite, (round(at) - 16 + (k - 1) * 9, floor - 11 + k % 2 * 2))
        moving = start <= t < end
        name, images = ('walk', walk) if moving else ('idle', idle)
        local = t - start if moving else t
        elapsed = local % sum(clips[name]['seconds'])
        bounds, total = [], 0
        for seconds in clips[name]['seconds']:
            total += seconds
            bounds.append(total)
        character, pivot_x, pivot_y = images[min(bisect.bisect_right(bounds, elapsed), len(images) - 1)]
        scene.alpha_composite(character, (px - pivot_x, floor - pivot_y))
        enlarged = scene.convert('RGB').resize((width * 2, height * 2), Image.Resampling.NEAREST)
        frames.append(enlarged)
        if n == 68:
            peak = enlarged
    frames[0].save(dest / 'walking-electric-trail.gif', save_all=True, append_images=frames[1:], duration=40, loop=0, optimize=False)
    peak.save(dest / 'walking-electric-trail.png')
    settings = {'kind': 'art-composite-preview', 'unityCapture': False, 'character': 'base_sword',
                'characterTiming': clips['walk']['seconds'], 'pixelScale': 2, 'fps': fps, 'frames': count,
                'previewOnly': {'movementPixelsPerSecond': speed, 'patchLifetimeSeconds': patch_life,
                                'patchIntervalSeconds': step_interval, 'sparksPerPatch': 3},
                'groundAnchored': True, 'gameIntegration': 'not-integrated', 'inputs': inputs}
    with (dest / 'settings.json').open('w', encoding='utf-8', newline='\n') as stream:
        stream.write(json.dumps(settings, ensure_ascii=False, indent=2) + '\n')
    print(f'Rendered {count} frames; {width * 2}x{height * 2}; original game walk timings and Unity foot pivots preserved.')


if __name__ == '__main__':
    main()
