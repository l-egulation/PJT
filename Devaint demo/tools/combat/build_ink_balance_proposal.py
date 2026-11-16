#!/usr/bin/env python3
"""Generate ink calibration inputs and confirmed policies without writing runtime assets."""
import argparse
import ast
import hashlib
import itertools
import json
from pathlib import Path
import re
import statistics
from melee_source import load_melee

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'docs/design/equipment/inks/balance-proposal.md'
TRIGGERS = {'basic_attack':'기본 공격','skill_attack':'스킬 공격','weapon_switch':'스위칭','dash_use':'대쉬','status_condition':'상태이상 부여'}
EFFECTS = {'movement_speed_increase':'이속 증가','shield':'방어막 생성','heal':'체력 회복','stun':'기절','root':'속박','slow':'슬로우'}
CONDITIONS = {'stun':'기절 시','root':'속박 시','slow':'슬로우 시'}
INPUTS = ['data/combat/ink_content_balance_proposal.json','data/combat/weapon_ink_run_design.json','data/combat/boss_status_design.json','data/map/block_rooms.json','data/combat/stage1_village_monsters.json','data/combat/chapter1_new_monsters.json','data/combat/clock_executioner_timing.json','data/combat/melee_weapon_design.json','data/combat/actions/base_sword_basic_0.json','data/combat/ranged_basic_design.json','data/combat/unarmed_basic.json','unity/Assets/ForestCombatV2/Runtime/ClockExecutionerBoss.cs','unity/Assets/ForestCombatV2/Runtime/ClockExecutionerBoss.ChainSlash.cs','docs/overview/overview.md']

def read(name): return json.loads((ROOT/name).read_text())
def number_expr(expr, values):
    node=ast.parse(re.sub(r'(?<=\d)f\b','',expr.strip()),mode='eval').body
    def run(n):
        if isinstance(n,ast.Constant) and isinstance(n.value,(int,float)):return n.value
        if isinstance(n,ast.Name):return values[n.id]
        if isinstance(n,ast.BinOp):
            a,b=run(n.left),run(n.right)
            if isinstance(n.op,ast.Add):return a+b
            if isinstance(n.op,ast.Sub):return a-b
            if isinstance(n.op,ast.Mult):return a*b
            if isinstance(n.op,ast.Div):return a/b
        raise ValueError(expr)
    return run(node)

def constants(text):
    expressions={}
    for block in re.findall(r'public const (?:float|int)\s+([^;]+);',text):
        for part in block.split(','):
            if '=' in part:
                name,expr=part.split('=',1);expressions[name.strip()]=expr
    result={}
    for _ in range(len(expressions)):
        changed=False
        for name,expr in expressions.items():
            if name in result:continue
            try:result[name]=number_expr(expr,result);changed=True
            except (KeyError,ValueError,SyntaxError):pass
        if not changed:break
    return result

def model():
    p=read('data/combat/ink_content_balance_proposal.json');a=p['assumptions']
    village=read('data/combat/stage1_village_monsters.json')['monsters']
    monsters={m['id']:{'hp':m['maxHP'],'damage':m['damage'],'interval':m['interval'],'prepare':m['prepare']} for m in village}
    for m in read('data/combat/chapter1_new_monsters.json')['monsters']:
        b=m['behavior'];monsters[m['id']]={'hp':b['hp'],'damage':b['damage'],'interval':b['interval'],'prepare':b.get('telegraphSeconds',b.get('inflationSeconds'))}
    mapping=read('data/combat/stage1_village_monsters.json')['legacyMap'];aliases={m['from']:m['to'] for m in mapping}
    blocks=read('data/map/block_rooms.json');species=blocks['species']['s1'];room_rows=[]
    for r in blocks['rooms']:
        if r['stage']!=1 or r['kind']!='combat':continue
        ids=[aliases.get(species[e['t']],species[e['t']]) for e in r['enemies']]
        room_rows.append({'id':r['id'],'count':len(ids),'hp':sum(monsters[s]['hp'] for s in ids)})
    subsets=[{'count':sum(r['count'] for r in group),'hp':sum(r['hp'] for r in group)} for group in itertools.combinations(room_rows,8)]
    assert len(subsets)==495 and len(room_rows)==12
    melee=load_melee();weapons=[]
    for f in melee['forms']:
        b=f['basic'];weapons.append({'id':f['id'],'name':f['name'],'dps':sum(b['damages'])/sum(b['motion_durations'])})
    ranged=read('data/combat/ranged_basic_design.json')
    for id,name in [('launcher','발사기'),('shuriken','수리검'),('bomb','폭탄')]:
        r=ranged[id];dps=sum(r['damages'])/sum(r['motion_durations']) if id=='shuriken' else r['damage']/(r['windup']+r['recovery'])
        weapons.append({'id':id,'name':name,'dps':dps})
    hands=read('data/combat/unarmed_basic.json')['form']['basics'];weapons.insert(0,{'id':'unarmed','name':'맨손','dps':sum(v['damage'] for v in hands)/sum(v['duration'] for v in hands)})
    code=(ROOT/'unity/Assets/ForestCombatV2/Runtime/ClockExecutionerBoss.cs').read_text()
    chain=(ROOT/'unity/Assets/ForestCombatV2/Runtime/ClockExecutionerBoss.ChainSlash.cs').read_text()
    c=constants(code);c.update(constants(chain));timing=read('data/combat/clock_executioner_timing.json')
    gap=float(re.search(r'float PatternGap=>FinalPhase\?combatTiming.finalPhasePatternGapSeconds:([\d.]+)',code)[1])
    recovery=c['SpecialRecovery'];safe=recovery+gap
    # Chain count is uniform 5..7, so 6 is its steady-state mean. Fixed real-time hold is not divided by phase speed.
    patterns={'긴 주먹':c['LongFistDuration']+gap,'시계침':c['NeedleDuration']+gap,'믹서기':c['MixerDuration']+gap,'사슬 긋기':c['ChainLead']+5*c['ChainInterval']+c['ChainImpact']+timing['chainHoldSeconds']+c['ChainRetract']+safe}
    phase_hp=[timing['enrageDamage'],timing['maxHealth']-timing['enrageDamage']-timing['finalPhaseHealth'],timing['finalPhaseHealth']]
    speeds=[c['DefaultPhaseOneSpeed'],c['PhaseTwoSpeed'],timing['finalPhaseSpeedMultiplier']]
    overview=(ROOT/'docs/overview/overview.md').read_text()
    match=re.search(r'전체 목표 플레이 시간 (\d+)~(\d+)분',overview)
    run_bounds=[int(match[1]),int(match[2])]
    chapter_seconds=statistics.mean(run_bounds)*60/3
    inks=read('data/combat/weapon_ink_run_design.json')['inks']
    return {
        'assumptions': a,
        'sources': {f:hashlib.sha256((ROOT/f).read_bytes()).hexdigest() for f in INPUTS},
        'conditions': inks['statusConditions'],
        'contentCounts': inks['contentCounts'],
        'uptimeGoal': inks['sustainedEffectGoal'],
        'requiredCombinations': inks['effectGranting']['requiredCombinations'],
        'effectTriggerRules': inks['effectGranting']['triggerRules'],
        'shieldGenerationValues': inks['effectGranting']['shieldLifetimeAndCap']['generationAmount']['perInkBaseAndEnhancedValues'],
        'effectCooldownSeconds': inks['effectGranting']['internalCooldown']['perEffectAndTriggerSeconds'],
        'healAmount': inks['effectGranting']['healAmount'],
        'movementSpeedValues': inks['effectGranting']['movementSpeedStacking']['effectValues'],
        'rarity': inks['rarity'],
        'redConditionRarities': inks['attackGranting']['conditionVariantRarities'],
        'red': inks['attackGranting'],
        'blue': inks['skillGranting'],
        'roomRows': room_rows,
        'stageEnvelope': {k:[min(v[k] for v in subsets),max(v[k] for v in subsets)] for k in ['count','hp']},
        'monsters': monsters,
        'weapons': weapons,
        'boss': {'hp':timing['maxHealth'],'phaseHp':phase_hp,'phaseSpeeds':speeds,'phaseTwoPatternCycles':patterns},
        'timeBudgetAssumptions': {
            'runMinutes':run_bounds,
            'chapterSeconds':chapter_seconds,
            'referenceRoomSeconds':chapter_seconds*a['normalRoomsBudgetShare']/8,
            'referenceBossSeconds':chapter_seconds*a['bossBudgetShare'],
        },
    }


