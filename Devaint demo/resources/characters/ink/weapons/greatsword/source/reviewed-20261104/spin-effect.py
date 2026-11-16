from bisect import bisect_right
from itertools import accumulate
from math import cos, pi, sin
from pathlib import Path
import json
import subprocess

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageSequence

OUT = Path(__file__).resolve().parent
SPIN = OUT.parent / 'greatsword-spin-loop-20261104'
SIZE = (512, 220)
STEP = 20
START = 200
END = 2200
TOTAL = 2500
WINDUP = [(0, 1), (60, 4), (120, 6), (160, 7)]
RECOVERY = [(2200, 22), (2240, 6), (2300, 4), (2380, 1)]
# Phases track the apparent blade tip, including when it passes behind the hood.
TIP_PHASE = (47, 22, 0, -37, -78, -96, -132, -163, -197, -252, -313)


def source_layers(pose):
    with Image.open(SPIN / 'spin-beltless-v4' / 'frames' / f'F{pose:03}.png') as src:
        rgb = np.array(src.convert('RGB'))
    high = rgb.max(axis=2)
    low = rgb.min(axis=2)
    # The original gray slash is baked into the cel. Tint its neutral core
    # separately while keeping colored clothing, sword and face pixels intact.
    slash = (high - low <= 9) & (low >= 66) & (high <= 192)
    if pose in (1, 4):
        slash[:] = False
    white = (low >= 244) & (high - low <= 13)
    rgba = np.concatenate((rgb, np.where(white | slash, 0, 255).astype('uint8')[..., None]), axis=2)
    return Image.fromarray(rgba), Image.fromarray(slash.astype('uint8') * 255)


def baked_glint(canvas, mask, strength):
    if not mask.getbbox():
        return
    field = np.array(mask)
    outer = np.array(mask.filter(ImageFilter.MaxFilter(5)))
    inner = np.array(mask.filter(ImageFilter.MinFilter(5)))
    for pixels, color, alpha in (
        ((outer > 0) & (field == 0), '#D78052', 150),
        (field > 0, '#432C2B', 195),
        ((field > 0) & (inner == 0), '#E9B763', 232),
    ):
        canvas.paste(color, (0, 0, *SIZE), Image.fromarray((pixels * int(alpha * strength)).astype('uint8')))


def ribbons(angle, power):
    rear, front = (Image.new('RGBA', SIZE) for _ in range(2))
    draws = [ImageDraw.Draw(rear), ImageDraw.Draw(front)]

    def band(head, length, rx, ry, offset, width, color, opacity):
        for i in range(50):
            ends = []
            for part in (i / 50, (i + 1) / 50):
                theta = (head + length * part) * pi / 180
                taper = max(0.12, sin(pi * (part * 0.91 + 0.06)) ** 0.65)
                x = 241 + rx * cos(theta)
                y = 135 + offset + ry * sin(theta) - 0.09 * (x - 241)
                half = width * taper / 2
                ends.append((theta, (round(x), round(y-half)), (round(x), round(y+half))))
            theta = (ends[0][0] + ends[1][0]) / 2
            draws[sin(theta) >= 0].polygon(
                [ends[0][1], ends[1][1], ends[1][2], ends[0][2]],
                fill=(*color, round(opacity * power))
            )

    band(angle + 17, 144, 226, 42, 9, 13, (44, 24, 24), 216)
    band(angle + 11, 156, 229, 45, 13, 5, (176, 52, 37), 246)
    band(angle + 8, 121, 218, 39, 9, 3, (255, 214, 136), 250)
    band(angle + 158, 94, 215, 39, -7, 7, (127, 81, 51), 190)
    band(angle + 151, 105, 220, 42, -10, 3, (245, 180, 98), 242)
    band(angle + 87, 62, 200, 29, 24, 3, (236, 112, 67), 160)
    return rear, front


