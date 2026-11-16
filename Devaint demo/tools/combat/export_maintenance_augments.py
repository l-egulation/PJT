"""Validate the maintenance augment runtime values and write the Unity catalog (implemented cards only)."""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/maintenance_augments_proposal.json'
OUTPUT = ROOT / 'unity/Assets/Deviant/Combat/Resources/MaintenanceAugments.json'
# Every effect field the Unity runtime reads (Deviant.Run.AugmentEffect). Unknown fields are refused.
EFFECT_FIELDS = (
    'damage', 'weaponDamage', 'basicDamage', 'skillDamage', 'incoming', 'maxHealth', 'healing', 'walkSpeed',
    'basicSpeed', 'skillCooldown', 'critChance', 'executeThreshold', 'executeDamage', 'eliteBossWeaponDamage',
    'sameEnemyHits', 'sameEnemyExtra', 'attackHits', 'attackExtra', 'signHits', 'signExtra',
    'lowHealthThreshold', 'lowHealthDamage', 'highHealthDamage', 'unhurtMaxDamage', 'unhurtGainPerSecond',
    'hurtDamage', 'hurtSeconds', 'nearbyRadius', 'aloneDamage', 'crowdDamage', 'frontWeaponDamage',
    'backIncoming', 'everyAttack', 'everyAttackDamage', 'otherAttackDamage', 'switchFirstDamage',
    'repeatAttackDamage', 'leechRatio', 'leechMaxHeal', 'leechCooldown', 'growthStartRatio', 'growthKills',
    'growthStepRatio', 'growthFinalRatio', 'reflectDamageMultiplier', 'projectileIncoming', 'bookmarkSeconds',
)
RULE_FIELDS = (
    'minimumDamageScale', 'minimumIncomingScale', 'minimumMaxHealthScale', 'minimumWalkSpeedScale',
    'minimumBasicSpeedScale', 'maximumSkillCooldownReduction', 'maximumCritChance',
)
BLOCKED = 'excluded_until_durability'


def close(a, b):
    return abs(a - b) < 1e-9


