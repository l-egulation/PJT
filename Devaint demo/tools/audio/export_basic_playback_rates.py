#!/usr/bin/env python3
"""Carry the selected PCM's baked rate compensation into runtime without encoding audio."""
import argparse
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/audio/basic-attack-selections.json'
TARGET = ROOT / 'unity/Assets/Deviant/Audio/Resources/WeaponSounds/BasicPlaybackRates.json'

def content():
    rows = json.loads(SOURCE.read_text(encoding='utf-8'))['outputs']
    entries = []
    for row in rows:
        rate = row['playbackRate']
        if not math.isfinite(rate) or not .01 <= rate <= 3:
            raise ValueError('Invalid selected audio compensation: ' + row['actionId'])
        entries.append(dict(id=row['actionId'], rate=rate))
    if len({r['id'] for r in entries}) != len(entries):
        raise ValueError('Duplicate basic audio calibration')
    return json.dumps(dict(schema=1, entries=entries), ensure_ascii=False, indent=2) + '\n'

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = content()
    if args.check:
        if not TARGET.exists() or TARGET.read_text(encoding='utf-8') != result:
            raise SystemExit('BasicPlaybackRates.json is stale; run tools/audio/export_basic_playback_rates.py')
    else:
        TARGET.write_text(result, encoding='utf-8')
    print('PASS: selected audio compensation ' + ('matches runtime' if args.check else 'exported without changing PCM'))
