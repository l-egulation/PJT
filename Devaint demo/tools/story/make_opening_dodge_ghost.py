"""Writes the opening's dash-dodge demonstration (track id "dodge") and the dash card's run-dash-run (track id
"dash_run") into data/story/opening_ghosts.json.

The other tracks were recorded at 60 fps from the HTML prototype; this one is built from the same numbers (run 7.5 units/s,
a dash of 0.2 s covering 4.5 units, recorded in the "dash" track) because the prototype has no projectile. The ghost stands
on the road between the bridge piers, a round ink ball flies at it from the right and the ghost
dashes through it: the dash is invulnerable (data/combat locomotion dashInvulnerability).

Run: python -I tools/story/make_opening_dodge_ghost.py, then python tools/assets/build_hub_bindings.py.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/story/opening_ghosts.json'

FPS = 60
RUN = 7.5 / FPS          # units per frame, as the recorded run
DASH = 4.5 / 12          # 12 frames (0.2 s) of dash
SLIDE = [.33, .3, .2, .1]  # the stop after the dash
START_X = -117.6         # ground between the piers at -122..-118 and -107..-103
IDLE_BEFORE, RUN_FRAMES, IDLE_AFTER = 20, 24, 50
SHOT = {
    'shotArt': 'ink-ball',  # a round ink ball drawn by the game (StartingVillageWorld.ShotSprite)
    'shotFrom': IDLE_BEFORE, 'shotTo': 85,
    'shotX': -107.6, 'shotY': 1.0, 'shotSpeed': -9.0, 'shotScale': 1,
}


def build():
    frames = []
    x = START_X

    def add(clip, t, held='', tapped=''):
        frames.append({'x': round(x, 4), 'y': 0.0, 'face': 1, 'clip': clip, 'clipTime': round(t, 4), 'held': held, 'tapped': tapped, 'punch': 0})

    for f in range(IDLE_BEFORE):
        add('idle', f / FPS)
    for f in range(RUN_FRAMES):
        x += RUN
        add('run', f / FPS, 'ArrowRight')
    for f in range(12):
        x += DASH
        add('dash', f / FPS, 'ArrowRight+ShiftLeft' if f == 0 else 'ArrowRight', 'ShiftLeft' if f == 0 else '')
    for f, step in enumerate(SLIDE):
        x += step
        add('run', (RUN_FRAMES + f) / FPS)
    for f in range(IDLE_AFTER):
        add('idle', f / FPS)
    track = {
        'id': 'dodge', 'keys': ['Shift'], 'codes': ['ShiftLeft'], 'text': '대시로 피하기', 'needHeld': [],
        'note': 'built by tools/story/make_opening_dodge_ghost.py, not recorded',
        'startX': START_X, 'startY': 0.0,
    }
    for key in ('x', 'y', 'face', 'clip', 'clipTime', 'held', 'tapped', 'punch'):
        track[key] = [frame[key] for frame in frames]
    track.update(SHOT)
    return track


def build_dash_run():
    """The dash card of the controls (track "dash_run"): running, a dash, running on, so a wrapping card never stops."""
    frames = []
    x = 0.0

    def add(clip, t, held='', tapped=''):
        frames.append({'x': round(x, 4), 'y': 0.0, 'face': 1, 'clip': clip, 'clipTime': round(t, 4), 'held': held, 'tapped': tapped, 'punch': 0})

    for f in range(24):
        x += RUN
        add('run', f / FPS, 'ArrowRight')
    for f in range(12):
        x += DASH
        add('dash', f / FPS, 'ArrowRight+ShiftLeft' if f == 0 else 'ArrowRight', 'ShiftLeft' if f == 0 else '')
    for f in range(24):
        x += RUN
        add('run', (24 + f) / FPS, 'ArrowRight')
    track = {'id': 'dash_run', 'keys': ['Shift'], 'codes': ['ShiftLeft'], 'text': '대시', 'needHeld': [],
             'note': 'built by tools/story/make_opening_dodge_ghost.py, not recorded', 'startX': 0.0, 'startY': 0.0}
    for key in ('x', 'y', 'face', 'clip', 'clipTime', 'held', 'tapped', 'punch'):
        track[key] = [frame[key] for frame in frames]
    return track


def main():
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    made = [build(), build_dash_run()]
    ids = {t['id'] for t in made}
    data['ghosts'] = [g for g in data['ghosts'] if g['id'] not in ids] + made
    SOURCE.write_text(json.dumps(data, ensure_ascii=False, separators=(',', ':')), encoding='utf-8', newline='\n')
    for track in made:
        print(f'{track["id"]}: {len(track["x"])} frames, x {track["x"][0]} .. {track["x"][-1]}')


if __name__ == '__main__':
    main()
