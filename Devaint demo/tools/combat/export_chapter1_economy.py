"""Validate the chapter 1 reward/price trial values and copy them to Unity Resources."""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/chapter1_economy.json'
OUTPUT = ROOT / 'unity/Assets/ForestCombatV2/Resources/Chapter1Economy.json'
ROLES = {'normal', 'elite', 'boss'}
TRACKS = {'statUpgrade', 'slotUpgrade'}


def validate(data):
    assert set(data['excludedFormInks']) == {'base_sword', 'hammer', 'launcher'}, 'starting forms must not enter ink reward pools'
    healing = data['roomClearHealth']
    assert set(healing) == {'combat', 'elite', 'boss'}
    assert all(type(value) is int and value >= 0 for value in healing.values()), 'room clear health must be nonnegative integers'
    roles = {table['role'] for table in data['rewards']}
    assert roles == ROLES, f'reward roles must be {sorted(ROLES)}: {sorted(roles)}'
    assert data['slotUpgradeCosts']==[50,100,180,350,600]
    assert {table['role'] for table in data['chapter2Rewards']}==ROLES
    for table in data['rewards']+data['chapter2Rewards']:
        # Palettes are fixed at departure; kills and shops never hand them out.
        assert 'paletteChance' not in table and 'paletteCount' not in table, f"{table['role']} must not drop palettes"
        for key in ('partsChance', 'stampChance'):
            assert 0 <= table[key] <= 1, f"{table['role']}.{key} must be within 0..1"
        for key in ('gold', 'parts'):
            span = table[key]
            assert 0 <= span['min'] <= span['max'], f"{table['role']}.{key} range is invalid"
        assert table['stampCount'] >= 0
        assert table.get('inkCount', 0) >= 0 and table.get('specialInkCount', 0) >= 0
    tracks = {price['id'] for price in data['prices']}
    assert tracks == TRACKS, f'price tracks must be {sorted(TRACKS)}: {sorted(tracks)}'
    for price in data['prices']:
        assert price['start'] >= 0 and price['step'] >= 0, f"{price['id']} price is invalid"
    validate_ink_shop(data)
    assert 0 <= data['weaponStampShare'] <= 1
    assert data['stampSwapCost'] >= 0 and 'paletteOffers' not in data, 'maintenance does not sell palettes'
    assert type(data['stampSaleGold']) is int and data['stampSaleGold'] >= 0, 'stampSaleGold must be a nonnegative integer'
    # Stamps dropped in one stage (chapter 1 random stage, chapter 2 stage) stop at this count (user decision 2026-11-06).
    assert type(data['stageStampLimit']) is int and data['stageStampLimit'] >= 1, 'stageStampLimit must be a positive integer'


def validate_ink_shop(data):
    # The maintenance ink stall replaced the random draw (user decision 2026-11-09): each facility shows 2~3 grade 3~4
    # colour inks at fixed prices close to what four to five attack upgrades cost in total.
    shop = data['inkShop']
    grades = shop['grades']
    assert grades == [3, 4], 'the ink stall sells grade 3 and 4 inks'
    count = shop['offerCount']
    assert type(count['min']) is int and type(count['max']) is int and 1 <= count['min'] <= count['max'], 'offerCount range is invalid'
    prices = {row['grade']: row['gold'] for row in shop['gradePrices']}
    assert sorted(prices) == grades, f'every sold grade needs one price: {sorted(prices)}'
    attack = next(price for price in data['prices'] if price['id'] == 'statUpgrade')
    def total(n):
        return sum(attack['start'] + attack['step'] * i for i in range(n))
    for grade, gold in prices.items():
        assert type(gold) is int and total(4) <= gold <= total(5),             f'grade {grade} ink costs {gold}G; it must stay within four to five attack upgrades ({total(4)}~{total(5)}G)'
    assert prices[3] <= prices[4], 'a grade 4 ink must not be cheaper than a grade 3 ink'
    assert type(shop['saleGold']) is int and shop['saleGold'] >= 0, 'saleGold must be a nonnegative integer'


def render():
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    validate(data)
    return json.dumps(data, ensure_ascii=False, indent=2) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Fail when the Unity copy differs from the source')
    args = parser.parse_args()
    text = render()
    if args.check:
        if not OUTPUT.is_file() or OUTPUT.read_text(encoding='utf-8') != text:
            print(f'{OUTPUT.relative_to(ROOT)} is out of date; run without --check', file=sys.stderr)
            return 1
        print('chapter1 economy is up to date')
        return 0
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(text, encoding='utf-8', newline='\n')
    print(f'wrote {OUTPUT.relative_to(ROOT)}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