def validate(data):
    assert data['schemaVersion'] == 1 and data['status'] == 'runtime_trial', 'expected the runtime trial catalog'
    cards = data['augments']
    rules = data['runtimeRules']
    for key in RULE_FIELDS:
        assert isinstance(rules[key], (int, float)) and rules[key] > 0, f'runtimeRules.{key}'
    assert rules['stacking'] == 'additive_within_stat'
    # Augments are never sold (user decision 2026-11-13): a boss clear and the seal reward chest each give a free
    # choice from the same cards.
    assert rules['acquisition'] == 'boss_clear_and_seal_reward_chest'
    assert rules['bossReward'] == 'free_offer_after_boss_weapon_choice_when_next_chapter'
    assert rules['sealRewardChest'] == 'free_offer_from_same_pool'
    assert not {'priceTrack', 'maxPicksSource', 'maintenancePrices', 'maintenancePicksPerStage'} & set(rules), 'augments carry no price'
    blocked = sorted(c['id'] for c in cards if c['runtime'] == BLOCKED)
    assert blocked == sorted(rules['excludedUntilDurability']), 'excluded list disagrees with the cards'
    implemented = [c for c in cards if c['runtime'] == 'implemented']
    assert len(implemented) + len(blocked) == len(cards), 'every card is implemented or excluded'
    for card in cards:
        if card['runtime'] == BLOCKED:
            assert 'effect' not in card, f"{card['id']}: an excluded card carries no runtime effect"
            continue
        effect = card['effect']
        assert effect and set(effect) <= set(EFFECT_FIELDS), f"{card['id']}: unknown effect field {set(effect) - set(EFFECT_FIELDS)}"
        for key, value in effect.items():
            assert isinstance(value, (int, float)) and value == value, f"{card['id']}.{key}"
        if card['group'] == 'safe':
            # Stable cards keep the common damage step and carry no drawback.
            assert effect.get('damage', 0) >= .12 - 1e-9, f"{card['id']}: stable cards keep the common 12% damage"
            assert all(value >= 0 or key == 'incoming' for key, value in effect.items()), f"{card['id']}: stable cards have no drawback"
            assert effect.get('incoming', 0) <= 0, f"{card['id']}: stable cards cannot raise damage taken"
    stable = [c for c in implemented if c['group'] == 'safe']
    risk_groups = {c['group'] for c in implemented if c['group'] != 'safe'}
    assert stable and len(risk_groups) >= 2, 'one stable and two different risk groups must stay drawable'
    # Numbers already fixed in numericRules must match the runtime effect.
    by_id = {c['id']: c.get('effect', {}) for c in cards}
    nr = data['numericRules']
    assert close(by_id['C14']['growthStartRatio'], nr['C14']['initialMaxHealthRatio'])
    assert close(by_id['C14']['growthStepRatio'], nr['C14']['maxHealthRatioGainPerMilestone'])
    assert close(by_id['C14']['growthFinalRatio'], nr['C14']['finalMaxHealthRatio'])
    assert by_id['C14']['growthKills'] == nr['C14']['killsPerMilestone']
    assert close(1 + by_id['C13']['maxHealth'], nr['C13']['maxHealthMultiplier'])
    assert close(1 + by_id['C13']['healing'], nr['C13']['healingReceivedMultiplier'])
    assert close(1 + by_id['C02']['maxHealth'], nr['C02']['maxHealthMultiplier'])
    assert close(1 + by_id['C02']['incoming'], nr['C02']['damageReceivedMultiplier'])
    assert close(by_id['C02']['leechRatio'], nr['C02']['damageToHealRatio'])
    assert by_id['C02']['leechMaxHeal'] == nr['C02']['maxHealPerProc']
    assert by_id['C02']['leechCooldown'] == nr['C02']['internalCooldownSeconds']
    assert close(by_id['C04']['unhurtMaxDamage'], nr['C04']['maxDamageBonus'])
    assert close(1 + by_id['C04']['hurtDamage'], nr['C04']['afterHitDamageMultiplier'])
    assert by_id['C04']['hurtSeconds'] == nr['C04']['afterHitPenaltySeconds']
    assert close(by_id['C04']['unhurtGainPerSecond'], nr['C04']['bonusGainRate'])
    assert close(1 + by_id['M02']['incoming'], nr['M02']['damageReceivedMultiplier'])
    assert close(1 + by_id['M02']['walkSpeed'], nr['M02']['walkSpeedMultiplier'])
    assert close(1 + by_id['W08']['switchFirstDamage'], nr['W08']['firstAttackAfterAlternatingMultiplier'])
    assert close(1 + by_id['W08']['repeatAttackDamage'], nr['W08']['consecutiveAttackMultiplier'])
    return data


def catalog(data):
    rules = data['runtimeRules']
    cards = []
    for card in data['augments']:
        if card['runtime'] != 'implemented':
            continue
        entry = {key: card.get(key, '') for key in ('id', 'group', 'name', 'gain', 'cost', 'play', 'note')}
        entry['effect'] = {key: float(card['effect'].get(key, 0)) for key in EFFECT_FIELDS}
        cards.append(entry)
    result = {
        'schema': 1,
        'offeredCards': data['selection']['offeredCards'],
        'stableCards': data['selection']['stableCards'],
        'riskCards': data['selection']['riskCards'],
    }
    result.update({key: float(rules[key]) for key in RULE_FIELDS})
    result['groups'] = [{'id': key, 'name': value} for key, value in data['groups'].items()]
    result['augments'] = cards
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='fail when the Unity catalog differs from the source')
    args = parser.parse_args()
    data = validate(json.loads(SOURCE.read_text(encoding='utf-8')))
    result = catalog(data)
    text = json.dumps(result, ensure_ascii=False, indent=2) + '\n'
    if args.check:
        current = OUTPUT.read_bytes().decode('utf-8') if OUTPUT.exists() else ''
        if current != text:
            print(f'{OUTPUT.relative_to(ROOT)} is out of date; run without --check', file=sys.stderr)
            return 1
        print(f"maintenance augments up to date ({len(result['augments'])} implemented)")
        return 0
    OUTPUT.write_bytes(text.encode('utf-8'))
    print(f"wrote {OUTPUT.relative_to(ROOT)} ({len(result['augments'])} augments)")
    return 0


if __name__ == '__main__':
    sys.exit(main())
