"""Native-pixel attack FX and an unapproved elite burrow/eruption review.

Only writes local-assets/monster-attack-fx-v1. Approved sprites and Unity are read-only.
"""
from pathlib import Path
import json
import math
import hashlib
import shutil
import zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'local-assets/monster-attack-fx-v1'
APPROVED = ROOT / 'approved'
COLORS = ['#171716', '#41413D', '#575451', '#817D77', '#9C968D', '#D6D0C7']
RGB = [tuple(bytes.fromhex(c[1:])) + (255,) for c in COLORS]
manifest = json.loads((APPROVED / 'manifest.json').read_text(encoding='utf-8'))
monsters = {m['id']: m for m in manifest['monsters']}
OUT.mkdir(parents=True, exist_ok=True)


def blank(size=(192, 144)):
    return Image.new('RGBA', size)


def polygon(draw, points, color):
    draw.polygon([(round(x), round(y)) for x, y in points], fill=color)


def crescent(draw, center, radii, a, b, width, color):
    """Filled tapered ribbon on an ellipse, rasterized directly at native resolution."""
    cx, cy = center
    rx, ry = radii
    n = 34
    outer, inner = [], []
    for i in range(n + 1):
        u = i / n
        angle = math.radians(a + (b - a) * u)
        w = width * math.sin(math.pi * u) ** .8
        outer.append((cx + rx * math.cos(angle), cy + ry * math.sin(angle)))
        inner.append((cx + (rx - w) * math.cos(angle), cy + (ry - w) * math.sin(angle)))
    polygon(draw, outer + inner[::-1], color)


def slash_frame(i):
    im = blank()
    d = ImageDraw.Draw(im)
    # Pivot (112,128); forward is left. Main cut occupies the 58x48px live box.
    p = i / 9
    span = min(172, 45 + i * 34)
    end = 112 + p * 125
    thickness = [2, 5, 8, 7, 5, 4, 3, 2, 1, 1][i]
    for delta, tone in [(0, 3), (12, 4), (22, 5)]:
        crescent(d, (111, 103), (58 - delta * .12, 25 - delta * .1),
                 end - span, end, thickness * (.75 if tone == 5 else 1), RGB[tone])
    if i in range(2, 7):
        crescent(d, (106, 107), (49, 19), 124 + i * 5, 273 + i * 5,
                 max(1, 5 - abs(i - 3)), RGB[4])
    if i >= 5:
        # Detached tapered remnants, not random single-pixel noise.
        for j in range(3):
            x, y = 57 - (i - 5) * 3 + j * 11, 93 + j * 12
            polygon(d, [(x, y), (x + 7 - (i - 5), y - 2), (x + 4, y + 1)], RGB[4])
    return im


PUFF = [
    '....333....', '..3333333..', '.333334443.', '33333444443',
    '33334444444', '33334444444', '.333444444.', '..3334444..', '....333....',
]


def puff(size, flip=False):
    im = blank((11, 9))
    for y, row in enumerate(PUFF):
        for x, c in enumerate(row):
            if c != '.':
                im.putpixel((x, y), RGB[int(c) - 1])  # dust tones 2/3
    if flip:
        im = im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    return im.resize(size, Image.Resampling.NEAREST)


