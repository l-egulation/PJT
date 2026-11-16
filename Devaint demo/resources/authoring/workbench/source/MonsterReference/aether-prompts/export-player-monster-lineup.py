"""Compose native sprites on one ground line without changing the approved art."""
from pathlib import Path
import argparse
import bisect
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont, ImageSequence

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'approved'
parser = argparse.ArgumentParser()
parser.add_argument('--include-boss', action='store_true')
args = parser.parse_args()
OUT = ROOT / ('local-assets/player-monster-lineup-v2' if args.include_boss
              else 'local-assets/player-monster-lineup-v1')
OUT.mkdir(parents=True, exist_ok=True)
manifest = json.loads((SRC / 'manifest.json').read_text(encoding='utf-8'))
monsters = manifest['monsters'][:4]
hero_path = ROOT / 'local-assets/selected-large-review-v1/player.png'
hero = Image.open(hero_path).convert('RGBA')
assert hero.getbbox()[3] - hero.getbbox()[1] == 64
colors = [(255, 255, 255)] + [tuple(bytes.fromhex(c[1:])) for c in manifest['palette']]
for pixel in hero.get_flattened_data():
    if pixel[3] and pixel[:3] not in colors:
        colors.append(pixel[:3])
palette = [v for rgb in colors for v in rgb] + [0] * (768 - len(colors) * 3)
indices = {rgb: i for i, rgb in enumerate(colors)}
SIZE, SCALE, GROUND = ((1150, 280), 2, 240) if args.include_boss else ((850, 180), 2, 140)
CENTERS = [200, 336, 480, 720]
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 8)
title_font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 10)
labels = {'base': '기본 자세', 'idle': '대기', 'walk': '걷기', 'attack': '공격', 'hit': '피격', 'death': '죽음'}


def indexed(rgba):
    result = Image.new('P', rgba.size, 0)
    result.putpalette(palette)
    values = []
    for r, g, b, a in rgba.get_flattened_data():
        assert a in (0, 255)
        values.append(indices[(r, g, b)] if a else 0)
    result.putdata(values)
    return result, rgba.getchannel('A')


hero_sprite = indexed(hero)
boss_path = SRC / manifest['monsters'][4]['motions']['base']['frames'][0]
boss_sprite = indexed(Image.open(boss_path).convert('RGBA')) if args.include_boss else None
if boss_sprite:
    assert boss_sprite[1].getbbox()[3] - boss_sprite[1].getbbox()[1] == 208
motions = []
for monster in monsters:
    record = {}
    for key, motion in monster['motions'].items():
        editor = json.loads((SRC / motion['editorJSON']).read_text(encoding='utf-8'))
        frames = [indexed(Image.open(SRC / p).convert('RGBA')) for p in motion['frames']]
        ends, total = [], 0
        for ms in motion['ms']:
            total += ms
            ends.append(total)
        record[key] = {'frames': frames, 'ends': ends, 'duration': total,
                       'leftPad': editor.get('rangeExtension', {}).get('sourceOffset', [0, 0])[0],
                       'pivotX': motion['pivot'][0]*motion['width'],
                       'ground': motion.get('groundBoundaryY', editor.get('groundBoundaryY', 120))}
    motions.append(record)


def stamp(canvas, sprite, center, ground, pivot_x=None):
    im, mask = sprite
    x, y = round(center - (im.width / 2 if pivot_x is None else pivot_x)), GROUND - ground
    box = mask.getbbox()
    if box:
        assert 0 <= x + box[0] <= x + box[2] <= SIZE[0]
        assert 0 <= y + box[1] <= y + box[3] <= SIZE[1]
    canvas.paste(im, (x, y), mask)


def render(key, time_ms=0):
    canvas = Image.new('P', SIZE, 0)
    canvas.putpalette(palette)
    draw = ImageDraw.Draw(canvas)
    draw.line((12, GROUND + 1, SIZE[0] - 12, GROUND + 1), fill=6)
    count = 5 if args.include_boss else 4
    draw.text((12, 5), f'주인공 + 몬스터 {count}종  /  ' + labels[key], font=title_font, fill=1)
    stamp(canvas, hero_sprite, 64, 121)
    draw.text((64, GROUND + 11), '주인공', font=font, fill=1, anchor='mt')
    draw.text((64, GROUND + 23), '64px', font=font, fill=2, anchor='mt')
    for monster, record, center in zip(monsters, motions, CENTERS):
        actual_key = key if key in record else 'base'
        motion = record[actual_key]
        t = time_ms % motion['duration'] if key in ('idle', 'walk') else min(time_ms, motion['duration'] - 1)
        frame = min(bisect.bisect_right(motion['ends'], t), len(motion['frames']) - 1)
        stamp(canvas, motion['frames'][frame], center, motion['ground'], motion['pivotX'])
        name = '엘리트 · 우물 수문장' if monster['id'].startswith('04_') else monster['name']
        draw.text((center, GROUND + 11), name, font=font, fill=1, anchor='mt')
        note = '미제작 · 기본 자세' if actual_key != key else labels[key]
        if key == 'base':
            box = motion['frames'][0][1].getbbox()
            note = str(box[3] - box[1]) + 'px'
        draw.text((center, GROUND + 23), note, font=font, fill=2, anchor='mt')
    if boss_sprite:
        stamp(canvas, boss_sprite, 1000, 240)
        draw.text((1000, GROUND + 11), '보스 · 시계탑 집행자', font=font, fill=1, anchor='mt')
        draw.text((1000, GROUND + 23), '208px · 기본 자세', font=font, fill=2, anchor='mt')
    return canvas


