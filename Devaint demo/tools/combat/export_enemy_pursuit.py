"""Validate melee pursuit tuning and generate its Unity Resources copy.

Source: data/combat/enemy_pursuit.json (tune with tools/combat/monster_pursuit_tuner/index.html).
Only the `pursuit` block reaches Unity. `rangedRange` records the tuner's proposed ranges; those values are
applied to each species' own source data once confirmed, not from here.
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/enemy_pursuit.json'
OUTPUT = ROOT / 'unity/Assets/Deviant/Combat/Resources/EnemyPursuit.json'
RANGES = {
    'chaseSpeedScale': (0, 4), 'jumpHeight': (0, 10), 'jumpAirSpeed': (0, 30), 'jumpWindupSeconds': (0, 2),
    'maxDropHeight': (0, 100), 'replanSeconds': (0, 5), 'detectionScale': (0, 5),
    'targetFacingDeadzone': (0, 1),
    'randomHopIntervalMin': (0, 60), 'randomHopIntervalMax': (0, 60),
    'randomHopHeightMin': (0, 10), 'randomHopHeightMax': (0, 10),
}
# Zero permits an immediate route jump or a disabled target-facing band.
ZERO_OK = {'jumpWindupSeconds', 'pathJumpDelayMin', 'pathJumpDelayMax', 'targetFacingDeadzone'}
DELAYS = {'pathJumpDelayMin': (0, 5), 'pathJumpDelayMax': (0, 5)}
KNOWN_MELEE = {'01_thorn-root', '02_broom-stalker', 'draft_07_heavy_charger', 'K4', 'B3', 'M3'}
KNOWN_RANGED_WALKERS = {'03_gutter-spitter', 'R-selected', 'H3'}
LEASH_RANGES = {'leaveScreenSeconds': (0, 30), 'unseenGiveUpSeconds': (0, 60), 'returnTeleportSeconds': (0, 120),
                'noRouteTeleportSeconds': (0, 30)}
ALERT_RANGES = {'reactionDelayMin': (0, 5), 'reactionDelayMax': (0, 5), 'alertRadius': (0, 100),
                'alertDelayPerUnit': (0, 2), 'alertJitter': (0, 5)}


def compile_settings(data):
    assert data['schema'] == 'enemy-pursuit-v1'
    p = data['pursuit']
    applies = p['appliesTo']
    assert isinstance(applies, list) and applies and len(set(applies)) == len(applies)
    assert set(applies) <= KNOWN_MELEE, f'unknown or non-melee species: {sorted(set(applies) - KNOWN_MELEE)}'
    assert p['persistAfterDetection'] is True and p['knowsTargetPositionAfterDetection'] is True and p['leash'] == 'screenLeash'
    out = {'schema': data['schema'], 'status': data['status'], 'appliesTo': applies}
    for key, (low, high) in RANGES.items():
        value = p[key]
        assert type(value) in (int, float) and (low < value <= high or key in ZERO_OK and value == 0), f'invalid {key}'
        out[key] = value
    for key, (low, high) in DELAYS.items():
        value = p[key]
        assert type(value) in (int, float) and low <= value <= high, f'invalid {key}'
        out[key] = value
    assert out['randomHopIntervalMax'] >= out['randomHopIntervalMin'], 'randomHopInterval range'
    assert out['randomHopHeightMax'] >= out['randomHopHeightMin'], 'randomHopHeight range'
    assert out['pathJumpDelayMax'] >= out['pathJumpDelayMin'], 'pathJumpDelay range'
    for key in ('dropThrough', 'walkOffLedges', 'randomHop'):
        assert isinstance(p[key], bool)
        out[key] = p[key]
    species = data.get('species', {})
    unknown = set(species) - set(applies) - {'note'}
    assert not unknown, f'species overrides for non-pursuit ids: {sorted(unknown)}'
    out['species'] = []
    for sid in applies:
        o = species.get(sid, {})
        jh = o.get('jumpHeight', out['jumpHeight']); hop = o.get('randomHop', out['randomHop'])
        assert type(jh) in (int, float) and 0 < jh <= 10, f'invalid jumpHeight for {sid}'
        assert isinstance(hop, bool), f'invalid randomHop for {sid}'
        out['species'].append({'id': sid, 'jumpHeight': jh, 'randomHop': hop})
    leash = data['screenLeash']
    assert isinstance(leash['enabled'], bool)
    out['screenLeash'] = leash['enabled']
    for key, (low, high) in LEASH_RANGES.items():
        value = leash[key]
        assert type(value) in (int, float) and low <= value <= high, f'invalid {key}'
        out[key] = value
    assert out['unseenGiveUpSeconds'] > 0 and out['returnTeleportSeconds'] > 0, 'leash timeouts must be positive'
    v = data['individuality']['individualVariance']
    assert type(v) in (int, float) and 0 <= v < 1, 'invalid individualVariance'
    out['individualVariance'] = v
    a = data['alert']
    assert isinstance(a['alertEnabled'], bool)
    out['alertEnabled'] = a['alertEnabled']
    for key, (low, high) in ALERT_RANGES.items():
        assert type(a[key]) in (int, float) and low <= a[key] <= high, f'invalid {key}'
        out[key] = a[key]
    assert a['reactionDelayMax'] >= a['reactionDelayMin'], 'reactionDelay range'
    assert type(a['alertMarkSeconds']) in (int, float) and 0 < a['alertMarkSeconds'] <= 10
    out['alertMarkSeconds'] = a['alertMarkSeconds']
    receivers = a['receivers']
    assert isinstance(receivers, list) and len(set(receivers)) == len(receivers)
    assert set(receivers) <= KNOWN_MELEE | KNOWN_RANGED_WALKERS, f'unknown alert receivers: {sorted(set(receivers) - KNOWN_MELEE - KNOWN_RANGED_WALKERS)}'
    out['alertReceivers'] = receivers
    for species, r in data.get('rangedRange', {}).items():
        assert species not in applies and r['current'] > 0 and r['proposed'] > 0
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    text = json.dumps(compile_settings(json.loads(SOURCE.read_text(encoding='utf-8'))), ensure_ascii=False, indent=2) + '\n'
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding='utf-8') != text:
            parser.exit(1, 'EnemyPursuit.json is stale; run tools/combat/export_enemy_pursuit.py\n')
        print('enemy pursuit tuning is up to date')
    else:
        OUTPUT.write_text(text, encoding='utf-8', newline='\n')
        print(f'wrote {OUTPUT.relative_to(ROOT)}')


if __name__ == '__main__':
    main()