def dust_frame(i, elite=False):
    im = blank()
    d = ImageDraw.Draw(im)
    u = i / 13
    # Contact-directed fan -> rolling puffs -> broken curling wisps.
    if i < 5:
        reach = 20 + i * 12
        height = 6 + i * 3
        for j in range(3):
            polygon(d, [(111, 128 - j * 2), (111 - reach, 126 - height - j * 4),
                        (118 - reach + j * 5, 126 - height - j * 3), (94, 126)], RGB[3 if j % 2 else 4])
    for j, delay in enumerate([0, 1, 2, 4, 5]):
        age = i - delay
        if age < 0 or age > 9:
            continue
        x = 99 - j * 10 - age * (2 if elite else 3)
        y = 127 - min(age * 2, 13) - (j % 2) * 6
        radius = max(2, round((20 if elite else 18) * math.sin((age + 2) / 12 * math.pi)))
        if age < 6:
            cloud = puff((radius * 2, radius + 10), j % 2 == 0)
            im.alpha_composite(cloud, (x - radius, y - radius - 2))
            # Broad lit interior, with no dark contour around a puff.
            polygon(ImageDraw.Draw(im), [(x-radius//2,y-radius+1),(x-3,y-radius-2),
                     (x+3,y-radius-1),(x+4,y-radius+2),(x-5,y-radius+4)],RGB[4])
        else:
            # A curved fragment grows thinner as it drifts; no opacity fade.
            crescent(d, (x, y - 8), (radius + 3, radius), 75, 276,
                     max(1, 3 - (age - 6)), RGB[3])
    return im


def broom_cloud_frame(i):
    """A connected rolling cloud spanning the 83x38px attack region."""
    im = blank()
    mask = Image.new('L', im.size)
    md = ImageDraw.Draw(mask)
    # Keep expanding after the initial billow. Never shrink toward the broom.
    growth = [.25, .55, .83, 1, 1, 1.02, 1.04, 1.06, 1.08, 1.10, 1.12, 1.14, 1.16, 1.18, 1.20, 1.22][i]
    opacity = [255,255,255,255,255,245,225,200,170,140,110,80,54,32,16,0][i]
    drift = max(0, i - 5)
    # Front lobe, main billow, rear billow, and floor bridge. Consistent silhouette.
    lobes = [(46, 118, 12, 10), (62, 109, 19, 17), (83, 110, 20, 18),
             (102, 119, 14, 10), (75, 123, 35, 5)]
    for j, (x, y, rx, ry) in enumerate(lobes):
        x = 108 + (x - 108) * min(1, growth * 1.4) - drift * 1.4
        y -= drift * (1.2 + (j % 2) * .3)
        rx = max(1, round(rx * growth)); ry = max(1, round(ry * growth))
        x, y = round(x), round(y)
        md.ellipse((x-rx, y-ry, x+rx, min(127,y+ry)), fill=255)
        md.ellipse((x-rx+2,y-ry-2,x-rx+max(6,rx),y-ry+max(5,ry)),fill=255)
    # Uniform FX opacity fades broad pixel shapes without eroding their edges.
    im.paste(RGB[3], (0,0), mask)
    shade = Image.new('RGBA', im.size)
    sd = ImageDraw.Draw(shade)
    for j, (x, y, rx, ry) in enumerate(lobes[:4]):
        x = 108 + (x-108)*min(1,growth*1.4)-drift*1.4
        y -= drift*(1.2+(j%2)*.3)
        rx,ry=rx*growth,ry*growth
        # Open shading patches, never an outer contour.
        polygon(sd,[(x-rx*.8,y+ry*.35),(x-rx*.4,y+ry*.65),(x+rx*.55,y+ry*.55),
                    (x+rx*.9,y+ry*.2),(x+rx*.7,y+ry*.9),(x-rx*.5,y+ry)],RGB[2])
        crescent(sd,(x-2,y-2),(max(2,rx*.65),max(2,ry*.65)),178,310,max(1,3*growth),RGB[4])
    shade.putalpha(Image.fromarray(np.minimum(np.asarray(shade.getchannel('A')), np.asarray(mask))))
    im.alpha_composite(shade)
    im.putalpha(im.getchannel('A').point(lambda a: opacity if a else 0))
    return im


def ellipse(d, box, tone):
    d.ellipse(tuple(round(v) for v in box), fill=RGB[tone])


def pool(im, amount, phase=0):
    d = ImageDraw.Draw(im)
    cx, gy = 96, 144
    width = round(26 + amount * 42)
    ellipse(d, (cx - width, gy - 5, cx + width, gy + 1), 0)
    ellipse(d, (cx - width + 5, gy - 4, cx + width - 12, gy - 1), 1)
    if amount > .5:
        d.line((cx - width + 12, gy - 3, cx - 8, gy - 3), fill=RGB[2], width=1)


base_motion = monsters['04_well-warden']['motions']['base']
base = Image.open(APPROVED / base_motion['frames'][0]).convert('RGBA')
BASE = np.array(base)
BODY = np.zeros((176, 192, 4), dtype=np.uint8)
BODY[24:152, 32:160] = BASE


def melt_frame(k, n=14):
    u = k / (n - 1)
    im = blank((192, 176))
    if k == 0:
        im.alpha_composite(base, (32, 24))
        return im
    dst = np.zeros_like(BODY)
    # Cohesive, smooth lobes drain at different rates; rows are not scaled flat.
    for x in range(192):
        sx = round(96 + (x - 96) / (1 + .07 * u))
        lobe = .8 + .18 * math.sin((sx - 42) / 13) + .13 * math.cos(sx / 8)
        shift = round(130 * u ** 1.8 * lobe)
        for y in range(144):
            sy = y - shift
            if sx < 0 or sx >= 192 or sy < 0 or not BODY[sy, sx, 3]:
                continue
            dst[y, x] = BODY[sy, sx]
    # Metal landmarks gradually become ink before disappearing.
    if u > .25:
        for x in range(192):
            rows = np.where(dst[:, x, 3] > 0)[0]
            if rows.size:
                wave = 146 - 100 * u + 6 * math.sin(x / 12 + u * 2)
                # Ink consumes material from the ground upward, retaining broad form.
                for y in rows:
                    if y > wave:
                        dst[y, x] = RGB[0 if y > wave + 10 else 1]
                low = max(rows[0], round(wave + 8))
                if low < 144 and u > .5:
                    dst[low:144, x] = RGB[0]
    im = Image.fromarray(dst)
    pool(im, u)
    d = ImageDraw.Draw(im)
    for x in [67, 92, 118]:
        top = round(118 + 25 * u + 4 * math.sin(x))
        if u > .35 and top < 144:
            ellipse(d, (x - 3, top, x + 3, 146), 0)
    return im


def bubble_frame(k):
    im = blank((192, 176))
    d = ImageDraw.Draw(im)
    pool(im, .55)
    for j, x in enumerate([65, 91, 117, 131]):
        phase = (k + j * 2) % 8
        r = [1, 2, 3, 4, 5, 5, 4, 2][phase]
        y = 141 - (phase // 2)
        ellipse(d, (x - r, y - r, x + r, y + r // 2), 1)
        if r >= 3:
            d.line((x - 2, y - r + 1, x, y - r + 1), fill=RGB[2], width=1)
    if k >= 5:
        for x in [77, 108]:
            polygon(d, [(x - 3, 142), (x, 133 - (k % 2)), (x + 3, 142)], RGB[0])
    return im


def eruption_fx(k):
    im = blank((192, 176))
    d = ImageDraw.Draw(im)
    u = k / 13
    pool(im, .8)
    if k < 6:
        height = [35, 78, 98, 82, 58, 35][k]
        for j, (x, factor) in enumerate([(96, 1), (70, .67), (120, .8), (52, .42), (141, .45)]):
            h = round(height * factor)
            w = 11 if j == 0 else 8
            # Curved liquid jets with rounded heads, avoiding triangular spikes.
            left, right = [], []
            lean = (j - 2) * 3
            for q in range(25):
                v = q / 24
                cx = x + lean * v * v
                yy = 144 - h * v
                radius = 2 + w * (1-v)**3 + 2*math.sin(v*math.pi*1.3)**2
                left.append((cx-radius,yy));right.append((cx+radius,yy))
            polygon(d,left+right[::-1],RGB[0])
            ellipse(d,(x+lean-3,141-h,x+lean+3,148-h),0)
            crescent(d,(x+lean/2-1,144-h/2),(max(2,w/2),max(3,h/2-5)),
                     110,245,2,RGB[1])
    for j in range(10):
        a = math.radians(18 + j * 16)
        travel = 12 + k * 4
        x = 96 + math.cos(a) * travel
        y = 137 - math.sin(a) * travel * 1.5 + k * k * .4
        r = max(1, 3 - k // 5)
        if y < 143:
            ellipse(d, (x - r, y - r - 1, x + r, y + r), 0 if j % 3 else 1)
    return im


def emerge_body(k):
    im = blank((192, 176))
    u = min(1, k / 9)
    # Re-form upright from the floor, full body scale; no whole-body stretch.
    lift = round((1 - u) ** 1.6 * 98)
    a = np.array(BODY)
    a[:, :, 3] = 0
    for y in range(145 - lift):
        a[y + lift] = BODY[y]
    a[144:] = 0
    im = Image.fromarray(a)
    if k < 8:
        pool(im, .9 - u * .5)
    return im


def save_clip(name, frames, ms, pivot, palette, **extra):
    target = OUT / name
    (target / 'frames').mkdir(parents=True, exist_ok=True)
    paths = []
    for i, im in enumerate(frames):
        p = target / 'frames' / f'{i:02}.png'
        im.save(p)
        paths.append(p.relative_to(OUT).as_posix())
    w, h = frames[0].size
    sheet = blank((w * min(7, len(frames)), h * math.ceil(len(frames) / 7)))
    for i, im in enumerate(frames):
        sheet.alpha_composite(im, ((i % 7) * w, (i // 7) * h))
    sheet.save(target / 'sheet.png')
    data = dict(name=name, frames=paths, ms=ms, width=w, height=h, pivot=pivot,
                palette=palette, frameCount=len(frames), ppu=32, **extra)
    (target / 'clip.json').write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    return data


clips = {}
clips['thorn'] = save_clip('thorn-slash', [slash_frame(i) for i in range(10)],
                          [20, 20, 30, 30, 30, 35, 35, 40, 40, 50], [112, 128], COLORS[3:6],
                          activeAtMs=650, damageWindowMs=[650, 850], attackSizePx=[57.6, 48],
                          attackOffsetPx=[-28.8, -24], notes='Cosmetic tail outlasts the live hitbox.')
clips['broom'] = save_clip('broom-dust', [broom_cloud_frame(i) for i in range(16)],
                          [25,25,30,30,40,40,40,40,45,45,50,50,50,50,55,55], [112, 128], COLORS[2:5],
                          activeAtMs=700, damageWindowMs=[700, 880], attackSizePx=[83.2, 38.4],
                          attackOffsetPx=[-36.8, -19.2],
                          alphaPolicy='Uniform frame opacity for requested smoke fade; pixel boundaries remain hard.',
                          opacity=[255,255,255,255,255,245,225,200,170,140,110,80,54,32,16,0])
clips['elite'] = save_clip('elite-claw-dust', [dust_frame(i, True) for i in range(14)],
                          [30] * 4 + [40] * 5 + [50] * 5, [112, 128], COLORS[2:5],
                          activeAtMs=2240, damageWindowMs=[2240, 2410], attackSizePx=[80, 76.8],
                          attackOffsetPx=[-32, -38.4], notes='Dust accompanies the claw; existing slash trail is retained.')
clips['melt'] = save_clip('elite-burrow-body', [melt_frame(i) for i in range(14)],
                         [40] * 13 + [30], [96, 144], COLORS, role='body')
clips['bubbles'] = save_clip('elite-ground-warning', [bubble_frame(i) for i in range(10)],
                            [55] * 10, [96, 144], COLORS[:3], role='fx')
clips['burst'] = save_clip('elite-eruption-fx', [eruption_fx(i) for i in range(14)],
                          [30] * 6 + [45] * 8, [96, 144], COLORS[:3], role='fx')
clips['emerge'] = save_clip('elite-emerge-body', [emerge_body(i) for i in range(14)],
                           [40] * 14, [96, 144], COLORS, role='body')

pattern = dict(status='review-only-not-integrated', id='elite-ink-burrow-eruption',
    phases=[dict(id='melt', startMs=0, endMs=550), dict(id='hidden', startMs=550, endMs=1550),
            dict(id='warning', startMs=1550, endMs=2100), dict(id='eruption', startMs=2100, endMs=2280),
            dict(id='recovery', startMs=2280, endMs=2900)],
    targetLockMs=1550, damageWindowMs=[2100, 2280], damagePulses=1,
    proposedHitboxPx=[90, 96], requiredGround='valid walkable ground under player',
    targeting='Lock once when warning appears. Do not track during warning.',
    collisionPolicy='Do not damage during melt, hidden or warning. Reject walls/pits; safe fallback ground needed.',
    provisionalCooldownSeconds=8, timingStatus='prototype for visual review; balance unconfirmed')

data = dict(version=1, status='visual-review-only', facing='left', clips=clips, pattern=pattern, monsters=[])
for key, ident in [('thorn', '01_thorn-root'), ('broom', '02_broom-stalker'), ('elite', '04_well-warden')]:
    mon = monsters[ident]
    motion = mon['motions']['attack']
    data['monsters'].append(dict(key=key, id=ident, name=mon['name'],
         attack=dict(frames=['../../approved/' + p for p in motion['frames']], ms=motion['ms'],
                     pivot=[round(motion['pivot'][0] * motion['width']), motion['groundBoundaryY']]),
         base='../../approved/' + mon['motions']['base']['frames'][0]))

player_path = ROOT / 'local-assets/selected-large-review-v1/player.png'
if player_path.exists():
    shutil.copy2(player_path, OUT / 'player.png')
    data['player'] = 'player.png'
    data['playerPivot'] = [64, 121]
(OUT / 'review.json').write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
shutil.copy2(Path(__file__).with_name('attack-fx-review-v1.html'), OUT / 'index.html')


def frame_at(frames, ms, age):
    for im, dur in zip(frames, ms):
        if age < dur:
            return im
        age -= dur
    return frames[-1]


def export_gifs_and_board():
    board = Image.new('RGB', (960, 840), '#eeeae2')
    bd = ImageDraw.Draw(board)
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 16)
    entries = [('thorn', [650, 690, 740]), ('broom', [700, 800, 940]), ('elite', [2240, 2340, 2470])]
    for row, (key, times) in enumerate(entries):
        mon = next(m for m in data['monsters'] if m['key'] == key)
        motion = monsters[mon['id']]['motions']['attack']
        body = [Image.open(APPROVED / p).convert('RGBA') for p in motion['frames']]
        fx = clips[key]
        effects = [Image.open(OUT / p).convert('RGBA') for p in fx['frames']]
        samples = []
        dur = sum(motion['ms']) + 750
        for t in range(0, dur, 40):
            im = Image.new('RGBA', (240, 160), '#eeeae2')
            ox, gy = 164, 147
            if key == 'thorn':
                ox -= round(48 * min(1, max(0, (t - 650) / 200)))
            im.alpha_composite(frame_at(body, motion['ms'], min(t, sum(motion['ms']) - 1)),
                               (ox - mon['attack']['pivot'][0], gy - mon['attack']['pivot'][1]))
            age = t - fx['activeAtMs']
            if 0 <= age < sum(fx['ms']):
                im.alpha_composite(frame_at(effects, fx['ms'], age), (ox - 112, gy - 128))
            ImageDraw.Draw(im).line((0, gy + 1, 240, gy + 1), fill='#b9b4a9')
            samples.append(im.convert('RGB').resize((720, 480), Image.Resampling.NEAREST))
        samples[0].save(OUT / f'{key}-attack-preview.gif', save_all=True, append_images=samples[1:],
                        duration=40, loop=0, optimize=False)
        bd.text((12, row * 220 + 8), mon['name'], font=font, fill='#383831')
        for col, t in enumerate(times):
            sample = samples[min(len(samples) - 1, t // 40)]
            board.paste(sample.resize((288, 192), Image.Resampling.NEAREST), (90 + col * 286, row * 220))
    bd.text((12, 668), '추가 패턴: 잉크화 → 1초 잠행 → 발밑 예고 → 분출', font=font, fill='#383831')
    for j, (key, i) in enumerate([('melt', 7), ('bubbles', 7), ('burst', 2), ('emerge', 9)]):
        im = Image.open(OUT / clips[key]['frames'][i]).convert('RGBA')
        crop = im.crop((22, 30, 170, 151))
        board.paste(crop, (180 + j * 190, 710), crop)
    board.save(OUT / 'contact-sheet.png')


export_gifs_and_board()


def export_pattern_gif():
    imgs = {k: [Image.open(OUT / p).convert('RGBA') for p in clips[k]['frames']]
            for k in ['melt', 'bubbles', 'burst', 'emerge']}
    player = Image.open(OUT / 'player.png').convert('RGBA') if (OUT / 'player.png').exists() else None
    samples = []
    for t in range(0, 3900, 40):
        im = Image.new('RGBA',(480,210),'#eeeae2')
        gy=188
        px=165-round(92*max(0,min(1,(t-1770)/250)))
        if player: im.alpha_composite(player,(px-64,gy-121))
        def put(key,age,x):
            im.alpha_composite(frame_at(imgs[key],clips[key]['ms'],age),(x-96,gy-144))
        if t<550: put('melt',t,366)
        elif 1550<=t<2100: put('bubbles',t-1550,165)
        elif t>=2100:
            put('emerge',t-2100,165)
            if t-2100<sum(clips['burst']['ms']):put('burst',t-2100,165)
        ImageDraw.Draw(im).line((0,gy+1,480,gy+1),fill='#b9b4a9')
        samples.append(im.convert('RGB').resize((960,420),Image.Resampling.NEAREST))
    samples[0].save(OUT/'elite-burrow-preview.gif',save_all=True,append_images=samples[1:],
                    duration=40,loop=0,optimize=False)


export_pattern_gif()


def export_fx_overview():
    movies=[]
    for key in ['thorn','broom','elite']:
        gif=Image.open(OUT/f'{key}-attack-preview.gif')
        frames=[]
        for i in range(gif.n_frames):
            gif.seek(i)
            frames.append(gif.convert('RGB').resize((480,320),Image.Resampling.NEAREST))
        movies.append(frames)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',21)
    names=['가시뿌리 · 베기','먹빗자루 · 먼지구름','엘리트 · 할퀴기 먼지']
    out=[]
    for i in range(180):
        frame=Image.new('RGB',(1440,364),'#eeeae2')
        d=ImageDraw.Draw(frame)
        for j,movie in enumerate(movies):
            frame.paste(movie[i%len(movie)],(j*480,44))
            d.text((j*480+18,10),names[j],font=font,fill='#41413d')
        out.append(frame)
    out[0].save(OUT/'all-attack-fx-preview.gif',save_all=True,append_images=out[1:],
                duration=40,loop=0,optimize=False)


export_fx_overview()
validation = []
for key, clip in clips.items():
    allowed = {tuple(bytes.fromhex(c[1:])) for c in clip['palette']}
    used = set()
    binary = True
    for i,p in enumerate(clip['frames']):
        im = np.asarray(Image.open(OUT / p).convert('RGBA'))
        alphas = set(np.unique(im[:, :, 3]))
        binary = binary and alphas.issubset({0,255})
        assert alphas.issubset({0,clip['opacity'][i] if key == 'broom' else 255}), p
        colors = set(map(tuple, im[im[:, :, 3] > 0][:, :3]))
        assert colors.issubset(allowed), (p, colors - allowed)
        used |= colors
        assert im[:, :, 3].any() or (key=='broom' and i==len(clip['frames'])-1), p
    validation.append(dict(clip=key, frames=clip['frameCount'], colors=len(used), binaryAlpha=binary,
                           uniformOpacityFade=key=='broom'))
    if key not in ['melt', 'emerge']:
        assert len(used) <= 3
(OUT / 'validation.json').write_text(json.dumps(validation, indent=2), encoding='utf-8')
with zipfile.ZipFile(OUT / 'native-frames.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for p in OUT.rglob('*'):
        if p.is_file() and p.suffix in {'.png', '.json'}:
            z.write(p, p.relative_to(OUT))
print(json.dumps(dict(output=str(OUT), clips=validation), ensure_ascii=True))