still = render('base')
still.save(OUT / 'player-monsters-lineup-native.png')
still.resize((SIZE[0] * SCALE, SIZE[1] * SCALE), Image.Resampling.NEAREST).save(OUT / 'player-monsters-lineup-2x.png')

# GIF playback uses a 20ms minimum step to avoid browsers stretching 10ms frames.
# Only export timestamps round by at most 10ms; the approved frame data stays exact.
frames, durations, phases = [], [], []
for key in ('idle', 'walk', 'attack', 'hit', 'death'):
    repeats = 2 if key in ('idle', 'walk') else 1
    length = max(record[key]['duration'] for record in motions if key in record) * repeats
    length = ((length + 10) // 20) * 20
    phases.append({'motion': key, 'startMs': sum(durations), 'durationMs': length})
    for t in range(0, length, 20):
        im = render(key, t)
        if frames and im.tobytes() == frames[-1].tobytes():
            durations[-1] += 20
        else:
            frames.append(im)
            durations.append(20)
durations[-1] += 660
expanded = [im.resize((SIZE[0] * SCALE, SIZE[1] * SCALE), Image.Resampling.NEAREST) for im in frames]
path = OUT / 'player-monsters-all-motions.gif'
expanded[0].save(path, save_all=True, append_images=expanded[1:], duration=durations,
                 loop=0, disposal=1, optimize=False, background=0)
decoded = Image.open(path)
assert decoded.info['loop'] == 0 and decoded.n_frames == len(expanded)
for frame, expected, ms in zip(ImageSequence.Iterator(decoded), expanded, durations):
    assert frame.convert('RGB').tobytes() == expected.convert('RGB').tobytes()
    assert frame.info['duration'] == ms

settings = {'source': 'approved/manifest.json', 'playerSHA256': hashlib.sha256(hero_path.read_bytes()).hexdigest(),
            'nativeSize': SIZE, 'scale': SCALE, 'groundBoundaryY': GROUND, 'playerBodyHeightPx': 64,
            'phases': phases, 'durationMs': sum(durations), 'gifFrames': len(frames),
            'timingPolicy': '20ms export steps, no source timing edits; player is a fixed size reference',
            'eliteDeath': 'approved small-droplet v4; 34 frames / 1790ms'}
if args.include_boss:
    settings['boss'] = {'source': str(boss_path.relative_to(ROOT)), 'bodyHeightPx': 208,
                        'sha256': hashlib.sha256(boss_path.read_bytes()).hexdigest(),
                        'motion': 'static two-fist base; no approval metadata changed'}
(OUT / 'export.json').write_text(json.dumps(settings, ensure_ascii=False, indent=2), encoding='utf-8')
(OUT / 'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>주인공과 몬스터 · 나란히 비교</title>
<style>body{max-width:1740px;margin:28px auto;padding:0 20px;background:#202321;color:#eee;font:16px system-ui}a{color:#e5c896}.screen{overflow:auto;background:#fff;margin:18px 0}img{display:block;width:1700px;height:360px;image-rendering:pixelated}p{line-height:1.7}</style>
<h1>주인공과 몬스터 4종</h1><p>같은 픽셀 배율과 바닥선으로 배치했습니다. 주인공 64px · 가시뿌리 60px · 먹빗자루 68px · 배수관 포수 58px · 엘리트 112px.<br>대기 → 걷기 → 공격 → 죽음 순서로 반복합니다. 엘리트는 사거리 2배 공격과 작은 잉크 방울 죽음을 재생합니다.</p>
<div class="screen"><img src="player-monsters-all-motions.gif" alt="주인공과 몬스터 4종 전체 모션 반복"></div><p><a download href="player-monsters-all-motions.gif">전체 모션 GIF 다운로드</a> · <a download href="player-monsters-lineup-2x.png">크기 비교 PNG 다운로드</a> · <a download href="player-monsters-lineup-native.png">1배 PNG</a></p>
<div class="screen"><img src="player-monsters-lineup-2x.png" alt="주인공과 몬스터의 기본 자세 및 크기 비교"></div></html>'''
    .replace('4종', '5종' if args.include_boss else '4종')
    .replace('1740px', str(SIZE[0] * SCALE + 40) + 'px')
    .replace('1700px', str(SIZE[0] * SCALE) + 'px')
    .replace('360px', str(SIZE[1] * SCALE) + 'px')
    .replace('엘리트 112px.', '엘리트 112px · 보스 208px. 보스는 양주먹 기본 자세로 고정합니다.' if args.include_boss else '엘리트 112px.'), encoding='utf-8')
print(json.dumps(settings, ensure_ascii=True))
print('PASS: exact native pixels and shared baseline; GIF pixels/timing/loop verified.')