def document(m):
    names={tier['id']:tier['name'] for tier in m['rarity']['tiers']}
    lines=[
        '# 잉크 주기·상태 조건 산정 기준', '',
        '[잉크 개요](README.md) · [밸런스 원칙](balance-principles.md)', '',
        '이 문서는 `tools/combat/build_ink_balance_proposal.py`가 기획 데이터와 현행 전투 원본에서 생성한다. Unity 런타임 구현·플레이 검증 완료를 뜻하지 않는다.', '',
        '## 상태 조건과 색별 역할', '',
        '빨강은 기본 공격 피해, 파랑은 스킬 공격과 함께 공격 대상 몬스터의 기절·속박·슬로우 상태 조건을 확인한다. 빨강은 무기와 관계없이 플레이어의 기본 공격 피해만, 파랑의 피해 증가는 스킬 피해만 높인다. 빨강 버프는 교대·원본 무기 가방 보관·소멸 후에도 남은 시간 동안 유지·진행하며 다른 무기와 맨손 기본 공격에 적용한다. 빨강은 내부 쿨타임 없이 매 유효 적중에 원본 무기·동일 잉크의 전체 지속시간을 갱신한다. 파랑에는 쿨타임 감소 유형도 있다. 파랑은 스킬 공격 적중 시 맞힌 몬스터의 상태 조건을 확인해 발동한다. 빗나가거나 적중 대상이 없으면 발동하지 않는다. 빨강·파랑 모두 같은 공격의 상태이상 부여 후 조건을 확인하므로 새로 부여한 상태로도 즉시 발동한다. 상태 부여 → 조건 확인 → 강화 적용 → 피해 계산 순서로 빨강·파랑 모두 발동 타격부터 증가 피해를 적용한다. 파랑 피해 강화는 1타의 상태 부여·조건 확인·강화 적용을 피해 계산 전에 끝내 1타부터 증가 피해를 적용한다. 같은 1타로 슬로우를 부여해도 슬로우 조건 잉크가 준비되어 있으면 기준 피해 100에 기본 +20%를 적용한 120의 피해를 준다(다른 보정 없음). 파랑의 피해 증가는 같은 스킬 사용의 남은 타격까지 유지하고 다음 스킬 사용으로 이어지지 않는다. 서로 다른 파란 잉크의 스킬 피해 증가율은 합산한다. 파란 피해 증가는 피해 계산 시 각 대상이 해당 잉크의 상태 조건을 만족할 때만 적용하며 이미 계산한 피해는 소급 변경하지 않는다.', '',
        '비용·페널티는 없다. 체력·방어막·이동속도·몬스터 상태 시간을 차감하지 않으며 플레이어 반동도 없다. 기본 목록은 잉크마다 한 상태 조건을 가지며 복합 조건 예외형은 미정이다.', '',
        '효과 6개×트리거 5개에서 상태이상 부여→상태이상 부여 3개를 제외해 27개 조합을 정의한다. 현재 정의한 유형의 레어도는 노말이며 기본/강화 수치와 독립이다.', '',
        '보라는 원본 무기 부여 버프 활성 중 매 기본·스킬 적중에 상태를 부여하고 종료 후 재사용 시간을 센다. 강화본은 1.5개분으로 계산한다. 적 상태 시간은 최대 3초, 슬로우 감속은 최대 90%이며 보스는 표시·조건만 유지한다. 중복 공식과 첫 시험 시간은 [효과 규칙](effects.md#원본-무기의-상태이상-부여-버프)을 따른다.', '',
        '서로 다른 노란 잉크와 동일 잉크 중복본의 이동속도 증가율은 장착본별로 합산하고 효과별 지속시간은 독립 관리한다. 같은 장착본 재발동은 증가량 중첩 없이 원래 지속시간으로 갱신한다. 교대 후에도 이동속도 효과를 유지하고 남은 시간은 계속 흐른다. 방어막은 남은 양에 새 양을 더하되 최대 체력의 20% 상한을 두고 서서히 감소한다. 기본/스킬/스위칭 방어막은 적중 시, 상태이상 조건은 부여 성공 시 생성한다. 대쉬 방어막은 시작 즉시 적중 없이 생성하고 더 빨리 사라져 무적 대쉬 직후를 잠시 보호한다. 잉크 이동속도 합산 증가율의 상한은 +40%를 플레이 테스트 시작값으로 사용한다. 모든 원본의 유효 효과 중 기여 상한으로 선택한 최대 6칸을 합산한 뒤 적용하며 영구 이동속도 강화는 별도로 더한다. 노랑 5종의 증가율·지속시간·내부 쿨타임은 아래 표의 플레이 테스트 시작값을 따른다. 기본/강화는 증가율만 다르고 시간 수치는 같으며 쿨타임은 효과 발동부터 진행한다. 일반 방어막은 마지막 직접 적중 후 10초부터 감소하며 재적중하면 감소를 멈추고 10초 유예를 갱신한다. 두 무기의 기본/스킬 직접 적중과 발사체·설치물 직접 적중을 인정한다. 일반·대쉬 방어막은 최대 체력 비례 초당 고정량으로 감소하며 일반은 5%/초, 대쉬는 20%/초다. 현재 남은 방어막에 대한 비율이 아니다. 대쉬 방어막은 대쉬 종료 후 3초부터 감소하며 적중으로 유예를 갱신하지 않는다. 두 방어막은 양과 시간을 분리 관리하고 피격 시 대쉬분부터 소모하며 합계에 상한을 적용한다. 여러 대쉬 생성분은 유예 종료 시각이 빠른 순으로 소모하고 대쉬분을 모두 소모한 뒤 일반 방어막을 사용한다. 새 생성분은 상한의 빈 공간만 채우고 초과분은 버린다. 대쉬 방어막은 생성분별로 자신을 만든 대쉬 종료 후 3초 유예를 독립 관리하며 반복 대쉬가 기존 분량의 시간을 갱신하지 않는다. 유예가 끝난 생성분마다 각각 최대 체력의 20%/초로 감소하며 생성분 사이에 감소량을 나누거나 소진 후 남는 감소량을 넘기지 않는다. 아직 유예 중인 생성분은 감소하지 않는다. 상한 20%·일반 유예시간 10초·일반 5%/초 및 대쉬 20%/초 감소량은 플레이 테스트 시작값이며, 런 종료 처리는 미정이다. 무기가 소멸해도 이미 부여한 방어막은 기존 감소 규칙대로 유지한다.', '',
        '회복·방어막 생성을 제외한 같은 효과에는 실제 최대 6칸만 기여한다. 강화도 1칸으로 세며 강화 수치와 E 환산량은 유지한다. 플레이어 지속 버프는 보관·파괴된 원본까지 포함해 효과량이 높은 순, 동률이면 최근 발동 처리 순으로 선택한다. 초과 효과는 원래 시간대로 진행하고 선택분 만료 시 남은 효과만 재선택한다. 원본 귀속과 기존 수치 상한·회복·방어막 규칙은 [효과별 기여 상한](effects.md#효과별-기여-상한)을 따른다.', '',
        '같은 잉크 중복은 도면 슬롯 한도 내에서 허용한다. 빨강은 매 유효 적중에 전체 시간을 갱신하고 보라는 원본 무기·동일 잉크별 부여 버프를 공유한다. 노랑·초록·파랑은 기존 장착본별 쿨타임과 공격당 제한을 유지한다. 도면은 스테이지별 4·5·6칸이다.', '',
        '슬롯의 강화색이 잉크 색과 같으면 강화 수치를 쓴다. 강화 슬롯 최대 2칸은 같은 색이며, 빨강·보라는 일반 1·강화 1.5개분의 중복 환산으로 슬롯을 절약한다. 다른 색 효과는 기존 수치를 유지한다.', '',
        '내구도는 체력이 있는 공격 대상의 실제 감소량만 계산하며 초과 피해는 제외한다. 최대값은 [내구도 명세](../durability.md)의 2,000 + 슬롯 수 × 1,500을 따른다. 소진 타격 피해는 온전히 적용한 뒤 잔여 내구도를 0으로 만들고 무기를 소멸시킨다. 이미 생성된 독립 발사체·설치물은 유지하고 무기 부착 후속 타격은 종료한다. 남은 공격의 피해는 다른 무기에 전가하지 않는다. 광역 소진 타격은 같은 타격의 모든 대상에게 피해를 적용한 뒤 소멸 처리한다. 남은 독립 발사체·설치물도 원본 잉크 규칙대로 새 발동할 수 있으며 원본 쿨타임과 공격당 발동 기록을 유지하고 초기화하지 않는다.', '',
        '스케치 무기는 인벤토리에서 구성을 확정해 사용할 수 있으며 전투 중에도 구성 가능하고 전투와 타이머는 일시정지한다. 별도 제작 비용은 없으며 확정 시 슬롯당 잉크 1회를 소비한다. 구성을 확정해야 활성화되며 빈 슬롯을 포함한 전체 구성이 고정된다. 확정 후에는 빈 슬롯 추가·잉크 교체·해제·이동을 할 수 없다. 장착하지 않고 가방에 보관할 수 있으며 장착 무기를 교체하면 원래 무기는 가방으로 돌아간다. 구체적인 입력·교체 대기시간은 [장착 규칙](../weapons/rules.md)을 따른다.', '',
        '스케치 무기와 잉크는 독립된 보유 항목이며 수량 제한 없이 보관한다. 같은 무기·스킬 조합을 다시 획득할 수 있고 잉크 도감의 재제공 금지를 무기에 적용하지 않는다.', '',
        '공격 조건 보라는 첫 유효 적중에, 대쉬 조건은 시작에 활성화하며 첫 타격부터 적용한다. 활성 버프는 재적중으로 연장하지 않는다. 회복·방어막은 실제 증가량이 양수일 때만 기존 쿨타임과 공격당 기회를 사용한다.', '',
        '회복량과 방어막 생성량은 최대 체력과 무관한 고정 수치이며 잉크별 기본/강화 값을 정한다. 방어막 5종은 아래 표의 생성량을 발동 순간 즉시 지급하고 장착본별 내부 쿨타임을 기다린다. 중복 장착본은 준비된 경우 함께 발동하며 자동으로 번갈아 발동하지 않는다. 확정 수치는 플레이 테스트 시작값이며 대쉬 방어막도 실제 생성량이 양수일 때만 해당 장착본의 쿨타임을 사용한다. 방어막 보유 상한과 감소량의 최대 체력 비례 규칙은 유지한다. 회복 잉크도 기본/스킬 공격·스위칭·상태이상 조건은 6초, 대쉬 조건은 8초의 장착본별 내부 쿨타임을 기본/강화 공통으로 사용한다. 실제 회복량이 양수일 때만 시작한다. 회복 5종은 아래 표의 기본/강화 고정 체력 수치를 장착본마다 회복하며 최대 체력 초과분을 버린다.', '',
        '## 전체 잉크 개수', '',
        f'현재 기본 목록은 총 **{m["contentCounts"]["total"]}개**다. 기본/강화는 같은 잉크의 두 상태이며 미채택 예외 후보는 개수에 포함하지 않는다.', '',
        '| 분류 | 개수 |', '|---|---:|',
        *[f'| {label} | {m["contentCounts"]["byColor"][key]} |' for key,label in [('yellow','노랑'),('green','초록'),('purple','보라'),('red','빨강'),('blue','파랑')]],
        f'| 합계 | **{m["contentCounts"]["total"]}** |', '',
        '효과 부여 27개 + 빨강의 효과 2개×상태 조건 3개 + 파랑의 효과 2개×상태 조건 3개 = 39개다.', '',
        '## 효과 잉크의 발동 세부', '',
        '스위칭은 직전 공격이 다른 무기였을 때 들어온 무기의 잉크로 발동하며 상태이상은 해당 공격의 적중 대상에게 부여한다. 이동속도·회복은 조건을 만족한 공격 실행 시 적중 없이 적용하고 방어막은 적중 시 생성한다. 무기 교체 입력 자체로 발동하지 않는다. 런의 첫 공격은 스위칭이 아니며, 실제 실행한 공격은 빗나가도 이력을 갱신한다. 실행 전 취소는 기존 기록을 유지하고 스위칭 여부는 현재 공격의 이력 갱신 전에 판정한다.', '',
        '대쉬는 시작 무기의 준비된 잉크를 사용한다. 이동속도·방어막은 시작 즉시 적용하고 회복은 대쉬 경로 판정이 살아 있는 적에게 닿을 때 장착본별 대쉬당 최대 한 번 적용한다. 적 접촉이 없으면 회복과 회복 쿨타임 소모가 없으며 실제 회복량이 양수일 때만 쿨타임을 시작한다. 보라 부여 버프를 시작에 켜고 유효 경로의 대상별 한 번 적용한다. 활성 중 원본 무기의 후속 기본·스킬 적중에도 상태를 부여한다. 파괴·교대 시 원본 구성과 남은 시간을 유지한다.', '',
        '기본/스킬 공격 조건의 이동속도는 공격 실행 시 발동하며 빗나가도 적용하고 실행 전 취소에는 발동하지 않는다. 회복·방어막은 몬스터의 실제 체력을 줄인 뒤 발동하며 처치 타격도 인정한다. 빗나감·피해 0·체력이 줄지 않는 무적/방어 타격·플레이어·미끼 타격은 발동과 쿨타임을 소비하지 않는다. 적에게 부여하는 상태이상은 실제 적중 대상이 필요하다. 모든 잉크는 공격 실행당 잉크별 최대 1회다. 한 번의 상태 부여에 여러 대상이 포함될 수 있다. 공격 기반 보라 잉크는 같은 공격의 모든 적중 대상에게 각각 한 번 적용하고 늦게 처음 맞은 대상도 포함하되 동일 대상 재적중으로는 갱신하지 않는다. 대쉬 상태이상은 이동 중 접촉 즉시 대상당 대쉬 1회에 한 번 적용한다. 경로 판정은 잔류하지 않고 재접촉으로 재부여하지 않는다. 대쉬 중단 시 새 접촉 판정을 멈추되 기존 상태는 유지한다. 상태이상 판정은 캐릭터 피격 범위를 좌우·위아래로 각각 0.25unit 확장한 영역을 기준으로 하며 플레이어 피격 범위 자체는 변경하지 않는다. 원본 무기 교체·소멸만으로 대쉬 이동·무적이나 기존 경로 효과를 중단하지 않고 종료까지 유지한다. 새 무기의 잉크는 추가 발동하지 않으며 원본 쿨타임과 대상별 적용 기록을 보존한다. 별도 사유로 대쉬가 중단되는 경우에는 기존 중단 규칙을 따른다.', '',
        '## 필수 효과 조합', '',
        '| 트리거 | 효과 | 레어도 | 기본 / 강화 | 지속시간 / 쿨타임 |',
        '|---|---|---|---|---|',
    ]
    for p in m['requiredCombinations']:
        values='미정 / 미정';timing='미정 / 미정'
        if p['effect']=='shield':
            amount=m['shieldGenerationValues'][p['trigger']]
            cooldown=m['effectCooldownSeconds']['shield'][p['trigger']]
            values=f"{amount['base']} / {amount['enhanced']}" if amount is not None else values
            timing=f"방어막 감소 규칙 / {cooldown}초" if cooldown is not None else '방어막 감소 규칙 / 미정'
        elif p['effect']=='heal':
            amount=m['healAmount']
            values=f"{amount['base']} / {amount['enhanced']}"
            cooldown=m['effectCooldownSeconds']['heal'][p['trigger']]
            timing=f'{"경로 적 접촉 시 회복" if p["trigger"] == "dash_use" else "즉시 회복"} / {cooldown}초'
        elif p['effect']=='movement_speed_increase':
            movement=m['movementSpeedValues'];rates=movement['increaseRate']
            values=f"+{rates['base']*100:g}% / +{rates['enhanced']*100:g}%"
            duration=movement['durationSecondsByTrigger'][p['trigger']]
            cooldown=m['effectCooldownSeconds']['movement_speed_increase'][p['trigger']]
            timing=f'{duration}초 / {cooldown}초'
        elif p['effect'] in ('stun','root'):
            durations=m['conditions']['hardControlReapplication']['perInkDurationSeconds'][p['effect']]
            values=f"{durations['base']:g}초 / {durations['enhanced']:g}초"
            cooldown=m['effectCooldownSeconds'][p['effect']][p['trigger']]
            timing=f'무기 버프·종료 후 쿨타임은 중복 공식 참조 (일반 1개 {cooldown}초)'
        elif p['effect']=='slow':
            slow=m['conditions']['slowEffect']
            movement=slow['movementReductionRate'];attack=slow['attackMotionReductionRate']
            values=f"이동 -{movement['base']*100:g}%·공격 동작 -{attack['base']*100:g}% / 이동 -{movement['enhanced']*100:g}%·공격 동작 -{attack['enhanced']*100:g}%"
            duration=slow['durationSecondsByTrigger'][p['trigger']]
            cooldown=m['effectCooldownSeconds']['slow'][p['trigger']]
            timing=f'대상 {duration}초(강화 1.5초), 무기 버프·쿨타임은 중복 공식 참조'
        lines.append(f'| {TRIGGERS[p["trigger"]]} | {EFFECTS[p["effect"]]} | {names[p["rarity"]]} | {values} | {timing} |')
    lines += ['', '## 빨강의 조건 유형', '', '| 몬스터 상태 조건 | 레어도 | 기본 / 강화 피해 증가율 | 지속시간 / 쿨타임 |', '|---|---|---|---|']
    red=m['red'];rates=red['buff']['attackPowerIncrease']
    lines += [f"| {CONDITIONS[k]} | {names[v]} | +{rates['base']*100:g}% / +{rates['enhanced']*100:g}% | {red['buff']['durationSeconds']}초 / {red['cooldown']['seconds']}초 |" for k,v in m['redConditionRarities'].items()]
    speed=red['attackSpeedBuff']; rates=speed['increaseRate']
    lines += ['', '### 기본 공격 공속 증가', '', '| 몬스터 상태 조건 | 기본 / 강화 | 일반 1개 / 강화 1개 지속 |', '|---|---|---|']
    lines += [f"| {CONDITIONS[k]} | +{rates['base']*100:g}% / +{rates['enhanced']*100:g}% | 1.5초 / 2.25초 |" for k in m['redConditionRarities']]
    lines += ['', '공속 버프는 같은 원본 무기·동일 잉크 환산량 E에 대해 min(3, 1.5×E)초 유지한다. 모든 원본의 활성 공속 중 기여 상한으로 선택한 최대 6칸을 합산해 +60%로 제한한다. 다음 기본 공격 시작에 배속을 고정하며 진행 중 타격을 재시간화하지 않는다. 런의 증강·은신 종료 공속과 한 축에서 합산하고 버프 배속은 최대 2.0이다. 스킬·독립 탄도/설치물 주기는 유지한다. 효과음은 원본 속도·피치를 유지하고 휘두름은 시작, 타격음은 실제 적중에 연결한다.', '']
    lines += ['', '## 파랑의 효과 유형', '', '| 효과 | 공통 상태 조건 | 레어도 | 기본 / 강화 | 내부 쿨타임 |', '|---|---|---|---|---|']
    blue_labels={'cooldownReduction':'쿨타임 감소','skillDamageIncrease':'스킬 피해량 증가'}
    for pair in m['blue']['requiredCombinations']:
        values='미정 / 미정'
        if pair['effect']=='cooldownReduction':
            reduction=m['blue']['effects']['cooldownReduction']['reductionAmountAndMode']
            values=f"기본 쿨타임의 {reduction['baseFraction']*100:g}% / {reduction['enhancedFraction']*100:g}%"
        elif pair['effect']=='skillDamageIncrease':
            increase=m['blue']['effects']['skillDamageIncrease']['increaseAmountAndMode']
            values=f"+{increase['base']*100:g}% / +{increase['enhanced']*100:g}%"
        cooldown=m['blue']['internalCooldownRules']['seconds']
        lines.append(f'| {blue_labels[pair["effect"]]} | {CONDITIONS[pair["condition"]]} | {names[pair["rarity"]]} | {values} | {cooldown}초 |')
    lines += ['', '기본 목록은 조건별로 빨강 3개·파랑 6개를 정의한다. 복합 조건 예외형은 별도 결정한다. 쿨타임 감소는 조건을 충족한 공격의 원본 무기에서 현재 진행 중인 같은 스킬의 대기를 줄인다. 감소량은 스킬의 전체 기본 쿨타임 × 감소율로 계산하며 남은 시간에 감소율을 곱하지 않는다. 전체 기본 쿨타임이 10초이고 4초가 남았다면 기본 15%는 1.5초를 차감해 2.5초가 남는다. 이전 사용의 지연 투사체·설치물도 다음 사용의 대기를 줄일 수 있다. 감소는 조건을 만족한 적중 시 즉시 적용하며 최소 대기시간은 현재 감소 대상의 쿨타임 시작부터 계산한다. 동시에 발동하는 서로 다른 잉크의 감소율은 합산한다. 최소 총 대기시간은 기본 쿨타임의 20%를 플레이 테스트 시작값으로 사용하며 스킬 쿨타임 시작부터 계산한다. 잉크 발동으로 대기를 다시 시작하지 않으며 최소 시간이 지난 뒤에는 감소로 즉시 준비될 수 있다. 쓰지 못한 감소량은 버리고 이월하지 않는다. 충전 수를 보유하는 스킬의 쿨감 처리는 미정이다. 기존 동일 피해 범위의 피해 증가율과 잉크 피해 증가율은 합산하며 세부 규칙은 [잉크 효과](effects.md#피해-증가율-합성)를 따른다. 파랑의 두 효과 유형은 기본/강화 모두 위 표의 내부 쿨타임을 플레이 테스트 시작값으로 사용하며 스킬 재사용 대기시간과 별도로 관리한다. 파랑 내부 쿨타임은 실제 잉크 발동 시 시작한다. 스킬 쿨감 실효량이 0이면 내부 쿨타임과 공격당 발동 기회를 보존하고 재시도에도 원래 적중·상태 조건을 요구한다. 동시에 조건을 충족한 쿨감 장착본은 도면 슬롯 순서대로 적용하며 매 장착본 처리 후 남은 스킬 쿨타임과 최소 총 대기시간을 다시 확인한다. 실제 감소량이 양수인 장착본만 쿨타임과 발동 기회를 사용한다. 감소율 합산과 하한은 유지한다.', '',
        '## 주기와 진행 속도 입력', '',
        '이속·빨강 기본 공격력 버프·슬로우는 조건 충족·두 무기 교대를 통해 유지율 90~100%에 접근하는 빌드를 목표로 한다. 무기별 50~60%는 검토 범위이며 노랑·빨강·슬로우의 확정 시간 수치는 위 표를 우선한다. 새 도면 무기의 모든 잉크는 준비 상태로 시작하고 기존 무기 교대는 쿨타임을 초기화하지 않는다. 비활성 무기의 잉크 쿨타임과 빨강 버프 지속시간은 계속 진행한다. 무기 소멸 후에도 기존 이속·방어막·몬스터 상태이상·플레이어의 빨강 버프는 각 수명대로 유지한다. 상태 유지·발동 지연을 반영해 실제 유지율을 측정한다. 하드 CC의 100% 유지를 일괄 목표로 삼지 않는다.', '',
        f'현행 일반 방 12개에서 8개를 고르는 495개 조합의 몬스터 수 외곽 범위는 {m["stageEnvelope"]["count"][0]}~{m["stageEnvelope"]["count"][1]}마리, 총 HP는 {m["stageEnvelope"]["hp"][0]}~{m["stageEnvelope"]["hp"][1]}다. 실제 선택 확률을 뜻하지 않는다.', '',
        f'시계탑 보스 총 HP는 {m["boss"]["hp"]}, 페이즈별 HP는 {m["boss"]["phaseHp"]}다. 기절은 부여되지만 패턴은 계속 진행하며 상태 확인으로 시간을 소비하지 않는다. 패턴에 잉크 주기를 맞추는 규칙은 없다.', '',
        '시간 배분·공격 기회는 `ink_content_balance_proposal.json`의 미측정 검토 가정이다. 기본 공격 DPS에는 스킬·사거리·적중률·교대 효율이 포함되지 않는다.', '',
        '| 무기 | 연속 기본 공격 DPS |', '|---|---:|']
    lines += [f'| {w["name"]} | {w["dps"]:.2f} |' for w in m['weapons']]
    lines += ['', '## 재생성과 검증', '',
        '`python3 tools/combat/build_ink_balance_proposal.py`로 생성하고 `--check`로 일치를 검사한다. `--preview`는 Git 제외 `outputs/ink-balance/model.json`에 입력·SHA256을 기록한다. 검사는 27개 조합·대상 상태 조건·비용/페널티 없음·피해 범위 분리·레어도·HP 보존을 다루며 실제 플레이 검증을 대신하지 않는다.']
    return '\n'.join(lines)+'\n'


