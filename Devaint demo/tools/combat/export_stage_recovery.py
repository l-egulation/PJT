"""Validate stage recovery tuning and generate its Unity Resources copy."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/stage_recovery.json'
OUTPUT = ROOT / 'unity/Assets/ForestCombatV2/Resources/StageRecovery.json'


def validate(data):
    assert type(data['usesPerStage']) is int and 1 <= data['usesPerStage'] <= 10
    assert len(data['prices']) == data['usesPerStage'] and all(type(p) is int and p >= 0 for p in data['prices'])
    for key, low, high in (('healthFraction', 0, 1), ('holdSeconds', 0, 10), ('reuseDelaySeconds', 0, 60), ('interactionReach', 0, 10)):
        value = data[key]
        assert type(value) in (int, float) and low <= value <= high, f'invalid {key}'
        assert value > 0 or key == 'reuseDelaySeconds', f'{key} must be positive'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    validate(data)
    text = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding='utf-8') != text:
            parser.exit(1, 'StageRecovery.json is stale; run tools/combat/export_stage_recovery.py\n')
        print('stage recovery tuning is up to date')
    else:
        OUTPUT.write_text(text, encoding='utf-8', newline='\n')
        print(f'wrote {OUTPUT.relative_to(ROOT)}')


if __name__ == '__main__':
    main()
