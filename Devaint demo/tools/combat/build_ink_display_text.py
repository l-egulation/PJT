#!/usr/bin/env python3
"""Generate the approved ink text reference from design templates and numeric values."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'data/combat/weapon_ink_run_design.json'
DOC = ROOT / 'docs/design/equipment/inks/names-and-descriptions.md'
TRIGGERS = {
    'basic_attack': '기본 공격 시', 'skill_attack': '스킬 공격 시',
    'weapon_switch': '스위칭 공격 시', 'dash_use': '대쉬 시',
    'status_condition': '이 무기로 상태이상 부여 시',
}
CONDITIONS = {'stun': '기절한', 'root': '속박된', 'slow': '슬로우 상태인'}
GROUPS = {
    'movement_speed_increase': '노랑 — 이동속도 증가',
    'shield': '초록 — 방어막', 'heal': '초록 — 체력 회복',
    'stun': '보라 — 기절', 'root': '보라 — 속박',
    'slow': '보라 — 슬로우',
    'basic_attack_damage_increase': '빨강 — 기본 공격 강화',
    'basic_attack_speed_increase': '빨강 — 기본 공격 공속 증가',
    'cooldown_reduction': '파랑 — 스킬 쿨타임 감소',
    'skill_attack_damage_increase': '파랑 — 스킬 피해 강화',
}


def description(inks, entry):
    effect, trigger = entry['effect'], entry['trigger']
    effects, states = inks['effectGranting'], inks['statusConditions']
    params = {
        'stackCap': inks['effectStacking']['maximumContributingCopiesPerEffect'],
        'triggerText': '대쉬 경로에 적이 닿으면' if trigger == 'dash_use' and effect == 'heal' else TRIGGERS[trigger],
        'targetText': '경로에 닿은 적을' if trigger == 'dash_use' else '적을',
        'targetPossessive': '경로에 닿은 적의' if trigger == 'dash_use' else '적의',
    }
    if trigger in {'basic_attack', 'skill_attack'} and effect in {'heal', 'shield'}:
        params['triggerText'] = ('기본 공격' if trigger == 'basic_attack' else '스킬 공격') + '으로 몬스터에게 피해를 입힐 시'
    if entry['color'] in {'red', 'blue'}:
        params['conditionText'] = CONDITIONS[entry['condition']]
        if entry['color'] == 'red':
            red = inks['attackGranting']
            if effect == 'basic_attack_speed_increase':
                speed = red['attackSpeedBuff']
                values = speed['increaseRate']
                duration = speed['durationSecondsPerEffectiveCopy']
                enhanced_duration = min(speed['durationCapSeconds'], duration * inks['duplicateInvestment']['enhancedContribution'])
            else:
                values = red['buff']['attackPowerIncrease']
                duration = red['buff']['durationSeconds']
                enhanced_duration = duration * inks['duplicateInvestment']['enhancedContribution']
            params.update(duration=duration, enhancedDuration=enhanced_duration, cooldown=red['cooldown']['seconds'])
        else:
            blue = inks['skillGranting']
            key = 'cooldownReduction' if effect == 'cooldown_reduction' else 'skillDamageIncrease'
            values = blue['effects'][key]['reductionAmountAndMode' if key == 'cooldownReduction' else 'increaseAmountAndMode']
            if key == 'cooldownReduction':
                values = {'base': values['baseFraction'], 'enhanced': values['enhancedFraction']}
            params['cooldown'] = blue['internalCooldownRules']['seconds']
        params.update(base=values['base'] * 100, enhanced=values['enhanced'] * 100)
    else:
        params['cooldown'] = effects['internalCooldown']['perEffectAndTriggerSeconds'][effect][trigger]
        if effect == 'movement_speed_increase':
            speed = effects['movementSpeedStacking']['effectValues']
            values = speed['increaseRate']
            params.update(base=values['base'] * 100, enhanced=values['enhanced'] * 100,
                          duration=speed['durationSecondsByTrigger'][trigger])
        elif effect == 'shield':
            values = effects['shieldLifetimeAndCap']['generationAmount']['perInkBaseAndEnhancedValues'][trigger]
            params.update(base=values['base'], enhanced=values['enhanced'])
        elif effect == 'heal':
            values = effects['healAmount']
            params.update(base=values['base'], enhanced=values['enhanced'])
        elif effect in {'stun', 'root'}:
            params.update(states['hardControlReapplication']['perInkDurationSeconds'][effect])
        elif effect == 'slow':
            slow = states['slowEffect']
            rates = slow['movementReductionRate']
            if rates != slow['attackMotionReductionRate']:
                raise ValueError('The approved speed description requires equal movement and attack slow rates')
            params.update(base=rates['base'] * 100, enhanced=rates['enhanced'] * 100,
                          duration=slow['durationSecondsByTrigger'][trigger])
    if entry['color'] == 'purple':
        investment = inks['duplicateInvestment']
        window = investment['controlBaseWindowSeconds']
        enhanced_extra = investment['enhancedContribution'] - 1
        enhanced_window = (window + enhanced_extra * (investment['slowTwoCopiesWindowSeconds'] - window) if effect == 'slow'
                           else investment['controlEnhancedWindowSeconds'])
        cooldown_at_one = investment['slowCooldownAtOne' if effect == 'slow' else 'controlCooldownAtOne']
        floor = investment['slowCooldownFloor' if effect == 'slow' else 'controlCooldownFloor']
        decrement = cooldown_at_one - floor if effect == 'slow' else 1
        params.update(window=window, enhancedWindow=enhanced_window,
                      enhancedCooldown=max(floor, cooldown_at_one - enhanced_extra * decrement),
                      activationText='대쉬 시작 시' if trigger == 'dash_use' else TRIGGERS[trigger].replace(' 시', ' 적중 시'),
                      applicationText=('대쉬 경로의 적에게'
                                       if trigger == 'dash_use' else '이 무기의 기본·스킬 적중마다'))
    params = {key: f'{value:g}' if isinstance(value, (int, float)) else value for key, value in params.items()}
    return inks['presentation']['descriptionTemplates'][effect].format(**params)


def document(inks):
    entries = inks['presentation']['entries']
    expected = {(color, row['trigger'], row['effect'], None)
                for row in inks['effectGranting']['requiredCombinations']
                for color, effects in inks['effectGranting']['colors'].items() if row['effect'] in effects}
    expected |= {('red', 'basic_attack', effect, state) for state in CONDITIONS
                 for effect in ('basic_attack_damage_increase', 'basic_attack_speed_increase')}
    expected |= {('blue', 'skill_attack', row['effect'], row['condition'])
                 for row in inks['skillGranting']['requiredCombinations']}
    # Blue policy uses camelCase effect keys; text uses the underlying effect identifiers.
    expected = {(color, trigger, inks['skillGranting']['effects'][effect]['effect'] if color == 'blue' else effect, condition)
                for color, trigger, effect, condition in expected}
    keys = [(e['color'], e['trigger'], e['effect'], e.get('condition')) for e in entries]
    if len(keys) != 39 or len(set(keys)) != 39 or set(keys) != expected:
        raise ValueError('Display entries must cover all 39 standard inks exactly once')
    if len({entry['name'] for entry in entries}) != 39:
        raise ValueError('Ink display names must be unique')
    lines = ['# 잉크 이름과 설명', '', '[잉크 개요](README.md) · [표시 기준](rules.md#이름과-플레이어-설명)', '',
             '기본 39종의 확정 표시 문구다. 편집 원본은 `data/combat/weapon_ink_run_design.json`의 `inks.presentation`이며 수치는 같은 데이터의 효과별 기본·강화 값을 사용한다. `python3 tools/combat/build_ink_display_text.py`로 생성하고 `--check`로 일치를 검사한다. `export_run_inks.py`가 같은 문구를 Unity 런 도감·보상·상점에 연결한다. 잉크 자체의 색에 따라 기본 5색 아이콘을 사용하며 매핑은 같은 원본의 `inks.presentation.iconsByColor`를 따른다.', '',
             '괄호 안은 색 일치 강화 수치다. 파랑의 쿨타임 감소량은 전체 기본 스킬 쿨타임 기준이다.', '',
             '표시 문구의 간결화는 실제 발동 조건을 바꾸지 않는다. 실행·적중의 구분, 방어막 감소, 슬로우의 이동·공격 동작 감속, 빨강의 기본 공격 전용 범위, 파랑의 1타 사전 강화와 대상별 상태 조건·적용 수명은 [발동 조건](triggers.md)과 [효과 규칙](effects.md)을 따른다.']
    previous = None
    for number, entry in enumerate(entries, 1):
        group = GROUPS[entry['effect']]
        if group != previous:
            lines.extend(['', f'## {group}', ''])
            previous = group
        lines.extend([f"{number}. **{entry['name']}**\\", f"   {description(inks, entry)}", ''])
    return '\n'.join(lines).rstrip() + '\n'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    content = document(json.loads(SOURCE.read_text())['inks'])
    if args.check:
        if not DOC.exists() or DOC.read_text() != content:
            raise SystemExit('Stale ink display text; regenerate with this tool.')
    else:
        DOC.write_text(content)
    print('PASS: 39 unique ink names, complete combinations, structured numeric values and ' +
          ('document matches' if args.check else 'document generated'))