def dust(canvas, elapsed, turn):
    if not 0 <= elapsed < 150:
        return
    draw = ImageDraw.Draw(canvas)
    progress = elapsed / 150
    for i, (x, dx, height) in enumerate(((85,-39,18),(128,-27,13),(348,37,14),(396,42,25),(424,29,13),(104,-31,9))):
        x += dx * progress + ((turn + i) % 3 - 1) * 5
        y = 196 - height * 4 * progress * (1 - progress)
        length = 2 + (i % 3)
        draw.polygon([(x-length, y+1), (x+1, y-2), (x+length, y), (x+1, y+2)],
                     fill=(134, 89, 59, round(200 * (1-progress))))


def render():
    repo = OUT.parents[1]
    branch = lambda: subprocess.check_output(['git', '-C', str(repo), 'branch', '--show-current'], text=True).strip()
    assert branch() == 'feat/story-unity-essentials'
    timing = json.loads((SPIN / 'spin-smooth-v2' / 'animation.json').read_text('utf-8'))
    loop = timing['loop']
    stops = [0, *accumulate(f['durationMs'] for f in loop)]
    assert stops[-1] == 500 and len(stops) == len(TIP_PHASE)
    poses = {n for _, n in WINDUP + RECOVERY} | {f['sourceFrame'] for f in loop}
    layers = {pose: source_layers(pose) for pose in poses}
    frames = []
    for time in range(0, TOTAL, STEP):
        elapsed = time - START
        if START <= time < END:
            turn, local = divmod(elapsed, stops[-1])
            index = bisect_right(stops, local) - 1
            pose = loop[index]['sourceFrame']
            mix = (local - stops[index]) / (stops[index + 1] - stops[index])
            angle = TIP_PHASE[index] + mix * (TIP_PHASE[index + 1] - TIP_PHASE[index])
            pulse = 1 - min(local / 120, 1)
            power = 0.78 + 0.22 * pulse
        else:
            poses_by_time = WINDUP if time < START else RECOVERY
            pose = next(n for at, n in reversed(poses_by_time) if time >= at)
            angle = 47 - 0.82 * (elapsed if time < START else time - END)
            power = (0.62 * max(0, 1 - (START - time) / 60) if time < START
                     else 0.68 * max(0, 1 - (time - END) / 140))
            turn, local = 0, -1

        body, slash = layers[pose]
        cel = Image.new('RGBA', SIZE, '#F7F4ED')
        ImageDraw.Draw(cel).ellipse((155, 188, 330, 207), fill='#E9E0D3')
        baked_glint(cel, slash, max(power, 0.55) if pose in (6, 7, 22) else power)
        rear, front = ribbons(angle, power)
        cel.alpha_composite(rear)
        cel.alpha_composite(body)
        cel.alpha_composite(front)
        particles = Image.new('RGBA', SIZE)
        dust(particles, local, turn)
        cel.alpha_composite(particles)
        frame = Image.new('RGB', (850, 228), '#F7F4ED')
        ImageDraw.Draw(frame).line((0, 207, 850, 207), fill='#E9E0D3', width=1)
        progress = max(0, min(1, elapsed / (END - START)))
        frame.paste(cel, (12 + round(300 * progress), 0))
        frames.append(frame)
    assert branch() == 'feat/story-unity-essentials'
    preview = OUT / 'movement-skill-vfx.gif'
    frames[0].save(preview, save_all=True, append_images=frames[1:], duration=STEP, loop=0, disposal=2)
    with Image.open(preview) as gif:
        assert sum(frame.info['duration'] for frame in ImageSequence.Iterator(gif)) == TOTAL
    review = Image.new('RGB', (660, 456), '#F7F4ED')
    for index, time in enumerate((200, 260, 320, 380, 440, 520)):
        x = 12 + round(300 * (time - START) / (END - START))
        cell = frames[time // STEP].crop((x, 0, x+512, 220))
        cell = cell.resize((330, 142), Image.Resampling.NEAREST)
        review.paste(cell, (330 * (index % 2), 152 * (index // 2)))
    review.save(OUT / 'movement-skill-vfx-check.png')


if __name__ == '__main__':
    render()
