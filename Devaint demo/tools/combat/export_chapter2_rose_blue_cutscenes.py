"""Validate the approved Rose/Blue cutscene timelines and copy them into Unity Resources.

Sources (from the reviewed HTML cutscenes, resources monsters/stage-02/boss-arena-cutscenes-20261106/preview/):
  data/combat/chapter2_rose_blue_intro.json        -> Resources/RoseBlueIntro.json       (intro/)
  data/combat/chapter2_rose_blue_phase_shift.json  -> Resources/RoseBluePhaseShift.json  (phase-shift/)
"""
import argparse
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
motions = json.loads((root / 'unity/Assets/Chapter2/Resources/RoseBlueMotions.json').read_text(encoding='utf-8'))
known = {m['id'] for m in motions['motions']}


def common(data):
    assert 0 <= data['startAt'] < data['duration']
    ids = {d['id'] for d in data['dialogue']}
    for d in data['dialogue']:
        assert d['speaker'] in ('rose', 'blue') and d['from'] < d['to'] <= data['duration'], d['id']
        assert d.get('cover') is None or d['cover'] in ids, d['id']
    for p in data['rosePlan'] + data['bluePlan']:
        assert p['motion'] in known and p['from'] < p['to'], p


def intro(data):
    assert data['format'] == 'deviant-roseblue-intro-v1'
    t = data['times']
    assert data['startAt'] < t['appear'] < t['vanish'] < data['duration']
    assert t['bubbleStart'] < t['bubbleFormed'] <= t['liftEnd'] <= t['bubblePop'] <= t['barsOut'] <= data['duration']
    assert t['floorEraseStart'] <= t['platformsStart'] <= t['machineStart'] < t['liftEnd']


def phase_shift(data):
    assert data['format'] == 'deviant-roseblue-phase-shift-v1'
    t = data['times']
    assert data['startAt'] == t['machineBlast'] < t['barsIn'] < t['appear'] < t['barsOut'] <= data['duration']


JOBS = [('chapter2_rose_blue_intro.json', 'RoseBlueIntro.json', intro),
        ('chapter2_rose_blue_phase_shift.json', 'RoseBluePhaseShift.json', phase_shift)]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--check', action='store_true')
check = parser.parse_args().check
for source, target, validate in JOBS:
    data = json.loads((root / 'data/combat' / source).read_text(encoding='utf-8'))
    common(data)
    validate(data)
    out = root / 'unity/Assets/Chapter2/Resources' / target
    text = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
    if check:
        assert out.read_text(encoding='utf-8') == text, 'Stale Unity cutscene data: ' + target
    else:
        out.write_text(text, encoding='utf-8', newline='\n')
print('Approved Rose/Blue cutscenes synchronized')