def validate(m):
    expected={(t,e) for t in TRIGGERS for e in EFFECTS if not(t=='status_condition' and e in {'stun','root','slow'})}
    pairs=[(p['trigger'],p['effect']) for p in m['requiredCombinations']]
    assert len(pairs)==27 and set(pairs)==expected
    source=read('data/combat/weapon_ink_run_design.json')['inks']['effectGranting']
    assert source['requiredCombinationCount']==27
    cooldown_start=source['internalCooldown']['startTimingAndWithinWeaponSharing']
    assert cooldown_start['attackTriggeredPurple']=='window_expiry'
    assert cooldown_start['healAndShield']=='when_actual_resource_increase_positive'
    assert not cooldown_start['zeroResourceIncreaseConsumesCooldown']
    assert not cooldown_start['zeroResourceIncreaseConsumesAttackProc']
    assert cooldown_start['resourceRetryRequiresOriginalTrigger']
    resource_order=cooldown_start['simultaneousHealAndShieldApplication']
    assert resource_order['scope']=='same_weapon_same_trigger_event'
    assert resource_order['order']=='blueprint_slot_order'
    assert resource_order['recalculateAvailableCapacityAfterEachCopy']
    assert resource_order['zeroGainPreservesCooldownAndAttackProc']
    reservation=cooldown_start['attackTriggeredPurplePendingAttackReservation']
    assert reservation['selection']=='first_successful_status_application'
    assert not reservation['reserveAtAttackExecution']
    assert not reservation['winningAttackKeepsPerTargetApplication'] and not reservation['otherAttacksRespectCooldown']
    enhancement=read('data/combat/weapon_ink_run_design.json')['inks']['numericValues']['affectedParametersAndSharedValues']
    assert enhancement['enhancementScope']=='effect_values_and_duplicate_investment'
    enhancement_condition=read('data/combat/weapon_ink_run_design.json')['inks']['numericValues']['enhancementCauseAndConditions']
    assert enhancement_condition['condition']=='ink_color_matches_blueprint_slot_color'
    assert enhancement_condition['enhancedValuesOnMatch']
    assert enhancement_condition['mismatchedColorPlacement']=={'allowed':True,'numericVariant':'base'}
    assert enhancement_condition['slotColorAssignment']['determinedAt']=='blueprint_acquisition'
    assert enhancement_condition['slotColorAssignment']['fixedAfterAcquisition']
    slots=enhancement_condition['slotColorAssignment']
    assert slots['uncoloredSlots']=={'allowed':True,'appliedNumericVariant':'base'}
    assert slots['layoutSource']=='blueprints.normalRewardSlotCountsByOffer+blueprints.enhancedSlotChancePerSlot'
    assert slots['colorSource']=='blueprints.enhancedSlotColor'
    assert not enhancement['cooldownSharedBetweenBaseAndEnhanced'] and enhancement['sameTriggerEffectTypeAndTargetRules']
    data=read('data/combat/weapon_ink_run_design.json')
    application=data['inkApplication'];blueprints=data['blueprints']
    inks=data['inks']
    assert inks['timeAndWeaponLifecycle']['storedWeaponInkCooldowns']=='freeze'
    history=source['triggerRules']['weaponSwitch']['firstAttackAndHistoryUpdateRules']
    assert history['weaponIdentity']=='unique_weapon_instance'
    assert history['onWeaponDestruction']==history['onWeaponStored']=='preserve_previous_attack_weapon_identity'
    status=source['triggerRules']['statusCondition']
    assert set(status['supportedStatuses'])=={'stun','root','slow'}
    assert status['sourceScope']=='status_source_weapon_only'
    assert status['includesSuccessfulReapplication']
    assert not status['otherWeaponInksReact']
    red=inks['attackGranting']['procUnitAndOrder']
    assert red['triggeringHitScope']=='all_targets_of_same_hit'
    assert not red['requiresEachDamageTargetStatus'] and not red['retroactiveDamageChange']
    blue=inks['skillGranting']['effects']['cooldownReduction']
    assert blue['targetCooldown']=='currently_running_same_skill_on_source_weapon'
    assert blue['applicationTimingAndDuration']['olderExecutionCanReduceNewerExecutionCooldown']
    assert blue['applicationTimingAndDuration']['minimumWaitMeasuredFrom']=='current_skill_cooldown_start'
    assert inks['damageBonusComposition']['mode']=='additive_rates_with_existing_bonuses_in_same_damage_scope'
    assert inks['presentation']['nameStyle']=='functional_trigger_or_status_condition_plus_effect'
    transition=data['saveTransition']
    assert transition['policy']=='delete_legacy_play_data_and_start_with_new_rules'
    assert not transition['preserveLegacyUnlocks'] and not transition['convertLegacyItemsOrUnlocks']
    assert blueprints['minPaletteSlots']==4 and blueprints['maxPaletteSlots']==6 and blueprints['maxEnhancedSlots']==2
    assert blueprints['enhancedSlotColor']=={'selection':'uniform_among_allowed_colors','multipleEnhancedSlotsShareColor':True}
    stage_counts=blueprints['slotCountsByStage'];offers=blueprints['normalRewardSlotCountsByOffer']
    for stage in ('1','2','3'):
        counts=offers[stage]
        assert len(counts)==data['rewards']['normalChoicesPerStage'] and counts==sorted(counts)
        assert set(counts)==set(stage_counts[stage]) and all(1<=n<=6 for n in counts)
    for chance in blueprints['enhancedSlotChancePerSlot']['byStage'].values():
        assert chance is None or 0<chance<100
    assert application['capacity']=='one_ink_per_slot' and application['goldCost']==0
    assert application['filledSlotsFixed'] and not application['emptySlotsCanBeFilledLater']
    assert not application['laterInsertionLocksImmediately']
    assert application['allSlotsFixedOnConfirmation'] and not application['postConfirmationEditingAllowed']
    activation=data['activation']
    assert activation['requiresConfirmationBeforeUse'] and activation['confirmationLocksAllSlots']
    assert activation['method']=='confirm_palette_in_inventory' and activation['usableWithoutInk']
    assert activation['equipChoice']==['slot_1','slot_2','keep_in_bag']
    assert data['inventory']['pauseCombatAndTimers']
    assert data['inventory']['equipmentChangeCooldownSeconds']==5
    assert application['duplicateInkOnSameWeapon'] is True
    duplicate=application['duplicateEffectRules']
    assert duplicate['sameInkDamageIncreaseAndCooldownReduction']=='additive_per_equipped_copy'
    assert not duplicate['reprocCreatesAdditionalCopy']
    assert duplicate['cooldownSharing']=='independent_per_equipped_copy'
    assert duplicate['readyCopiesProc']=='all_ready_copies'
    assert duplicate['perCopyPerAttackExecution']==1
    lifecycle=read('data/combat/weapon_ink_run_design.json')['inks']['timeAndWeaponLifecycle']
    assert lifecycle['inactiveWeaponInkCooldowns']=='continue'
    assert lifecycle['newBlueprintWeaponInkCooldowns']=='ready' and not lifecycle['weaponSwitchResetsCooldowns']
    assert lifecycle['redBuffOnWeaponSwitch']=='retain_and_continue_duration'
    assert lifecycle['appliedEffectsOnWeaponDestruction']=='retain_with_existing_lifetimes'
    speed=source['movementSpeedStacking'];shield=source['shieldLifetimeAndCap']
    assert source['healAmount']['mode']=='fixed_health_points' and not source['healAmount']['scalesWithMaxHealth']
    assert source['healAmount']['base']==1 and source['healAmount']['enhanced']==2
    assert shield['generationAmount']['mode']=='fixed_shield_points' and not shield['generationAmount']['scalesWithMaxHealth']
    normal_shield_triggers={'basic_attack','skill_attack','weapon_switch','status_condition'}
    amounts=shield['generationAmount']['perInkBaseAndEnhancedValues']
    cooldowns=source['internalCooldown']['perEffectAndTriggerSeconds']['shield']
    assert set(amounts)==set(cooldowns)==normal_shield_triggers | {'dash_use'}
    for trigger in normal_shield_triggers:
        assert amounts[trigger]=={'base':7,'enhanced':10}
        assert cooldowns[trigger]==6
    assert amounts['dash_use']=={'base':7,'enhanced':10}
    assert cooldowns['dash_use']==8
    assert all(cooldowns['dash_use'] > cooldowns[t] for t in normal_shield_triggers)
    heal_cooldowns=source['internalCooldown']['perEffectAndTriggerSeconds']['heal']
    assert heal_cooldowns=={'basic_attack':6,'skill_attack':6,'weapon_switch':6,'dash_use':8,'status_condition':6}
    assert speed['differentInkStacking']=='additive_increase_rates' and speed['durationOwnership']=='per_effect_independent'
    assert speed['cap']['required'] and speed['cap']['valueDetermination']=='playtesting'
    assert speed['cap']['maximumIncreaseRate']==0.4
    movement=speed['effectValues']
    assert movement['increaseRate']=={'base':0.1,'enhanced':0.15}
    assert movement['durationSharedBetweenBaseAndEnhanced']
    assert movement['durationSecondsByTrigger']=={'basic_attack':3,'skill_attack':3,'weapon_switch':3,'dash_use':4,'status_condition':3}
    assert source['internalCooldown']['perEffectAndTriggerSeconds']['movement_speed_increase']=={'basic_attack':6,'skill_attack':6,'weapon_switch':6,'dash_use':8,'status_condition':6}
    assert source['internalCooldown']['startTimingAndWithinWeaponSharing']['movementSpeed']=='on_effect_activation'
    assert speed['cap']['scope']=='all_active_ink_effects_across_both_weapons_and_equipped_copies'
    assert not speed['cap']['includesPermanentMovementSpeedUpgrade']
    assert speed['duplicateInkStacking']=='additive_per_equipped_copy'
    assert not speed['sameInkReapplication']['stackMagnitude']
    assert speed['sameInkReapplication']['durationRule']=='reset_to_original_effect_duration'
    assert lifecycle['movementSpeedOnWeaponSwitch']=='retain_effect_and_continue_duration'
    assert shield['reapplication']=='add_to_remaining_shield'
    assert shield['lifetime']=='gradual_decay' and shield['expiresOverTime']
    assert shield['cap']['basis']=='maximum_health_fraction'
    assert shield['cap']['fraction']==0.2
    assert shield['decay']['graceSeconds']==10
    assert shield['dashShield']['generationTiming']=='dash_start'
    assert shield['decay']['hitRefreshRules']=='stop_decay_and_reset_grace'
    assert not shield['decay']['refreshRequiresShieldGeneration']
    refresh=shield['decay']['hitRefreshSources']
    assert refresh['weaponScope']=='both_weapons' and refresh['basicAndSkillDirectHits']
    assert refresh['projectileAndDeployableDirectHits']
    assert not refresh['requiresShieldInkProc']
    assert shield['decay']['rate']['mode']=='fixed_max_health_fraction_per_second'
    assert shield['dashShield']['decayRate']['mode']=='fixed_max_health_fraction_per_second'
    assert shield['dashShield']['decayRate']['fasterThanNormal']
    assert shield['decay']['rate']['fractionPerSecond']==0.05
    assert shield['dashShield']['decayRate']['fractionPerSecond']==0.2
    assert shield['dashShield']['decayRate']['multipleBatchDistribution']=='full_rate_independently_per_generated_batch'
    assert shield['dashShield']['decayRate']['fractionPerSecond'] > shield['decay']['rate']['fractionPerSecond']
    assert shield['dashShield']['decayStartTiming']=='after_dash_end_grace'
    assert shield['dashShield']['graceAfterDashEndSeconds']==3
    assert not shield['dashShield']['enemyHitRefreshesGrace']
    mixing=shield['dashShield']['mixingWithOtherShield']
    assert mixing['overflowHandling']=='fill_remaining_capacity_discard_excess'
    assert mixing['repeatedDashRefresh']=='independent_timer_per_generated_batch'
    assert mixing['amountAndTimers']=='separate_normal_and_dash'
    assert mixing['dashBatchDamageConsumptionOrder']=='earliest_grace_end_first'
    assert mixing['damageConsumptionOrder']==['dash','normal'] and mixing['capScope']=='combined_normal_and_dash'
    assert not shield['dashShield']['hitRequired'] and shield['dashShield']['disappearsFasterThanOtherShields']
    trigger=m['effectTriggerRules']
    # Heal and shield wait for actual monster HP loss; other attack inks fire on execution (effects.md).
    assert trigger['basicAndSkillAttack']=={'activationTiming':'attack_execution_except_heal_and_shield_on_positive_monster_health_loss','hitRequired':False,
        'healAndShield':{'activationTiming':'after_committed_positive_monster_health_loss','excludesDecoysAndPlayer':True,'lethalHitQualifies':True,'zeroDamageConsumesCooldownOrProc':False},
        'cancelledBeforeExecutionTriggers':False}
    switch=trigger['weaponSwitch'];dash=trigger['dashUse'];limit=trigger['attackProcLimit']
    assert switch['condition']=='previous_attack_used_different_weapon' and switch['owner']=='incoming_weapon'
    assert not switch['weaponChangeInputAloneTriggers'] and switch['statusTargets']=='qualifying_attack_hit_targets'
    assert switch['selfEffectTiming']=='qualifying_attack_execution' and not switch['selfEffectHitRequired']
    history=switch['firstAttackAndHistoryUpdateRules']
    assert not history['firstAttackTriggersSwitch']
    assert history['updateAt']=='attack_execution_even_on_miss'
    assert history['cancelledBeforeExecution']=='preserve_previous_attack_weapon' and history['compareBeforeUpdating']
    assert dash['activationTiming']=='dash_start_once' and dash['statusApplication']=='enemies_along_dash_path'
    assert dash['sourceWeaponSelection']=={'when':'dash_start','weapon':'currently_active_weapon','includesOtherWeapon':False,'onlyReadyEquippedCopies':True}
    assert not dash['requiresNextAttack']
    assert dash['cooldownStartTiming']=='self_effect_activation_or_purple_window_expiry'
    assert dash['healActivation']=={'timing':'on_enemy_contact_during_dash','pathDetection':'same_as_dash_status_effects','perEquippedCopyPerDash':1,'cooldownStartTiming':'when_actual_healing_positive','noEnemyContactTriggers':False}
    continuation=dash['interruptionAndWeaponSwitchHandling']
    assert continuation['weaponSwitchHandling']==continuation['weaponDestructionHandling']=='retain_dash_and_original_activated_path_effects'
    assert not continuation['activateNewWeaponInks']
    assert continuation['preserveOriginalInkCooldowns'] and continuation['preservePerTargetApplicationHistory']
    assert continuation['onInterruption']=='stop_new_contacts_preserve_already_applied_status'
    assert dash['pathWidthAndDetectionTiming']['applicationTiming']=='on_enemy_contact_during_dash'
    area=dash['pathWidthAndDetectionTiming']
    assert area['shapeBasis']=='player_hurtbox'
    assert area['expansionUnitsPerSide']==dict.fromkeys(('left','right','up','down'),0.25)
    assert not area['changesPlayerHurtbox']
    contact=dash['sameTargetReapplicationAndPathLifetime']
    assert contact['applicationsPerTargetPerDash']==1
    assert not contact['recontactReapplies'] and not contact['leavesPersistentPath']
    assert contact['appliedStatusKeepsOwnDuration']
    assert not dash['statusUsesAreaAroundDashPosition']
    assert dash['cooldownComparedToBasicAndSkill']=='longer_for_yellow_green_only'
    assert limit['scope']=='yellow_green_blue'
    assert limit['perInkPerAttackExecution']==1 and not limit['perHitOrTargetExtraProcs']
    assert limit['multiTargetStatusApplicationAllowed']
    targets=limit['multiHitStatusTargetSelection']
    assert targets['targets']=='all_hit_targets_of_same_attack_execution' and targets['applicationsPerTarget']=='every_hit_during_window'
    assert targets['laterHitsCanAddNewTargets'] and targets['repeatedHitsRefreshSameTarget']
    assert {(p['trigger'],p['effect']) for p in source['forbiddenCombinations']}=={('status_condition',s) for s in ['stun','root','slow']}
    c=m['conditions']
    assert c['subject']=='attack_target_monster' and c['states']==list(CONDITIONS)
    assert c['checkTiming']=='after_same_attack_status_application' and c['sameAttackAppliedStatusQualifies']
    assert c['hardControlReapplication']['durationRule']=='max_remaining_and_new_duration'
    assert not c['hardControlReapplication']['addDurations']
    hard_duplicates=c['hardControlReapplication']['sameInkDuplicateApplication']
    assert hard_duplicates['newDuration']=='effective_copies_capped_at_3_seconds'
    assert hard_duplicates['compareWithExisting']=='max_remaining_and_capped_new_duration'
    assert not hard_duplicates['combineStunAndRoot']
    assert hard_duplicates['durationCap']['required'] and hard_duplicates['durationCap']['valueDetermination']=='playtesting'
    assert hard_duplicates['durationCap']['seconds']==3
    assert c['hardControlReapplication']['perInkDurationSeconds']=={state:{'base':1,'enhanced':1.5} for state in ('stun','root')}
    for state in ('stun','root'):
        assert source['internalCooldown']['perEffectAndTriggerSeconds'][state]=={'basic_attack':3,'skill_attack':3,'weapon_switch':3,'dash_use':3}
    slow_effect=c['slowEffect']
    assert slow_effect['affectedSpeeds']==['movement','attack_motion']
    assert not slow_effect['affectsAttackReuseCooldown']
    phases=slow_effect['attackMotionPhaseDetails']
    assert phases['affectedPhases']==['windup','active','recovery']
    assert phases['motionAndHitTimingStaySynchronized']
    assert phases['changesDuringAttack']=='apply_current_slow_immediately_preserve_elapsed_progress_and_hit_history'
    assert phases['alreadyLaunchedProjectileSpeed']==phases['projectilesLaunchedDuringSlowSpeed']=='unchanged'
    boss_slow=slow_effect['bossApplicationDetails']
    assert boss_slow['statusApplies'] and not boss_slow['movementReductionApplies'] and not boss_slow['attackMotionReductionApplies']
    assert not boss_slow['cancelsPatterns'] and not boss_slow['changesPatternOrder']
    assert boss_slow['qualifiesForRedAndBlueConditions']
    assert slow_effect['movementReductionRate']==slow_effect['attackMotionReductionRate']=={'base':0.4,'enhanced':0.6}
    assert not slow_effect['durationSharedBetweenBaseAndEnhanced']
    assert slow_effect['durationSecondsByTrigger']=={'basic_attack':1,'skill_attack':1,'weapon_switch':1,'dash_use':1}
    assert source['internalCooldown']['perEffectAndTriggerSeconds']['slow']=={'basic_attack':3,'skill_attack':3,'weapon_switch':3,'dash_use':3}
    slow=c['slowStacking']
    assert slow['durationOwnership']=='per_effect_independent'
    assert slow['appliedStrength']=='strongest_active_slow_only' and not slow['addSlowRates']
    assert slow['sameSourceReapplication']['durationRule']=='reset_full_duration'
    assert not slow['sameSourceReapplication']['refreshOtherSources']
    reapply=source['statusTriggerSubjectAndEvent']['reapplicationAndFailure']
    assert reapply['successfulReapplicationTriggers']
    assert not reapply['requiresDurationOrStrengthIncrease'] and not reapply['failedApplicationTriggers']
    assert not c['consumesStatusDuration'] and not c['consumesResources'] and not c['penalty']
    assert set(m['redConditionRarities'])==set(CONDITIONS)
    assert all(v=='normal' for v in m['redConditionRarities'].values())
    assert all(p['rarity']=='normal' for p in m['requiredCombinations'])
    red=m['red'];blue=m['blue']
    assert red['trigger']=='damage_dealt_by_basic_attack' and not red['costRequired'] and not red['penalty']
    assert red['buff']['owner']=='player'
    assert red['buff']['affectedAttacksAndWeapons']['weaponScope']=='all_player_weapons'
    assert red['buff']['affectedAttacksAndWeapons']['includesUnarmed']
    assert red['buff']['instanceOwnership']=='per_source_weapon_same_ink'
    assert red['inactiveWeaponTimeAndStateLifecycle']['destroyedWeaponBuffUsable']
    assert lifecycle['destroyedWeaponAttackBuffUsable']
    assert red['buff']['affectedAttacksAndWeapons']['basicAttacks']
    assert not red['buff']['affectedAttacksAndWeapons']['skillAttacks']
    assert red['buff']['appliesToTriggeringHit']
    assert not red['buff']['damageIncreaseCap']['enabled']
    assert not blue['effects']['skillDamageIncrease']['stacking']['damageIncreaseCap']['enabled']
    assert red['buff']['attackPowerIncrease']=={'base':0.3,'enhanced':0.45}
    assert red['buff']['durationSeconds']==0.75 and red['cooldown']['seconds']==0
    assert not red['buff']['timeValuesSharedBetweenBaseAndEnhanced']
    assert blue['effects']['skillDamageIncrease']['appliesToTriggeringHit']
    target_rule=blue['effects']['skillDamageIncrease']['targetApplication']
    assert target_rule['requiresMatchingStatusAtDamageCalculation'] and target_rule['checkPerInkCondition']
    assert not target_rule['retroactiveDamageChange']
    assert blue['effects']['skillDamageIncrease']['stacking']['differentInkStacking']=='additive_increase_rates'
    duration=blue['effects']['skillDamageIncrease']['applicationTimingAndDuration']
    assert duration['scope']=='triggering_skill_execution'
    assert duration['startsAt']=='triggering_hit_before_damage_calculation'
    first_hit=duration['firstHitHandling']
    assert first_hit['order']==['apply_first_hit_status_effects','check_target_status','activate_ready_damage_increase','calculate_first_hit_damage']
    assert first_hit['firstHitCanActivateAndBenefit'] and first_hit['sameFirstHitStatusApplicationQualifies']
    assert first_hit['requiresReadyEquippedCopy'] and first_hit['requiresMatchingTargetStatus']
    example=first_hit['example']
    assert example['firstHitDamage']==example['baseFirstHitDamage']*(1+example['increaseRate'])
    assert duration['subsequentHitsAndDurationRules']=='triggering_hit_and_remaining_hits_of_same_execution'
    assert not duration['carriesToNextSkillExecution']
    assert blue['trigger']=='skill_attack' and not blue['penalty']
    assert blue['internalCooldownRules']['required']
    assert blue['internalCooldownRules']['seconds']==4
    assert blue['internalCooldownRules']['sharedBetweenBaseAndEnhanced']
    assert blue['effects']['skillDamageIncrease']['increaseAmountAndMode']=={'mode':'percentage','base':0.2,'enhanced':0.3}
    assert blue['internalCooldownRules']['startsAt']=='successful_ink_proc'
    zero_reduction=blue['effects']['cooldownReduction']['zeroActualReduction']
    assert not zero_reduction['consumesInternalCooldown'] and not zero_reduction['consumesAttackProcChance']
    assert zero_reduction['retryRequiresOriginalHitAndStatusConditions']
    blue_trigger=blue['triggerTimingAndHitRequirement']
    assert blue_trigger['activationTiming']=='skill_attack_hit' and blue_trigger['hitRequired']
    assert blue_trigger['statusCheckTarget']=='hit_monster'
    assert not blue_trigger['cancelledBeforeExecutionTriggers']
    assert set(blue['effects'])=={'cooldownReduction','skillDamageIncrease'}
    assert blue['effects']['cooldownReduction']['targetCooldown']=='currently_running_same_skill_on_source_weapon'
    assert blue['effects']['cooldownReduction']['minimumCooldownAndStacking']['differentInkStacking']=='additive_base_cooldown_reduction_rates'
    limits=blue['effects']['cooldownReduction']['minimumCooldownAndStacking']
    assert limits['minimumCooldown']['mode']=='fraction_of_base_cooldown'
    assert limits['minimumCooldown']['fraction']==0.2
    reduction_order=limits['simultaneousApplication']
    assert reduction_order['order']=='blueprint_slot_order'
    assert reduction_order['recalculateRemainingCooldownAndMinimumWaitAfterEachCopy']
    assert reduction_order['zeroReductionPreservesCooldownAndAttackProc']
    reduction=blue['effects']['cooldownReduction']['reductionAmountAndMode']
    assert reduction['baseFraction']==0.15 and reduction['enhancedFraction']==0.2
    assert reduction['calculationBasis']=='full_base_skill_cooldown'
    assert not reduction['usesRemainingCooldownAsBasis']
    example=reduction['example']
    assert example['reductionSeconds']==example['baseSkillCooldownSeconds']*example['reductionFraction']
    assert example['remainingAfterReductionSeconds']==example['remainingCooldownSeconds']-example['reductionSeconds']
    assert limits['minimumCooldown']['measuredFrom']=='skill_cooldown_start'
    assert not limits['minimumCooldown']['restartsOnInkProc']
    assert limits['excessReductionHandling']=='discard_without_carryover'
    assert blue['effects']['cooldownReduction']['reductionAmountAndMode']['mode']=='subtract_fraction_of_base_cooldown'
    assert all(not e['costRequired'] and e['rarity']=='normal' for e in blue['effects'].values())
    assert blue['effects']['skillDamageIncrease']['affectedAttacks']=={'basicAttacks':False,'skillAttacks':True}
    data=read('data/combat/weapon_ink_run_design.json')
    assert data['inventory']['weaponAndInkStorage']=='unlimited_and_independent'
    durability=data['durability']
    assert durability['maximumBySlots']=={str(n):durability['maximumBase']+durability['maximumPerSlot']*n for n in range(1,7)}
    accounting=data['durability']['damageAccounting']
    assert accounting['basis']=='actual_hp_removed_from_any_damageable_target' and not accounting['includeOverkill']
    final_hit=accounting['depletingHitHandling']
    assert final_hit['applyFullHitDamage'] and final_hit['remainingDurability']=='clamp_to_zero'
    assert final_hit['destroyTiming']=='after_depleting_hit_damage' and not final_hit['newAttacksAfterDestruction']
    attacks=accounting['alreadyCreatedAttackHandling']
    assert attacks['independentProjectilesAndDeployables']=='retain'
    assert attacks['weaponAttachedRemainingHits']=='terminate' and not attacks['afterSourceDestroyedChargeOtherWeapons']
    assert final_hit['multiTargetHitOrdering']=='apply_all_targets_of_same_hit_before_destruction'
    lingering=attacks['postDestructionInkProcs']
    assert lingering['allowed'] and lingering['rules']=='original_ink_rules'
    assert lingering['retainOriginalCooldownState'] and lingering['retainAttackProcHistory']
    assert not lingering['resetCooldownOrProcLimit']
    assert 'costIntensityPolicy' not in data['inks'] and 'costRiskNotice' not in data['activation']
    assert 'costKinds' not in red and 'consumeCostAgain' not in red['reprocAfterCooldown']
    blue_pairs=[(p['effect'],p['condition']) for p in blue['requiredCombinations']]
    assert len(blue_pairs)==6 and set(blue_pairs)=={(e,c) for e in blue['effects'] for c in CONDITIONS}
    assert all(p['rarity']=='normal' for p in blue['requiredCombinations'])
    assert red['requiredCombinationCount']==6 and blue['requiredCombinationCount']==6
    counts=m['contentCounts']
    by_color={color:sum(1 for p in m['requiredCombinations'] if p['effect'] in effects) for color,effects in source['colors'].items()}
    by_color.update(red=2*len(m['redConditionRarities']),blue=len(blue_pairs))
    assert counts['byColor']==by_color
    assert counts['effectGranting']==len(pairs) and counts['red']==6 and counts['blue']==6
    assert counts['total']==sum(by_color.values())==39
    assert not counts['baseAndEnhancedCountedSeparately'] and not counts['unadoptedExceptionCandidatesIncluded']
    assert sum(m['boss']['phaseHp'])==m['boss']['hp']
    assert all(w['dps']>0 for w in m['weapons'])
    rarity=m['rarity']
    assert [t['id'] for t in rarity['tiers']]==['normal','rare','epic']
    assert rarity['dropRatesAndWeights'] is None
    boss=read('data/combat/boss_status_design.json')
    assert boss['stunCanBeApplied'] and not boss['stunDurationCanBeConsumedByInk']
    assert not boss['stunStopsOrCancelsBossPatterns'] and not boss['stunPausesBossPatternClock']


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    parser.add_argument('--preview',action='store_true',help='write current inputs and policies to ignored outputs/ink-balance/model.json')
    args=parser.parse_args()
    result=model();validate(result);content=document(result)
    if args.check:
        if not DOC.exists() or DOC.read_text()!=content:
            raise SystemExit('Stale ink calibration document; regenerate with this tool.')
    else:
        DOC.write_text(content)
    if args.preview:
        out=ROOT/'outputs/ink-balance';out.mkdir(parents=True,exist_ok=True)
        (out/'model.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print('PASS: 39 inks (27 utility, 6 red, 6 blue), target status conditions, no costs or penalties, red/blue damage scopes, phase HP and '+('document matches' if args.check else 'document generated'))
