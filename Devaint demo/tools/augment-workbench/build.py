#!/usr/bin/env python3
"""Generate the maintenance-augment review page from its JSON source (runtime values: tools/combat/export_maintenance_augments.py)."""
import argparse
from collections import Counter
import json
import math
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'data/combat/maintenance_augments_proposal.json'
TEMPLATE = Path(__file__).with_name('template.html')
OUTPUT = Path(__file__).with_name('index.html')


def validate(data):
    if data.get('schemaVersion') != 1 or data.get('status') != 'runtime_trial':
        raise ValueError('Expected version 1 runtime-trial catalog')
    cards = data['augments']
    ids = [card['id'] for card in cards]
    if len(ids) != len(set(ids)):
        raise ValueError('Duplicate augment ID')
    groups = data['groups']
    for card in cards:
        if card['group'] not in groups:
            raise ValueError(f"Unknown group: {card['id']}")
        for field in ('id', 'name', 'gain', 'cost', 'play'):
            if not isinstance(card.get(field), str) or not card[field].strip():
                raise ValueError(f"Missing {field}: {card['id']}")
    stable = sum(card['group'] == 'safe' for card in cards)
    if data['catalogCounts'] != {'total': len(cards), 'stable': stable, 'risk': len(cards) - stable}:
        raise ValueError('Catalog counts do not match cards')
    policy = data['selection']
    if (policy['stableCards'], policy['riskCards'], policy['offeredCards'], policy['chosenCards']) != (1, 2, 3, 1):
        raise ValueError('Selection must offer one stable and two risky cards, choosing one')
    if stable < 1 or len({c['group'] for c in cards if c['group'] != 'safe'}) < 2:
        raise ValueError('Not enough compatible offer categories')
    growth = data['numericRules']['C14']
    start, gain, final = (growth[key] for key in ('initialMaxHealthRatio', 'maxHealthRatioGainPerMilestone', 'finalMaxHealthRatio'))
    if not (0 < start < 1 < final and gain > 0 and growth['killsPerMilestone'] > 0):
        raise ValueError('Invalid growth ratios')
    milestones = (final - start) / gain
    if not math.isclose(milestones, round(milestones)):
        raise ValueError('Growth cap must land on a kill milestone')
    card = next(card for card in cards if card['id'] == 'C14')
    if card['group'] == 'safe' or growth['basis'] != 'max_health_before_selection':
        raise ValueError('Growing Pains must be risky and use pre-selection maximum health')
    expected_start = f'{start * 100:g}%'
    expected_final = f'{final * 100:g}%'
    if expected_start not in card['cost'] or expected_final not in card['gain']:
        raise ValueError('Growth card text disagrees with numeric rules')


def render(data):
    validate(data)
    counts = Counter(card['group'] for card in data['augments'])
    tokens = {
        '__AUGMENT_DATA__': json.dumps(data['augments'], ensure_ascii=False, indent=2).replace('</', '<\\/'),
        '__GROUPS__': json.dumps(data['groups'], ensure_ascii=False),
        '__TOTAL__': str(data['catalogCounts']['total']),
        '__SAFE__': str(data['catalogCounts']['stable']),
        '__RISK__': str(data['catalogCounts']['risk']),
        '__ADOPTED__': str(sum(card.get('batch') == 'adopted' for card in data['augments'])),
        **{f'__COUNT_{group.upper()}__': str(counts[group]) for group in data['groups']},
    }
    html = TEMPLATE.read_text(encoding='utf-8')
    for token, value in tokens.items():
        html = html.replace(token, value)
    if re.search(r'__(?:AUGMENT_DATA|GROUPS|TOTAL|SAFE|RISK|ADOPTED|COUNT_[A-Z]+)__', html):
        raise ValueError('Unresolved template placeholder')
    return html


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='validate and compare without writing')
    args = parser.parse_args()
    data = json.loads(DATA.read_text(encoding='utf-8'))
    html = render(data)
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding='utf-8') != html:
            raise SystemExit('Generated augment review HTML is out of date; run build.py')
        print(f"PASS: {len(data['augments'])} design cards; selection, growth rules and generated HTML agree")
    else:
        OUTPUT.write_text(html, encoding='utf-8')
        print(f'Generated {OUTPUT.relative_to(ROOT)}')


if __name__ == '__main__':
    main()
