#!/usr/bin/env python3
"""Project the approved sketch-weapon/39-ink design into a Unity-friendly catalog."""
import argparse
import json
from pathlib import Path
from build_ink_display_text import description, document

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/weapon_ink_run_design.json'
TARGET = ROOT / 'unity/Assets/Deviant/Combat/Resources/RunInks.json'


def catalog(data):
    inks = data['inks']
    stacking = inks['effectStacking']
    cap = stacking['maximumContributingCopiesPerEffect']
    if type(cap) is not int or cap != 6 or stacking['excludedEffects'] != ['heal', 'shield']:
        raise ValueError('Ink contribution cap must be six actual copies, excluding healing and shields')
    document(inks)  # Also validates the complete set of 39 combinations and unique names.
    icons = inks['presentation']['iconsByColor']
    if set(icons) != {entry['color'] for entry in inks['presentation']['entries']} or not all(isinstance(path, str) and path.strip() for path in icons.values()):
        raise ValueError('Ink icons must cover all five colors with nonempty resource paths')
    uses = inks['applicationUses']['perAcquisition']
    if type(uses) is not int or uses <= 0:
        raise ValueError('Ink applications per acquisition must be a positive integer')
    effects = inks['effectGranting']
    statuses = inks['statusConditions']
    rows = []
    for entry in inks['presentation']['entries']:
        effect, trigger = entry['effect'], entry['trigger']
        duration = interval = 0
        if effect == 'movement_speed_increase':
            spec = effects['movementSpeedStacking']['effectValues']
            values = spec['increaseRate']
            duration = spec['durationSecondsByTrigger'][trigger]
        elif effect == 'shield':
            values = effects['shieldLifetimeAndCap']['generationAmount']['perInkBaseAndEnhancedValues'][trigger]
        elif effect == 'heal':
            values = effects['healAmount']
        elif effect in ('stun', 'root'):
            values = statuses['hardControlReapplication']['perInkDurationSeconds'][effect]
        elif effect == 'slow':
            values = statuses['slowEffect']['movementReductionRate']
            duration = statuses['slowEffect']['durationSecondsByTrigger'][trigger]
        elif effect == 'basic_attack_damage_increase':
            spec = inks['attackGranting']
            values = spec['buff']['attackPowerIncrease']
            duration = spec['buff']['durationSeconds']
        elif effect == 'basic_attack_speed_increase':
            spec = inks['attackGranting']['attackSpeedBuff']
            values = spec['increaseRate']
            duration = spec['durationSecondsPerEffectiveCopy']
        elif effect == 'cooldown_reduction':
            spec = inks['skillGranting']['effects']['cooldownReduction']['reductionAmountAndMode']
            values = dict(base=spec['baseFraction'], enhanced=spec['enhancedFraction'])
        elif effect == 'skill_attack_damage_increase':
            values = inks['skillGranting']['effects']['skillDamageIncrease']['increaseAmountAndMode']
        else:
            raise ValueError(effect)
        cooldown = (inks['attackGranting']['cooldown']['seconds'] if entry['color'] == 'red' else
                    inks['skillGranting']['internalCooldownRules']['seconds'] if entry['color'] == 'blue' else
                    effects['internalCooldown']['perEffectAndTriggerSeconds'][effect][trigger])
        key = '_'.join(filter(None, (entry['color'], trigger, effect, entry.get('condition'))))
        rows.append(dict(id=key, name=entry['name'], description=description(inks, entry),
                         color=entry['color'], trigger=trigger, effect=effect, condition=entry.get('condition', ''),
                         baseValue=values['base'], enhancedValue=values['enhanced'], duration=duration,
                         interval=interval, cooldown=cooldown, icon=icons[entry['color']]))
    shield = effects['shieldLifetimeAndCap']
    result = dict(schema=2, maximumContributingInkCopies=cap, damageUnitScale=data['combatBalanceTrial']['damageScale']['factor'], keyCarrierHp=data['combatBalanceTrial']['keyCarrierHp'], investment=inks['duplicateInvestment'], inks=rows, inkUsesPerAcquisition=uses, durabilityBase=data['durability']['maximumBase'],
                  durabilityPerSlot=data['durability']['maximumPerSlot'],
                  equipmentChangeCooldownSeconds=data['inventory']['equipmentChangeCooldownSeconds'],
                  moveCap=effects['movementSpeedStacking']['cap']['maximumIncreaseRate'],
                  shieldCap=shield['cap']['fraction'], shieldGrace=shield['decay']['graceSeconds'],
                  shieldDecay=shield['decay']['rate']['fractionPerSecond'],
                  dashShieldGrace=shield['dashShield']['graceAfterDashEndSeconds'],
                  dashShieldDecay=shield['dashShield']['decayRate']['fractionPerSecond'],
                  controlCap=statuses['hardControlReapplication']['sameInkDuplicateApplication']['durationCap']['seconds'],
                  stages=[])
    speed = inks['attackGranting']['attackSpeedBuff']
    result.update(basicAttackSpeedCap=speed['increaseCap'], basicAttackSpeedDurationCap=speed['durationCapSeconds'], maximumBasicSpeedScale=speed['maximumBasicSpeedScale'])
    gauge = data['rewards']['gauge']
    tiers = gauge['stageTiers']['1']
    result.update(gaugeBase=gauge['perReward'], gaugeStep=gauge['blocks']['requirement']['increasePerBlock'],
                  smallBelow=tiers['small']['maxHpBelow'], largeFrom=tiers['large']['maxHpFrom'],
                  smallGain=tiers['small']['gain'], mediumGain=tiers['medium']['gain'], largeGain=tiers['large']['gain'])
    # Opening rewards (user decision 2026-11-13): the run's 1st and 2nd rewards need 15 and 48, the 1st offers three
    # weapons, the 2nd and every 4th from the 6th give an ink choice and then a weapon choice.
    opening = gauge['openingRequirements']['values']
    assert opening and all(isinstance(v, (int, float)) and 0 < v for v in opening), 'opening requirements must be positive'
    offers = data['rewards']['openingOffers']
    assert offers['first'] == 'ink_three_then_weapon_three' and offers['second'] == 'ink_three_then_weapon_three', 'unsupported opening offers'
    double = data['rewards']['doubleOffers']
    assert double['kind'] == 'ink_three_then_weapon_three' and double['fromRunReward'] > 2 and double['everyRunRewards'] > 0, 'double offer rule'
    mixed = data['rewards']['mixedOffersFrom']
    assert mixed['runReward'] == 3 and mixed['minimumUnregisteredInk'] == 1 and mixed['minimumWeapons'] == 1, 'mixed offer rule'
    result.update(openingRequirements=[float(v) for v in opening], openingOffers=True,
                  doubleFrom=double['fromRunReward'], doubleEvery=double['everyRunRewards'])
    for stage in ('1', '2', '3'):
        price = data['rewards']['shop']['prices']['byStage'][stage]
        chance = data['blueprints']['enhancedSlotChancePerSlot']['byStage'][stage]
        result['stages'].append(dict(stage=int(stage), offerSlots=data['blueprints']['normalRewardSlotCountsByOffer'][stage],
                                    enhancementKnown=chance is not None, enhancedChance=chance or 0,
                                    pricesKnown=price is not None, weaponBase=price['base'] if price else 0,
                                    perSlot=price['perSlot'] if price else 0, perEnhanced=price['perEnhancedSlot'] if price else 0,
                                    inkPrice=price['ink'] if price else 0))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    content = json.dumps(catalog(json.loads(SOURCE.read_text())), ensure_ascii=False, indent=2) + '\n'
    if args.check:
        if not TARGET.exists() or TARGET.read_text() != content:
            raise SystemExit('RunInks.json is stale; run tools/combat/export_run_inks.py')
    else:
        TARGET.write_text(content)
    print('PASS: 39 approved inks and sketch weapon rules ' + ('match Unity catalog' if args.check else 'exported'))
